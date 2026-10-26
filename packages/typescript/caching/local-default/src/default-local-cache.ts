// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import {
  createLocalCacheOptions,
  InputFailures,
  type ILocalCache,
  type LocalCacheOptions,
  type TokenBucketDecision,
} from "@dcsv-io/d2-caching-abstractions";
import {
  conflict,
  notFound,
  ok,
  someFound,
  type D2Result,
} from "@dcsv-io/d2-result";
import { falsey } from "@dcsv-io/d2-utilities";

import {
  createLocalCacheCounters,
  type LocalCacheCounters,
} from "./local-cache-telemetry.js";

interface CacheEntry {
  value: unknown;
  expiresAt?: number;
}

interface LockEntry {
  lockId: string;
  expiresAt: number;
}

/**
 * One token bucket's stored tokens in thousandths, the instant they were
 * counted, and the accrual that has not yet amounted to a whole thousandth.
 * The .NET flavor stores the same values in the same units.
 */
interface TokenBucketState {
  tokensMilli: number;
  atMs: number;
  accrualRemainder: number;
}

const DISPOSED_MESSAGE = "DefaultLocalCache is disposed.";

/**
 * The unit in which token-bucket state is held: thousandths of a token, which
 * keeps the refill integer arithmetic. One elapsed millisecond adds a
 * fraction of a token at most rates, and a bucket that floored that fraction
 * to zero would never refill.
 */
const TOKEN_SCALE = 1000;

/** Whether a stored value is bucket state rather than something else. */
function isTokenBucketState(value: unknown): value is TokenBucketState {
  return (
    typeof value === "object" &&
    value !== null &&
    typeof (value as TokenBucketState).tokensMilli === "number" &&
    typeof (value as TokenBucketState).atMs === "number" &&
    typeof (value as TokenBucketState).accrualRemainder === "number"
  );
}

/**
 * In-process implementation of {@link ILocalCache}. Values and counters
 * live in an LRU `Map`; locks live in a dedicated lock `Map`. Every
 * operation body is fully synchronous (methods are `async` only to match
 * the interface signatures), so check-then-write windows are atomic on the
 * Node event loop.
 *
 * Per-call failures return `@dcsv-io/d2-result` shapes (`notFound`,
 * `validationFailed` via {@link InputFailures}, `conflict`). Lifecycle
 * misuse (ops after {@link dispose}) throws a plain `Error` with the
 * pinned message `"DefaultLocalCache is disposed."`.
 *
 * Counters bind to the global OpenTelemetry MeterProvider at
 * construction time. Construct caches after telemetry setup, or the
 * counters bind to the no-op meter for the life of the instance.
 *
 * `signal` parameters are accepted for interface parity and ignored:
 * every operation completes synchronously in process.
 *
 * Clock note: the injected clock must be non-decreasing. A backwards
 * jump can transiently resurrect an expired-but-unevicted entry.
 *
 * @see ICacheBasic / ICacheAtomic port contracts on `@dcsv-io/d2-caching-abstractions`.
 */
export class DefaultLocalCache implements ILocalCache, Disposable {
  private readonly options: LocalCacheOptions;
  private readonly clock: () => number;
  private readonly counters: LocalCacheCounters;
  private readonly store = new Map<string, CacheEntry>();
  private readonly locks = new Map<string, LockEntry>();
  private disposed = false;

  /**
   * Constructs a local cache. Merges `options` over
   * `LOCAL_CACHE_DEFAULTS` via `createLocalCacheOptions`, then rejects
   * structurally broken numbers with `RangeError`.
   *
   * @param options - Partial options; omitted fields take defaults.
   * @param clock - Epoch-ms clock (defaults to the system epoch-ms clock).
   *   Sole time source for value TTL, lock expiry, and `getTtl` remaining
   *   arithmetic.
   */
  constructor(
    options?: Partial<LocalCacheOptions>,
    clock: () => number = Date.now,
  ) {
    const merged = createLocalCacheOptions(options);

    if (!Number.isSafeInteger(merged.maxEntries) || merged.maxEntries < 0) {
      throw new RangeError(
        `LocalCacheOptions.maxEntries must be a non-negative safe ` +
          `integer, got ${String(merged.maxEntries)}.`,
      );
    }

    if (!Number.isFinite(merged.defaultExpirationMs)) {
      throw new RangeError(
        `LocalCacheOptions.defaultExpirationMs must be a finite number, ` +
          `got ${String(merged.defaultExpirationMs)}.`,
      );
    }

    this.options = merged;
    this.clock = clock;
    this.counters = createLocalCacheCounters();
  }

  /**
   * Clears both maps and marks the instance disposed. Idempotent.
   * Subsequent operations throw `"DefaultLocalCache is disposed."`.
   */
  dispose(): void {
    if (this.disposed) {
      return;
    }

    this.store.clear();
    this.locks.clear();
    this.disposed = true;
  }

  /** Delegates to {@link dispose} for `using` declarations. */
  [Symbol.dispose](): void {
    this.dispose();
  }

  /** @inheritdoc */
  async get<T>(key: string, signal?: AbortSignal): Promise<D2Result<T>> {
    void signal;
    this.throwIfDisposed();

    if (falsey(key)) {
      return InputFailures.required<T>("key");
    }

    const entry = this.readLive(this.prefixed(key));

    if (entry === undefined) {
      this.counters.misses.add(1);

      return notFound();
    }

    this.counters.hits.add(1);

    return ok(entry.value as T);
  }

  /** @inheritdoc */
  async getMany<T>(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result<ReadonlyMap<string, T>>> {
    void signal;
    this.throwIfDisposed();

    if (falsey(keys)) {
      return InputFailures.required<ReadonlyMap<string, T>>("keys");
    }

    for (const key of keys) {
      if (falsey(key)) {
        return InputFailures.required<ReadonlyMap<string, T>>("keys");
      }
    }

    const hits = new Map<string, T>();
    let hitCount = 0;

    for (const key of keys) {
      const entry = this.readLive(this.prefixed(key));

      if (entry !== undefined) {
        hits.set(key, entry.value as T);
        hitCount++;
      }
    }

    this.counters.hits.add(hitCount);
    this.counters.misses.add(keys.length - hitCount);

    if (hitCount === 0) {
      return notFound();
    }

    if (hitCount === keys.length) {
      return ok(hits);
    }

    return someFound({ data: hits });
  }

  /** @inheritdoc */
  async exists(key: string, signal?: AbortSignal): Promise<D2Result<boolean>> {
    void signal;
    this.throwIfDisposed();

    if (falsey(key)) {
      return InputFailures.required<boolean>("key");
    }

    const entry = this.readLive(this.prefixed(key));

    return ok(entry !== undefined);
  }

  /** @inheritdoc */
  async getTtl(
    key: string,
    signal?: AbortSignal,
  ): Promise<D2Result<number | undefined>> {
    void signal;
    this.throwIfDisposed();

    if (falsey(key)) {
      return InputFailures.required<number | undefined>("key");
    }

    const entry = this.readLive(this.prefixed(key));

    if (entry === undefined) {
      return notFound();
    }

    if (entry.expiresAt === undefined) {
      return ok(undefined);
    }

    return ok(entry.expiresAt - this.clock());
  }

  /** @inheritdoc */
  async set<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    void signal;
    this.throwIfDisposed();

    if (falsey(key)) {
      return InputFailures.required("key");
    }

    if (this.isBadMs(expirationMs)) {
      return InputFailures.invalid("expirationMs");
    }

    this.setCore(this.prefixed(key), value, expirationMs);
    this.counters.sets.add(1);

    return ok();
  }

  /** @inheritdoc */
  async setMany<T>(
    entries: ReadonlyMap<string, T>,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    void signal;
    this.throwIfDisposed();

    if (falsey(entries)) {
      return InputFailures.required("entries");
    }

    if (this.isBadMs(expirationMs)) {
      return InputFailures.invalid("expirationMs");
    }

    for (const key of entries.keys()) {
      if (falsey(key)) {
        return InputFailures.required("entries");
      }
    }

    for (const [key, value] of entries) {
      this.setCore(this.prefixed(key), value, expirationMs);
    }

    this.counters.sets.add(entries.size);

    return ok();
  }

  /** @inheritdoc */
  async remove(key: string, signal?: AbortSignal): Promise<D2Result> {
    void signal;
    this.throwIfDisposed();

    if (falsey(key)) {
      return InputFailures.required("key");
    }

    this.store.delete(this.prefixed(key));
    this.counters.removes.add(1);

    return ok();
  }

  /** @inheritdoc */
  async removeMany(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result> {
    void signal;
    this.throwIfDisposed();

    if (falsey(keys)) {
      return InputFailures.required("keys");
    }

    for (const key of keys) {
      if (falsey(key)) {
        return InputFailures.required("keys");
      }
    }

    for (const key of keys) {
      this.store.delete(this.prefixed(key));
    }

    this.counters.removes.add(keys.length);

    return ok();
  }

  /** @inheritdoc */
  async setNx<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    void signal;
    this.throwIfDisposed();

    if (falsey(key)) {
      return InputFailures.required<boolean>("key");
    }

    if (this.isBadMs(expirationMs)) {
      return InputFailures.invalid<boolean>("expirationMs");
    }

    const prefixed = this.prefixed(key);
    const entry = this.readLive(prefixed);

    if (entry !== undefined) {
      return ok(false);
    }

    this.setCore(prefixed, value, expirationMs);
    this.counters.sets.add(1);

    return ok(true);
  }

  /** @inheritdoc */
  async setIfEqual<T>(
    key: string,
    expected: T,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    void signal;
    this.throwIfDisposed();

    if (falsey(key)) {
      return InputFailures.required<boolean>("key");
    }

    if (this.isBadMs(expirationMs)) {
      return InputFailures.invalid<boolean>("expirationMs");
    }

    const prefixed = this.prefixed(key);
    const entry = this.readLive(prefixed);

    // An absent key returns false: there is nothing to compare against, and
    // the caller's copy must not be written over the authoritative source that
    // the absence sends the next reader to.
    if (entry === undefined) {
      return ok(false);
    }

    // The Map holds values with no equality of its own, so the comparison is
    // structural over the JSON form. Reference identity here would refuse
    // nearly every write a caller expects to succeed, which is a silent
    // failure rather than a loud one.
    if (!structurallyEqual(entry.value, expected)) {
      return ok(false);
    }

    this.setCore(prefixed, value, expirationMs);
    this.counters.sets.add(1);

    return ok(true);
  }

  /** @inheritdoc */
  async removeIfEqual<T>(
    key: string,
    expected: T,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    void signal;
    this.throwIfDisposed();

    if (falsey(key)) {
      return InputFailures.required<boolean>("key");
    }

    const prefixed = this.prefixed(key);
    const entry = this.readLive(prefixed);

    // An absent key returns false: there is nothing to compare against and
    // nothing left to remove.
    if (entry === undefined) {
      return ok(false);
    }

    // The Map holds values with no equality of its own, so the comparison is
    // structural over the JSON form, as it is on setIfEqual.
    if (!structurallyEqual(entry.value, expected)) {
      return ok(false);
    }

    this.store.delete(prefixed);
    this.counters.removes.add(1);

    return ok(true);
  }

  /** @inheritdoc */
  async increment(
    key: string,
    amount?: number,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<number>> {
    void signal;
    this.throwIfDisposed();

    if (falsey(key)) {
      return InputFailures.required<number>("key");
    }

    if (this.isBadMs(expirationMs)) {
      return InputFailures.invalid<number>("expirationMs");
    }

    if (amount !== undefined && !Number.isSafeInteger(amount)) {
      return InputFailures.invalid<number>("amount");
    }

    const n = amount ?? 1;
    const prefixed = this.prefixed(key);
    const entry = this.readLive(prefixed);

    if (entry !== undefined) {
      if (
        typeof entry.value !== "number" ||
        !Number.isSafeInteger(entry.value)
      ) {
        return conflict<number>();
      }

      const next = entry.value + n;

      // JS numbers lose integer precision outside +/-MAX_SAFE_INTEGER.
      // Refuse before write so the stored counter stays exact.
      if (!Number.isSafeInteger(next)) {
        return InputFailures.invalid<number>("amount");
      }

      // Preserve expiresAt verbatim; ignore expirationMs on existing path.
      this.touch(prefixed, { value: next, expiresAt: entry.expiresAt });

      return ok(next);
    }

    this.setCore(prefixed, n, expirationMs);
    this.counters.sets.add(1);

    return ok(n);
  }

  /** @inheritdoc */
  async consumeToken(
    key: string,
    refillTokens: number,
    refillIntervalMs: number,
    burst: number,
    cost = 1,
    signal?: AbortSignal,
  ): Promise<D2Result<TokenBucketDecision>> {
    void signal;
    this.throwIfDisposed();

    const invalid = InputFailures.tokenBucket(
      key,
      refillTokens,
      refillIntervalMs,
      burst,
      cost,
    );

    if (invalid !== undefined) {
      return invalid;
    }

    const prefixed = this.prefixed(key);
    const capacity = burst * TOKEN_SCALE;
    const charge = cost * TOKEN_SCALE;
    let at = this.clock();
    const entry = this.readLive(prefixed);
    let tokens = capacity;
    let remainder = 0;

    if (entry !== undefined) {
      // The bucket shares the value namespace, so a key holding something
      // else returns conflict as increment does.
      if (!isTokenBucketState(entry.value)) {
        return conflict<TokenBucketDecision>();
      }

      at = Math.max(at, entry.value.atMs);
      const elapsed = at - entry.value.atMs;

      // The remainder the last refill could not spend on a whole thousandth is
      // added back before dividing. Dropping it each time would refill at a
      // rate the arrival rate itself reduced, down to no refill at all on a
      // bucket taking arrivals inside one thousandth's worth of time.
      const accrued =
        elapsed * refillTokens * TOKEN_SCALE + entry.value.accrualRemainder;

      tokens = entry.value.tokensMilli + Math.floor(accrued / refillIntervalMs);
      remainder = accrued % refillIntervalMs;

      if (tokens >= capacity) {
        tokens = capacity;
        remainder = 0;
      }
    }

    const admitted = tokens >= charge;
    let retryAfterMs = 0;

    if (admitted) {
      tokens -= charge;
    } else {
      retryAfterMs = Math.ceil(
        ((charge - tokens) * refillIntervalMs - remainder) /
          (refillTokens * TOKEN_SCALE),
      );
    }

    // A refill from empty to full plus one interval, reset on every use: a
    // bucket nobody touches again is dropped, and a caller returning after
    // that lifetime finds the full bucket the refill would have given it.
    this.setCore(
      prefixed,
      {
        tokensMilli: tokens,
        atMs: at,
        accrualRemainder: remainder,
      } satisfies TokenBucketState,
      Math.floor((burst * refillIntervalMs) / refillTokens) + refillIntervalMs,
    );

    return ok({ admitted, retryAfterMs });
  }

  /** @inheritdoc */
  async acquireLock(
    key: string,
    lockId: string,
    expirationMs: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    void signal;
    this.throwIfDisposed();

    if (falsey(key)) {
      return InputFailures.required<boolean>("key");
    }

    if (falsey(lockId)) {
      return InputFailures.required<boolean>("lockId");
    }

    if (!Number.isFinite(expirationMs) || expirationMs <= 0) {
      return InputFailures.invalid<boolean>("expirationMs");
    }

    const prefixed = this.prefixed(key);
    const now = this.clock();
    const existing = this.locks.get(prefixed);

    if (existing !== undefined && existing.expiresAt > now) {
      return ok(false);
    }

    this.locks.set(prefixed, {
      lockId,
      expiresAt: now + expirationMs,
    });

    return ok(true);
  }

  /** @inheritdoc */
  async releaseLock(
    key: string,
    lockId: string,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    void signal;
    this.throwIfDisposed();

    if (falsey(key)) {
      return InputFailures.required("key");
    }

    if (falsey(lockId)) {
      return InputFailures.required("lockId");
    }

    const prefixed = this.prefixed(key);
    const existing = this.locks.get(prefixed);

    if (existing !== undefined && existing.lockId === lockId) {
      this.locks.delete(prefixed);
    }

    return ok();
  }

  private throwIfDisposed(): void {
    if (this.disposed) {
      throw new Error(DISPOSED_MESSAGE);
    }
  }

  private prefixed(key: string): string {
    return falsey(this.options.keyPrefix) ? key : this.options.keyPrefix + key;
  }

  private isBadMs(expirationMs: number | undefined): boolean {
    return (
      expirationMs !== undefined &&
      (!Number.isFinite(expirationMs) || expirationMs <= 0)
    );
  }

  private readLive(prefixed: string): CacheEntry | undefined {
    const entry = this.store.get(prefixed);

    if (entry === undefined) {
      return undefined;
    }

    if (this.isExpired(entry)) {
      this.store.delete(prefixed);
      this.counters.evictions.add(1);

      return undefined;
    }

    this.touch(prefixed, entry);

    return entry;
  }

  private isExpired(entry: CacheEntry): boolean {
    return entry.expiresAt !== undefined && entry.expiresAt <= this.clock();
  }

  private touch(prefixed: string, entry: CacheEntry): void {
    this.store.delete(prefixed);
    this.store.set(prefixed, entry);
  }

  private setCore(
    prefixed: string,
    value: unknown,
    expirationMs: number | undefined,
  ): void {
    const effective = expirationMs ?? this.options.defaultExpirationMs;
    const expiresAt = effective > 0 ? this.clock() + effective : undefined;
    const entry: CacheEntry =
      expiresAt === undefined ? { value } : { value, expiresAt };

    // Unconditional overwrite (replacing an expired entry is not an eviction).
    // delete then set (via touch) repositions the entry to MRU; Map.set alone
    // keeps the order.
    this.touch(prefixed, entry);
    this.enforceCapacity();
  }

  private enforceCapacity(): void {
    while (this.store.size > this.options.maxEntries && this.store.size > 0) {
      this.evictHead();
    }
  }

  private evictHead(): void {
    // Requires store.size > 0.
    const first = this.store.keys().next().value as string;

    this.store.delete(first);
    this.counters.evictions.add(1);
  }
}

/**
 * Structural equality over the JSON shape of two values, key-order
 * independent. This is what `setIfEqual` compares with, because the store
 * holds values in a `Map` that has no equality of its own and reference
 * identity would refuse nearly every write a caller expects to succeed.
 *
 * An object that keeps its state outside its own enumerable properties
 * (a `Date`, a `Temporal` value, a `Map`, a `Set`) is compared on the JSON form
 * the contract states: its `toJSON` result where it has one, and otherwise
 * the comparison returns false, because neither the walk nor the JSON form can
 * tell two of them apart. A false result leaves the stored value in place, so a
 * comparison this walk cannot make refuses the write rather than taking it.
 */
function structurallyEqual(left: unknown, right: unknown): boolean {
  if (Object.is(left, right)) {
    return true;
  }

  if (
    typeof left !== "object" ||
    typeof right !== "object" ||
    left === null ||
    right === null
  ) {
    return false;
  }

  if (Array.isArray(left) || Array.isArray(right)) {
    if (!Array.isArray(left) || !Array.isArray(right)) {
      return false;
    }

    return (
      left.length === right.length &&
      left.every((item, index) => structurallyEqual(item, right[index]))
    );
  }

  const leftJson = jsonForm(left);
  const rightJson = jsonForm(right);

  if (leftJson !== left || rightJson !== right) {
    return structurallyEqual(leftJson, rightJson);
  }

  if (opaqueToTheWalk(left) || opaqueToTheWalk(right)) {
    return false;
  }

  const leftEntries = Object.entries(left as Record<string, unknown>);
  const rightRecord = right as Record<string, unknown>;
  const rightKeys = Object.keys(rightRecord);

  if (leftEntries.length !== rightKeys.length) {
    return false;
  }

  return leftEntries.every(
    ([key, item]) =>
      Object.hasOwn(rightRecord, key) &&
      structurallyEqual(item, rightRecord[key]),
  );
}

/**
 * The value `JSON.stringify` would serialize in place of this object, or the
 * object itself when it declares no `toJSON`.
 */
function jsonForm(value: object): unknown {
  const candidate = (value as { toJSON?: unknown }).toJSON;

  return typeof candidate === "function"
    ? (candidate as () => unknown).call(value)
    : value;
}

/**
 * Whether an object has state the property walk cannot reach: it declares
 * no own enumerable property and is not a plain object, so a walk over own
 * properties finds no difference between two of them.
 */
function opaqueToTheWalk(value: object): boolean {
  if (Object.keys(value).length > 0) return false;

  const proto = Object.getPrototypeOf(value) as object | null;

  return proto !== Object.prototype && proto !== null;
}
