// -----------------------------------------------------------------------
// <copyright file="ICacheAtomic.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching;

using DcsvIo.D2.Result;

/// <summary>
/// Atomic primitives - set-if-absent, compare-and-set, compare-and-delete,
/// increment, token-bucket consume, lock acquire/release.
/// Implemented by every cache flavor (<see cref="ILocalCache"/>,
/// <see cref="IDistributedCache"/>, <see cref="ITieredCache"/>); the
/// atomicity scope depends on the implementing cache.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope per implementation:</b>
/// </para>
/// <list type="bullet">
/// <item>
/// <see cref="ILocalCache"/>: per-process. Atomicity guaranteed within
/// this instance only.
/// </item>
/// <item>
/// <see cref="IDistributedCache"/>: cluster-wide. Atomicity enforced by
/// the remote store (e.g. Redis SETNX / INCR).
/// </item>
/// <item>
/// <see cref="ITieredCache"/>: cluster-wide. Routes through L2 (the
/// source of truth); L1 is invalidated or refreshed as a side effect.
/// </item>
/// </list>
/// <para>
/// Callers pick the scope by injecting the appropriate marker interface.
/// The same atomic op called via <c>ILocalCache</c> coordinates within one
/// process; via <c>IDistributedCache</c> or <c>ITieredCache</c> it
/// coordinates across the cluster.
/// </para>
/// </remarks>
public interface ICacheAtomic
{
    /// <summary>
    /// Sets a value only if the key is not already present (atomic).
    /// </summary>
    /// <typeparam name="T">Stored value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to store.</param>
    /// <param name="expiration">Optional TTL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok(true)</c> if the value was written. <c>Ok(false)</c> if the key
    /// already existed (no write). Failure on backing-store error.
    /// </returns>
    ValueTask<D2Result<bool>> SetNxAsync<T>(
        string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);

    /// <summary>
    /// Replaces the stored value only if what is stored right now equals
    /// <paramref name="expected"/> (atomic compare-and-set).
    /// </summary>
    /// <typeparam name="T">Stored value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="expected">The value the comparison expects to find stored.</param>
    /// <param name="value">The value to store when the comparison holds.</param>
    /// <param name="expiration">
    /// Optional TTL. A null expiration uses the store's default expiration,
    /// matching <see cref="SetNxAsync{T}"/> rather than
    /// <see cref="IncrementAsync"/> - this member replaces a value rather than
    /// accumulating onto one, so it follows the replacing sibling. A
    /// non-positive lifetime is an input failure on
    /// <c>expiration</c> in every flavor, whether the caller passed it or the
    /// store default resolved to it; there is no way to ask for an entry that
    /// never expires.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok(true)</c> if the value was written. <c>Ok(false)</c> if the stored
    /// value was not <paramref name="expected"/>, which includes the key being
    /// absent; nothing was written in that case. Failure on backing-store error.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>The comparison is by value.</b> A distributed implementation compares
    /// the serialized bytes it stores; an in-process implementation compares the
    /// stored object with the runtime's default value equality. So
    /// <paramref name="expected"/> must be a type whose equality is by value in
    /// every flavor - a record of primitives satisfies this, an array or a class
    /// without value equality does not, and for those the answers differ per
    /// flavor and nothing reports the difference. The requirement is stated rather
    /// than declared as a type constraint because the TypeScript twin has no
    /// mirror for one, and a constraint enforced on one runtime only makes the
    /// two contracts differ.
    /// </para>
    /// <para>
    /// <b>An absent key returns <c>Ok(false)</c>.</b> Once the key is gone, there
    /// is no stored value to compare the expected value against, and writing
    /// anyway would overwrite the value that the next reader loads in its place.
    /// </para>
    /// <para>
    /// <b>In the tiered flavor the local tier is dropped on every outcome</b> -
    /// written, comparison lost and store failure alike. The comparison holds
    /// for the shared value at the instant it ran and says nothing about a local
    /// copy a moment later, so the local copy is dropped.
    /// </para>
    /// <para>
    /// <b>The tiered flavor also invalidates the other instances on the arm
    /// where the write succeeded</b>, where an
    /// <see cref="ICacheInvalidationBackplane"/> is registered. That arm
    /// replaced the shared value, so every other instance's local copy is the
    /// value this write superseded. The arm where the comparison lost changed
    /// nothing at the shared store and publishes nothing. A publish that fails
    /// is logged and does not change the result: the other instances then serve
    /// their local copy until it expires. With no backplane registered there is
    /// nothing to publish on, and a key held in more than one local tier goes
    /// stale until TTL.
    /// </para>
    /// </remarks>
    ValueTask<D2Result<bool>> SetIfEqualAsync<T>(
        string key,
        T expected,
        T value,
        TimeSpan? expiration = null,
        CancellationToken ct = default);

    /// <summary>
    /// Removes the key only if what is stored right now equals
    /// <paramref name="expected"/> (atomic compare-and-delete).
    /// </summary>
    /// <typeparam name="T">Stored value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="expected">The value the comparison expects to find stored.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok(true)</c> if the key was removed. <c>Ok(false)</c> if the stored
    /// value was not <paramref name="expected"/>, which includes the key being
    /// absent; nothing was removed in that case. Failure on backing-store error.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>The comparison is by value</b>, on the terms
    /// <see cref="SetIfEqualAsync{T}"/> states: a distributed implementation
    /// compares the serialized bytes it stores, an in-process implementation
    /// compares the stored object with the runtime's default value equality, and
    /// <paramref name="expected"/> must be a type whose equality is by value in
    /// every flavor.
    /// </para>
    /// <para>
    /// <b>An absent key returns <c>Ok(false)</c>.</b> The result states that the
    /// expected entry is not the entry that is there, the same result as when
    /// another writer replaced it. In both cases nothing remains to remove.
    /// </para>
    /// <para>
    /// <b>In the tiered flavor the local tier is dropped on every outcome</b> -
    /// removed, comparison lost and store failure alike, for the reason
    /// <see cref="SetIfEqualAsync{T}"/> gives. The arm where the removal took
    /// also publishes the key on a registered
    /// <see cref="ICacheInvalidationBackplane"/>, on the terms
    /// <see cref="SetIfEqualAsync{T}"/> states.
    /// </para>
    /// <para>
    /// Use this member for an owner-scoped release: read the entry, then remove
    /// it only while it is still the entry that was read. A read followed by an
    /// unconditional remove leaves a window in which the entry a successor wrote
    /// is removed by its predecessor.
    /// </para>
    /// </remarks>
    ValueTask<D2Result<bool>> RemoveIfEqualAsync<T>(
        string key,
        T expected,
        CancellationToken ct = default);

    /// <summary>
    /// Atomically increments a numeric counter and returns the new value.
    /// Creates the key if absent (initial value = 0 + amount). The optional
    /// expiration is applied only when the key is created - existing keys
    /// keep their TTL.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="amount">Amount to add (may be negative).</param>
    /// <param name="expiration">TTL for newly-created keys.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok(newValue)</c> on success. <c>Conflict</c> if the existing key
    /// holds a non-numeric value (Redis WRONGTYPE parity). Failure on
    /// backing-store error.
    /// </returns>
    ValueTask<D2Result<long>> IncrementAsync(
        string key, long amount = 1, TimeSpan? expiration = null, CancellationToken ct = default);

    /// <summary>
    /// Refills a token bucket by the time elapsed since its last use and then
    /// consumes <paramref name="cost"/> from it, as one atomic step.
    /// </summary>
    /// <param name="key">The bucket key.</param>
    /// <param name="refillTokens">
    /// How many tokens one <paramref name="refillInterval"/> adds. The rate is
    /// this over that interval, and the two are passed apart so a rate never has
    /// to be written as a fraction.
    /// </param>
    /// <param name="refillInterval">The span <paramref name="refillTokens"/> is measured over.</param>
    /// <param name="burst">
    /// The bucket's capacity - the largest number of tokens it holds, and so the
    /// longest run of requests it admits at once. A bucket is created full.
    /// </param>
    /// <param name="cost">Tokens this request consumes. Default 1.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok</c> with the decision: admitted with a zero wait, or refused
    /// with the wait until the bucket holds <paramref name="cost"/>. Validation
    /// failure when the key is falsey, the refill is not positive, the interval
    /// is under one millisecond, the burst is under one, or the cost is under
    /// one or over the burst - a cost the bucket can never hold would be
    /// refused with a wait that never ends. <c>Conflict</c> when the key already
    /// holds something that is not bucket state, as
    /// <see cref="IncrementAsync"/> returns for a non-numeric key. Failure on
    /// backing-store error.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>The refill is continuous, so there is no window boundary at which the
    /// allowance can be spent twice.</b> A fixed window admits its whole allowance at the end of
    /// one window and again at the start of the next; a bucket that refills by
    /// elapsed time admits <paramref name="burst"/> at once and then only what
    /// the rate has added since.
    /// </para>
    /// <para>
    /// <b>The store's clock supplies the instant.</b> A distributed
    /// implementation reads the clock of the store every node shares, so two
    /// nodes whose own clocks disagree still charge one bucket consistently; an
    /// in-process implementation reads its own. Nothing about the elapsed time
    /// comes from the caller.
    /// </para>
    /// <para>
    /// <b>A bucket left alone expires.</b> Its key is given a lifetime no shorter
    /// than a refill from empty to full, so a key nobody touches again is dropped
    /// rather than kept, and a caller returning after that lifetime finds the full
    /// bucket the refill would have given it anyway.
    /// </para>
    /// <para>
    /// <b>In the tiered flavor this is pure delegation to L2</b>, as the lock
    /// members are: the bucket state belongs to the shared store and a local copy
    /// of it would describe one node rather than the cluster.
    /// </para>
    /// </remarks>
    ValueTask<D2Result<TokenBucketDecision>> ConsumeTokenAsync(
        string key,
        long refillTokens,
        TimeSpan refillInterval,
        long burst,
        long cost = 1,
        CancellationToken ct = default);

    /// <summary>
    /// Attempts to acquire a lock on the given key. The caller-supplied
    /// <paramref name="lockId"/> identifies the holder and is required for
    /// release. Locks expire automatically after <paramref name="expiration"/>
    /// to prevent indefinite hold by a crashed process.
    /// </summary>
    /// <param name="key">Lock key.</param>
    /// <param name="lockId">Caller-supplied identifier; needed for release.</param>
    /// <param name="expiration">Lock TTL - auto-released after this elapses.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok(true)</c> on acquisition. <c>Ok(false)</c> if the lock is held
    /// by someone else. Failure on backing-store error.
    /// </returns>
    ValueTask<D2Result<bool>> AcquireLockAsync(
        string key, string lockId, TimeSpan expiration, CancellationToken ct = default);

    /// <summary>
    /// Releases a lock previously acquired with <see cref="AcquireLockAsync"/>.
    /// Releasing a lock you don't hold (or one that's already expired) is
    /// a no-op rather than an error - release is idempotent.
    /// </summary>
    /// <param name="key">Lock key.</param>
    /// <param name="lockId">The same identifier used at acquire time.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>Ok</c>; failure on backing-store error.</returns>
    ValueTask<D2Result> ReleaseLockAsync(
        string key, string lockId, CancellationToken ct = default);
}
