// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import type { D2Result } from "@dcsv-io/d2-result";

import type { TokenBucketDecision } from "./token-bucket-decision.js";

/**
 * Atomic primitives: set-if-absent, compare-and-set, compare-and-delete,
 * increment, token-bucket consume, lock acquire and release.
 * Every cache flavor ({@link ILocalCache}, {@link IDistributedCache},
 * {@link ITieredCache}) implements them; the atomicity
 * scope depends on the implementing cache.
 *
 * **Scope per marker:**
 * - {@link ILocalCache}: per-process. Atomicity is guaranteed within this
 *   instance only.
 * - {@link IDistributedCache}: cluster-wide. The remote store enforces
 *   atomicity (e.g. Redis SETNX / INCR).
 * - {@link ITieredCache}: cluster-wide. Operations go through L2 (the source
 *   of truth); L1 is invalidated or refreshed as a side effect.
 *
 * The injected marker interface selects the scope.
 * The same atomic op called via `ILocalCache` coordinates within one
 * process; via `IDistributedCache` or `ITieredCache` it coordinates
 * across the cluster.
 *
 * **Counter width:** `number` (not `bigint`) is an intentional TS
 * ergonomic delta vs .NET `long`. Implementations must stay within
 * `Number.MAX_SAFE_INTEGER`.
 */
export interface ICacheAtomic {
  /**
   * Sets a value only if the key is not already present (atomic).
   *
   * @returns `ok(true)` if the value was written. `ok(false)` if the
   *   key already existed (no write). Failure on backing-store error.
   */
  setNx<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>>;

  /**
   * Replaces the stored value only if what is stored right now equals
   * `expected` (atomic compare-and-set).
   *
   * **The comparison is by value.** A distributed implementation compares
   * the serialized bytes it stores; an in-process implementation compares
   * the stored value structurally over its JSON form. So `expected` must
   * be a type whose equality is by value in every flavor. A plain object
   * of primitives satisfies this; a class with identity does not, and
   * for those the result differs per flavor with nothing reporting it.
   * The requirement is stated rather than declared as a type constraint
   * because a constraint enforced on one runtime only would make the two
   * contracts differ.
   *
   * **An absent key returns `ok(false)`.** A copy of what used to be stored
   * has nothing to compare against once the key is gone, and writing anyway
   * would overwrite the value the next reader loads in its place.
   *
   * **In the tiered flavor the local tier is dropped on every outcome**:
   * written, comparison failed and store failure alike. The comparison holds
   * for the shared value at the instant it ran and says nothing about a
   * local copy a moment later, so the local copy is dropped.
   *
   * **The tiered flavor also invalidates the other instances when the write
   * took effect**, where a backplane was passed. In that case the write
   * replaced the shared value, so every other instance's local copy is the
   * value this write superseded. When the comparison failed, nothing at the
   * shared store changed and nothing is published. A publish that fails is
   * logged and does not change the returned result: the other instances then
   * serve their local copy until it expires. With no backplane there is
   * nothing to publish on, and a key held in more than one local tier goes
   * stale until its TTL.
   *
   * @param expirationMs - Optional TTL in milliseconds. When omitted, the
   *   store's default expiration applies, as in {@link setNx} and unlike
   *   {@link increment}, because this member replaces a value rather than
   *   accumulating onto one.
   * @returns `ok(true)` if the value was written. `ok(false)` if the
   *   stored value was not `expected`, which includes the key being
   *   absent; nothing was written in that case. Failure on backing-store
   *   error.
   */
  setIfEqual<T>(
    key: string,
    expected: T,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>>;

  /**
   * Removes the key only if what is stored right now equals `expected`
   * (atomic compare-and-delete).
   *
   * **The comparison is by value**, on the terms {@link setIfEqual} states:
   * a distributed implementation compares the serialized bytes it stores, an
   * in-process implementation compares the stored value structurally over its
   * JSON form, and `expected` must be a type whose equality is by value in
   * every flavor.
   *
   * **An absent key returns `ok(false)`.** The result tells the caller that
   * the entry it was holding is not the entry that is there, which is the
   * same result it gets when somebody else replaced it. In both cases the
   * caller has nothing left to remove.
   *
   * **In the tiered flavor the local tier is dropped on every outcome**:
   * removed, comparison failed and store failure alike, for the reason
   * {@link setIfEqual} gives. When the removal took effect, the key is also
   * published on a backplane that was passed, on the terms {@link setIfEqual}
   * states.
   *
   * Use this member for an owner-scoped release: read the entry, then
   * remove it only while it is still the entry that was read. A read
   * followed by an unconditional remove leaves a window in which the entry
   * a successor wrote is removed by its predecessor.
   *
   * @returns `ok(true)` if the key was removed. `ok(false)` if the stored
   *   value was not `expected`, which includes the key being absent; nothing
   *   was removed in that case. Failure on backing-store error.
   */
  removeIfEqual<T>(
    key: string,
    expected: T,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>>;

  /**
   * Atomically increments a numeric counter and returns the new value.
   * Creates the key if absent (initial value = 0 + amount).
   *
   * **TTL-on-create-only:** the optional `expirationMs` is applied
   * **only when the key is created**; subsequent increments on an
   * existing key **preserve the existing TTL**.
   *
   * @param amount - Amount to add (may be negative). Default `1`.
   * @returns `ok(newValue)` on success. `conflict` if the existing key
   *   holds a non-numeric value (Redis WRONGTYPE parity). Failure on
   *   backing-store error.
   */
  increment(
    key: string,
    amount?: number,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<number>>;

  /**
   * Refills a token bucket by the time elapsed since its last use and then
   * consumes `cost` from it, as one atomic step.
   *
   * **The refill is continuous, so no window boundary lets the allowance be
   * spent twice.** A fixed window admits its whole allowance at the end of one
   * window and again at the start of the next; a bucket that refills by
   * elapsed time admits `burst` at once and then only what the rate has added
   * since.
   *
   * **The store's clock supplies the instant.** A distributed implementation
   * reads the clock of the store every node shares, so two nodes whose own
   * clocks disagree still charge one bucket consistently; an in-process
   * implementation reads its own. None of the elapsed time comes from
   * the caller.
   *
   * **An idle bucket expires.** Its key is given a lifetime no shorter
   * than a refill from empty to full, so a key nobody touches again is dropped
   * rather than kept, and a caller returning after that lifetime finds the
   * full bucket the refill would have given it anyway.
   *
   * **In the tiered flavor this is pure delegation to L2**, as the lock
   * members are: the bucket state belongs to the shared store, and a local
   * copy of it would reflect one node rather than the cluster.
   *
   * @param refillTokens - How many tokens one `refillIntervalMs` adds. The
   *   rate is this over that interval, and the two are passed separately so no
   *   caller has to express a rate as a fraction.
   * @param refillIntervalMs - The span those tokens are added over, in
   *   milliseconds.
   * @param burst - The bucket's capacity: the largest number of tokens it
   *   holds, and so the longest run of requests it admits at once. A bucket is
   *   created full.
   * @param cost - Tokens this request consumes. Default `1`.
   * @returns `ok` with the decision: admitted with a zero wait, or refused
   *   with the wait until the bucket holds `cost`. Validation failure when the
   *   key is falsey, the refill is not positive, the interval is under one
   *   millisecond, the burst is under one, or the cost is under one or over the
   *   burst, because a cost the bucket can never hold would return refused with
   *   a wait that never ends. `conflict` when the key already holds something
   *   that is not bucket state, as {@link increment} returns for a non-numeric
   *   key. Failure on backing-store error.
   */
  consumeToken(
    key: string,
    refillTokens: number,
    refillIntervalMs: number,
    burst: number,
    cost?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<TokenBucketDecision>>;

  /**
   * Attempts to acquire a lock on the given key. The caller-supplied
   * `lockId` identifies the holder and is required for release. Locks
   * expire automatically after `expirationMs` to prevent indefinite
   * hold by a crashed process.
   *
   * @param expirationMs - **Required** lock TTL in milliseconds -
   *   locks must auto-expire (matches .NET `TimeSpan expiration` with
   *   no default).
   * @returns `ok(true)` on acquisition. `ok(false)` if the lock is
   *   held by someone else. Failure on backing-store error.
   */
  acquireLock(
    key: string,
    lockId: string,
    expirationMs: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>>;

  /**
   * Releases a lock previously acquired with {@link acquireLock}.
   * Releasing a lock you don't hold (or one that's already expired) is
   * a no-op rather than an error - release is **idempotent**.
   *
   * @returns `ok`; failure on backing-store error.
   */
  releaseLock(
    key: string,
    lockId: string,
    signal?: AbortSignal,
  ): Promise<D2Result>;
}
