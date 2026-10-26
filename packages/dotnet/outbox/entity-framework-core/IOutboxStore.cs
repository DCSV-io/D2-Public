// -----------------------------------------------------------------------
// <copyright file="IOutboxStore.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox.EntityFrameworkCore;

using DcsvIo.D2.Result;
using NodaTime;

/// <summary>
/// The database seam the relay reads and writes through. It is declared here
/// rather than in the core package because it exists only to be that seam, and
/// its members move messages that already exist.
/// </summary>
/// <typeparam name="TDb">
/// The database this store reads and writes, identified by its application-layer
/// context interface. The relay for a database resolves the store registered
/// for its own <typeparamref name="TDb"/>, so two databases have two stores and
/// one relay cannot receive the other database's rows.
/// </typeparam>
public interface IOutboxStore<TDb>
{
    /// <summary>
    /// Claims the staged messages that are attemptable now, oldest first: it
    /// counts the attempt and pushes the next one out before returning them.
    /// </summary>
    /// <param name="now">The instant to compare next-attempt times against.</param>
    /// <param name="batchSize">The most messages to return.</param>
    /// <param name="lease">
    /// How far ahead the claim pushes each message's next attempt. It governs
    /// only the case where the outcome could not be recorded - a recorded outcome
    /// overwrites it with the message's own backoff, or with nothing at all once
    /// the message is published or abandoned. It must be at least one
    /// millisecond: the claim matches a message whose next attempt is at or
    /// before now, and a PostgreSQL timestamp keeps only microseconds, so a
    /// lease under one microsecond is stored as now and leaves a claimed
    /// message claimable again at once.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok</c> with the claimed batch, which may be empty. Each message has
    /// the attempt count it had before this claim, and the retry decision counts
    /// from that value.
    /// <c>ValidationFailed</c> when <paramref name="batchSize"/> is not positive
    /// or <paramref name="lease"/> is shorter than one millisecond.
    /// <c>Conflict</c> when another claimant wrote a row of the batch between
    /// this call's read and its claim - that claimant counted the attempt and
    /// owns the dispatch, and nothing is returned here.
    /// <c>ServiceUnavailable</c> when the database could not respond or the claim
    /// could not be written for any other reason.
    /// On <c>Conflict</c> and on <c>ServiceUnavailable</c> nothing is claimed
    /// and nothing is returned, so no message is ever dispatched without its
    /// attempt being counted first.
    /// </returns>
    ValueTask<D2Result<IReadOnlyList<StagedOutboxMessage>>> ClaimDueAsync(
        Instant now, int batchSize, Duration lease, CancellationToken ct = default);

    /// <summary>
    /// Persists the state a message moved to, and commits it.
    /// </summary>
    /// <param name="message">The message in its new state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok</c> when the state is durable. <c>NotFound</c> when the row is
    /// gone. <c>Conflict</c> when another writer changed the row first.
    /// <c>ServiceUnavailable</c> when the database could not be written.
    /// </returns>
    ValueTask<D2Result> RecordAsync(OutboxMessage message, CancellationToken ct = default);

    /// <summary>
    /// Deletes the settled messages that are past their retention window:
    /// published rows confirmed before <paramref name="publishedBefore"/> and
    /// abandoned rows whose last attempt failed before
    /// <paramref name="abandonedBefore"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A staged message is never deleted here, whatever its age: it still
    /// awaits delivery, and the retry curve rather than the retention window
    /// decides when it is abandoned.
    /// </para>
    /// <para>
    /// The delete is bounded by <paramref name="batchSize"/>, so one call
    /// deletes at most that many rows. A backlog is cleared over as many ticks
    /// as it takes.
    /// </para>
    /// </remarks>
    /// <param name="publishedBefore">
    /// Published rows confirmed before this instant are past their window.
    /// </param>
    /// <param name="abandonedBefore">
    /// Abandoned rows whose last attempt failed before this instant are past
    /// theirs.
    /// </param>
    /// <param name="batchSize">The most rows one call deletes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok</c> with how many rows were deleted, which may be zero.
    /// <c>ValidationFailed</c> when <paramref name="batchSize"/> is not positive.
    /// <c>ServiceUnavailable</c> when the database could not be written, in which
    /// case nothing was deleted and the next tick tries again.
    /// </returns>
    ValueTask<D2Result<int>> PruneSettledAsync(
        Instant publishedBefore,
        Instant abandonedBefore,
        int batchSize,
        CancellationToken ct = default);
}
