// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import type {
  ICacheInvalidationBackplane,
  IDistributedCache,
  ILocalCache,
  ITieredCache,
  TokenBucketDecision,
} from "@dcsv-io/d2-caching-abstractions";
import type { ILogger } from "@dcsv-io/d2-logging";
import {
  ErrorCodes,
  HttpStatusCode,
  notFound,
  ok,
  someFound,
  type D2Result,
} from "@dcsv-io/d2-result";

import {
  logInvalidationPublishFailed,
  logL1DropFailed,
  logL1InvalidationFailed,
  logL1WriteFailedAfterL2Success,
  TieredCacheOp,
  type TieredCacheOpName,
  TIERED_ERROR_CODE_UNKNOWN,
} from "./tiered-cache-log.js";

/**
 * Pinned registration-error message thrown by `*AndBroadcast*` when no
 * backplane was passed at construction. Same meaning as the .NET
 * `InvalidOperationException` thrown on a missing `ICacheInvalidationBackplane`.
 */
export const BACKPLANE_NOT_REGISTERED_MESSAGE =
  "ICacheInvalidationBackplane is not registered. Use set / remove " +
  "(no broadcast), or pass a backplane to DefaultTieredCache.";

/** Constructor dependencies for {@link DefaultTieredCache}. */
export interface DefaultTieredCacheDeps {
  /** Local (in-process) L1 cache. */
  l1: ILocalCache;
  /** Distributed (cluster) L2 cache. */
  l2: IDistributedCache;
  /** Required structured logger. */
  logger: ILogger;
  /**
   * Optional invalidation backplane. Required for `*AndBroadcast*` and
   * for cluster L1 coherency. When present, the tiered cache subscribes at
   * construction and drops L1 on every invalidation (everyone-acts).
   */
  backplane?: ICacheInvalidationBackplane;
}

/**
 * Composes one {@link ILocalCache} (L1) and one {@link IDistributedCache}
 * (L2) into a tiered cache. Reads check L1 first, fall through to L2, and
 * populate L1 on an L2 hit. Writes go to L2 first and L1 is written only if
 * L2 succeeded, so partial-write states are impossible. Atomic primitives
 * route through L2 (the cluster source of truth) and invalidate L1 as a
 * side effect. Optional {@link ICacheInvalidationBackplane} enables
 * cluster-wide L1 invalidation: this instance subscribes at construction
 * and drops L1 entries on every received invalidation (including its own,
 * per the universal everyone-acts rule).
 *
 * Pure composition: it does not own or dispose L1, L2, or the backplane.
 * Disposing this instance only unsubscribes its backplane handler.
 *
 * @see ITieredCache port contracts on `@dcsv-io/d2-caching-abstractions`.
 */
export class DefaultTieredCache implements ITieredCache, AsyncDisposable {
  private readonly l1: ILocalCache;
  private readonly l2: IDistributedCache;
  private readonly logger: ILogger;
  private readonly backplane: ICacheInvalidationBackplane | undefined;
  private readonly subscription: AsyncDisposable | undefined;
  private disposed = false;

  /**
   * Constructs a tiered cache. Throws `TypeError` when `l1`, `l2`, or
   * `logger` is nullish. When `backplane` is present, subscribes
   * synchronously for L1 invalidation.
   *
   * @param deps - L1, L2, logger, and optional backplane.
   */
  constructor(deps: DefaultTieredCacheDeps) {
    if (deps.l1 == null) {
      throw new TypeError("l1 is required");
    }

    if (deps.l2 == null) {
      throw new TypeError("l2 is required");
    }

    if (deps.logger == null) {
      throw new TypeError("logger is required");
    }

    this.l1 = deps.l1;
    this.l2 = deps.l2;
    this.logger = deps.logger;
    this.backplane = deps.backplane;

    if (deps.backplane !== undefined) {
      // Capture into locals for the lambda closure: the subscription may
      // outlive the constructor call, and the locals avoid races on a
      // disposed `this`.
      const capturedLogger = deps.logger;
      const capturedL1 = deps.l1;

      this.subscription = deps.backplane.subscribe(async (key, signal) => {
        const result = await capturedL1.remove(key, signal);

        if (!result.success) {
          logL1InvalidationFailed(
            capturedLogger,
            key,
            result.errorCode ?? TIERED_ERROR_CODE_UNKNOWN,
          );
        }
      });
    }
  }

  // ----- ICacheBasic -----

  /**
   * Reads a single value: L1 first; on miss, L2 then populate L1.
   * L1 hard-fail is treated as a miss (fall through to L2).
   */
  async get<T>(key: string, signal?: AbortSignal): Promise<D2Result<T>> {
    const l1 = await this.l1.get<T>(key, signal);

    if (l1.success) {
      return l1;
    }

    const l2 = await this.l2.get<T>(key, signal);

    if (!l2.success) {
      return l2;
    }

    // Populate L1 with the default L1 TTL (there is no atomic read of the
    // remaining L2 TTL). Ignore an L1 set failure, matching the .NET
    // fire-and-forget populate.
    await this.l1.set(key, l2.data as T, undefined, signal);

    return l2;
  }

  /**
   * Reads many keys: L1 partial hits first; missing keys from L2;
   * merge them into one result as the `getMany` rows of the Result mapping
   * table in the package README state.
   */
  async getMany<T>(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result<ReadonlyMap<string, T>>> {
    const l1 = await this.l1.getMany<T>(keys, signal);

    // All keys hit in L1: return early, before any missing-key work.
    if (l1.success && !l1.isPartialSuccess) {
      return l1;
    }

    // Hit map: ok or someFound (isPartialSuccess alone identifies someFound;
    // never gate on success, because the real TS someFound has success: false).
    const l1Hits: Map<string, T> =
      l1.success || l1.isPartialSuccess
        ? new Map(l1.data ?? new Map())
        : new Map();

    const missing = keys.filter((k) => !l1Hits.has(k));

    if (missing.length === 0) {
      return l1;
    }

    const l2 = await this.l2.getMany<T>(missing, signal);

    if (isNotFoundResult(l2)) {
      return l1Hits.size === 0
        ? notFound()
        : someFound({ data: l1Hits as ReadonlyMap<string, T> });
    }

    // Hard failure (serviceUnavailable, validationFailed and so on): not ok
    // and not someFound.
    if (!l2.success && !l2.isPartialSuccess) {
      return l2;
    }

    // ok or someFound: merge L2 hits into L1 hits and populate L1.
    const l2Hits = l2.data ?? new Map<string, T>();
    const merged = new Map(l1Hits);
    const populate = new Map<string, T>();

    for (const [k, v] of l2Hits) {
      merged.set(k, v);
      populate.set(k, v);
    }

    if (populate.size > 0) {
      // Ignore an L1 populate failure, matching .NET, which does not check
      // the GetMany populate.
      await this.l1.setMany(populate, undefined, signal);
    }

    if (merged.size === keys.length) {
      return ok(merged as ReadonlyMap<string, T>);
    }

    return someFound({ data: merged as ReadonlyMap<string, T> });
  }

  /**
   * Returns whether a key is present: L1 `true` short-circuits; else L2.
   */
  async exists(key: string, signal?: AbortSignal): Promise<D2Result<boolean>> {
    const l1 = await this.l1.exists(key, signal);

    if (l1.success && l1.data === true) {
      return l1;
    }

    return this.l2.exists(key, signal);
  }

  /**
   * Returns remaining TTL from L2 only (cluster source of truth).
   */
  getTtl(
    key: string,
    signal?: AbortSignal,
  ): Promise<D2Result<number | undefined>> {
    return this.l2.getTtl(key, signal);
  }

  /**
   * Writes L2-first, then L1 with the same value/expirationMs/signal.
   * An L1 failure after L2 succeeded logs a Warning and returns the L2 success.
   */
  async set<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    const l2 = await this.l2.set(key, value, expirationMs, signal);

    if (!l2.success) {
      return l2;
    }

    const l1 = await this.l1.set(key, value, expirationMs, signal);

    if (!l1.success) {
      logL1WriteFailedAfterL2Success(
        this.logger,
        TieredCacheOp.SET,
        key,
        l1.errorCode ?? TIERED_ERROR_CODE_UNKNOWN,
      );
    }

    return l2;
  }

  /**
   * Bulk write L2-first, then L1 with the same entries/expirationMs/signal.
   */
  async setMany<T>(
    entries: ReadonlyMap<string, T>,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    const l2 = await this.l2.setMany(entries, expirationMs, signal);

    if (!l2.success) {
      return l2;
    }

    const l1 = await this.l1.setMany(entries, expirationMs, signal);

    if (!l1.success) {
      logL1WriteFailedAfterL2Success(
        this.logger,
        TieredCacheOp.SET_MANY,
        `${entries.size} entries`,
        l1.errorCode ?? TIERED_ERROR_CODE_UNKNOWN,
      );
    }

    return l2;
  }

  /**
   * Removes L2-first, then L1. An L1 failure after L2 succeeded logs and
   * returns the L2 ok.
   */
  async remove(key: string, signal?: AbortSignal): Promise<D2Result> {
    const l2 = await this.l2.remove(key, signal);

    if (!l2.success) {
      return l2;
    }

    const l1 = await this.l1.remove(key, signal);

    if (!l1.success) {
      logL1WriteFailedAfterL2Success(
        this.logger,
        TieredCacheOp.REMOVE,
        key,
        l1.errorCode ?? TIERED_ERROR_CODE_UNKNOWN,
      );
    }

    return l2;
  }

  /**
   * Bulk remove L2-first, then L1.
   */
  async removeMany(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result> {
    const l2 = await this.l2.removeMany(keys, signal);

    if (!l2.success) {
      return l2;
    }

    const l1 = await this.l1.removeMany(keys, signal);

    if (!l1.success) {
      logL1WriteFailedAfterL2Success(
        this.logger,
        TieredCacheOp.REMOVE_MANY,
        `${keys.length} keys`,
        l1.errorCode ?? TIERED_ERROR_CODE_UNKNOWN,
      );
    }

    return l2;
  }

  // ----- ICacheAtomic -----

  /**
   * Set-if-absent via L2; when the write took effect, populate L1; when the
   * key already exists, drop L1. L1 side effects are fire-and-forget (a
   * failure is not logged).
   */
  async setNx<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    const l2 = await this.l2.setNx(key, value, expirationMs, signal);

    if (!l2.success) {
      return l2;
    }

    if (l2.data === true) {
      await this.l1.set(key, value, expirationMs, signal);
    } else {
      await this.l1.remove(key, signal);
    }

    return l2;
  }

  /**
   * Compare-and-set via L2, then drop L1 on every outcome: written,
   * comparison failed and store failure alike. This differs from
   * `increment`. Populating L1 with the written value the way
   * `setNx` does would reopen the race the conditional write exists to
   * close: the backplane subscriber drops L1 on its own task, so an
   * invalidation applied between the L2 hop and the populate would be
   * undone by the populate, and `get` returns an L1 hit without consulting
   * L2. After a failed write the node cannot know whether its copy still
   * matches the shared value either, and declining to trust a local copy is
   * the purpose of the member.
   *
   * When the write took effect, the member also invalidates the other
   * instances, on the terms {@link broadcastConditionalWrite} states.
   */
  async setIfEqual<T>(
    key: string,
    expected: T,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    const l2 = await this.l2.setIfEqual(
      key,
      expected,
      value,
      expirationMs,
      signal,
    );

    const l1 = await this.l1.remove(key, signal);

    if (!l1.success) {
      logL1DropFailed(
        this.logger,
        TieredCacheOp.SET_IF_EQUAL,
        key,
        l1.errorCode ?? TIERED_ERROR_CODE_UNKNOWN,
      );
    }

    await this.broadcastConditionalWrite(
      TieredCacheOp.SET_IF_EQUAL,
      key,
      l2,
      signal,
    );

    return l2;
  }

  /**
   * Compare-and-delete via L2, then drop L1 on every outcome.
   *
   * L1 is dropped whatever L2 returned, for the reason {@link setIfEqual}
   * gives: the comparison establishes a fact about the shared value at the
   * instant it ran and none about a local copy a moment later. When the entry
   * was removed, the local copy is of an entry that is gone; otherwise the node
   * cannot know whether its copy still matches the shared value.
   *
   * When the removal took effect, the member also invalidates the other
   * instances, on the terms {@link broadcastConditionalWrite} states.
   */
  async removeIfEqual<T>(
    key: string,
    expected: T,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    const l2 = await this.l2.removeIfEqual(key, expected, signal);

    const l1 = await this.l1.remove(key, signal);

    if (!l1.success) {
      logL1DropFailed(
        this.logger,
        TieredCacheOp.REMOVE_IF_EQUAL,
        key,
        l1.errorCode ?? TIERED_ERROR_CODE_UNKNOWN,
      );
    }

    await this.broadcastConditionalWrite(
      TieredCacheOp.REMOVE_IF_EQUAL,
      key,
      l2,
      signal,
    );

    return l2;
  }

  /**
   * Increment via L2 then always drop L1 (counters in L1 would diverge).
   */
  async increment(
    key: string,
    amount?: number,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<number>> {
    const l2 = await this.l2.increment(key, amount, expirationMs, signal);

    if (!l2.success) {
      return l2;
    }

    await this.l1.remove(key, signal);

    return l2;
  }

  /**
   * Pure L2 delegation; the bucket state belongs to the shared store. An L1
   * copy would reflect this node rather than the cluster, which defeats the
   * purpose of a shared budget.
   */
  consumeToken(
    key: string,
    refillTokens: number,
    refillIntervalMs: number,
    burst: number,
    cost = 1,
    signal?: AbortSignal,
  ): Promise<D2Result<TokenBucketDecision>> {
    return this.l2.consumeToken(
      key,
      refillTokens,
      refillIntervalMs,
      burst,
      cost,
      signal,
    );
  }

  /**
   * Pure L2 lock acquire; L1 is not involved.
   */
  acquireLock(
    key: string,
    lockId: string,
    expirationMs: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    return this.l2.acquireLock(key, lockId, expirationMs, signal);
  }

  /**
   * Pure L2 lock release: an ownership miss returns ok, and a store failure
   * is returned as is.
   */
  releaseLock(
    key: string,
    lockId: string,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    return this.l2.releaseLock(key, lockId, signal);
  }

  // ----- ICacheBroadcast -----

  /**
   * Tiered set then publish invalidation. A write failure skips the publish.
   * Missing backplane throws with {@link BACKPLANE_NOT_REGISTERED_MESSAGE}.
   */
  async setAndBroadcast<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    const setResult = await this.set(key, value, expirationMs, signal);

    if (!setResult.success) {
      return setResult;
    }

    return this.publish(key, signal);
  }

  /**
   * Tiered setMany then bulk publish. A write failure skips the publish.
   */
  async setManyAndBroadcast<T>(
    entries: ReadonlyMap<string, T>,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    const setResult = await this.setMany(entries, expirationMs, signal);

    if (!setResult.success) {
      return setResult;
    }

    return this.publishMany([...entries.keys()], signal);
  }

  /**
   * Tiered remove then publish. A write failure skips the publish.
   */
  async removeAndBroadcast(
    key: string,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    const removeResult = await this.remove(key, signal);

    if (!removeResult.success) {
      return removeResult;
    }

    return this.publish(key, signal);
  }

  /**
   * Tiered removeMany then bulk publish. A write failure skips the publish.
   */
  async removeManyAndBroadcast(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result> {
    const removeResult = await this.removeMany(keys, signal);

    if (!removeResult.success) {
      return removeResult;
    }

    return this.publishMany(keys, signal);
  }

  /**
   * Unsubscribes the backplane handler only. Does not dispose L1, L2,
   * or the backplane. Idempotent. Ops remain callable after dispose.
   */
  async dispose(): Promise<void> {
    if (this.disposed) {
      return;
    }

    this.disposed = true;

    if (this.subscription !== undefined) {
      await this.subscription[Symbol.asyncDispose]();
    }
  }

  /** Alias for {@link dispose}. */
  async [Symbol.asyncDispose](): Promise<void> {
    await this.dispose();
  }

  /**
   * Invalidates the other instances' L1 copies of a key that a conditional
   * write replaced or removed at L2.
   *
   * The plain writes leave the broadcast to the caller, which picks
   * `setAndBroadcast` or `set`. A conditional write has no such pair. When the
   * write took effect, the comparison succeeded against the shared value and
   * the write replaced it, so every other instance now holds a copy of the
   * value this write superseded. The publish runs in that case alone: a
   * failed comparison changed nothing at L2, so the other instances' copies
   * are as good as they were.
   *
   * It runs only where the host passed a backplane, and a publish that fails
   * is logged rather than returned: this member must return whether the
   * conditional write took effect, and a failed invalidation does not
   * change that. The other instances then serve their local copies until those
   * entries expire.
   *
   * @param operation - The member publishing, for the log line.
   * @param key - The key that was written.
   * @param write - What L2 returned.
   */
  private async broadcastConditionalWrite(
    operation: TieredCacheOpName,
    key: string,
    write: D2Result<boolean>,
    signal?: AbortSignal,
  ): Promise<void> {
    if (this.backplane === undefined || !write.success || write.data !== true) {
      return;
    }

    const published = await this.backplane.publishInvalidation(key, signal);

    if (!published.success) {
      logInvalidationPublishFailed(
        this.logger,
        operation,
        key,
        published.errorCode ?? TIERED_ERROR_CODE_UNKNOWN,
      );
    }
  }

  private async publish(key: string, signal?: AbortSignal): Promise<D2Result> {
    if (this.backplane === undefined) {
      throw new Error(BACKPLANE_NOT_REGISTERED_MESSAGE);
    }

    return this.backplane.publishInvalidation(key, signal);
  }

  private async publishMany(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result> {
    if (this.backplane === undefined) {
      throw new Error(BACKPLANE_NOT_REGISTERED_MESSAGE);
    }

    return this.backplane.publishInvalidationMany(keys, signal);
  }
}

function isNotFoundResult(r: D2Result<unknown>): boolean {
  return (
    !r.success &&
    (r.statusCode === HttpStatusCode.NotFound ||
      r.errorCode === ErrorCodes.GEN_NOT_FOUND)
  );
}
