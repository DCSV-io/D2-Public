// -----------------------------------------------------------------------
// <copyright file="StagedOutboxMessage.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox;

using NodaTime;

/// <summary>
/// A message that is durable and not yet delivered. This is the only state the
/// relay claims from.
/// </summary>
public sealed record StagedOutboxMessage : OutboxMessage
{
    /// <summary>
    /// Gets the earliest instant the relay may attempt this message again.
    /// Null means it is attemptable now.
    /// </summary>
    public Instant? NextAttemptAt { get; init; }

    /// <summary>
    /// Gets what went wrong on the attempt that put this message back into the
    /// staged state, in one code. Null on a message that has not been attempted.
    /// </summary>
    /// <remarks>
    /// It is recorded on every failed attempt rather than only on the last one.
    /// An operator querying the table during an outage sees an attempt count and
    /// a next-attempt instant. The code distinguishes a deployment missing a
    /// dispatcher from a transport that is down, while the message is still
    /// retrying.
    /// </remarks>
    public string? LastFailureCode { get; init; }

    /// <inheritdoc/>
    public override OutboxMessageStatus Status => OutboxMessageStatus.Staged;

    /// <summary>
    /// Records a confirmed delivery.
    /// </summary>
    /// <param name="at">The instant the dispatcher confirmed. Category 1.</param>
    /// <returns>The published state of this message.</returns>
    public PublishedOutboxMessage Publish(Instant at) => new()
    {
        Id = Id,
        Kind = Kind,
        Payload = Payload,
        StagedAt = StagedAt,
        Attempts = Attempts + 1,
        PublishedAt = at,
    };

    /// <summary>
    /// Records a failed delivery attempt, and decides whether that attempt was
    /// the last one.
    /// </summary>
    /// <param name="at">The instant the attempt failed. Category 1.</param>
    /// <param name="failureCode">What went wrong, in one code.</param>
    /// <param name="limit">
    /// The total number of attempts this message is allowed. The waits are one
    /// fewer than the limit, because the limit counts attempts.
    /// </param>
    /// <param name="backoff">
    /// How long to wait before the next attempt. Computed from the clock and
    /// the options, which this record does not hold.
    /// </param>
    /// <returns>
    /// A <see cref="StagedOutboxMessage"/> while attempts remain, and an
    /// <see cref="AbandonedOutboxMessage"/> at the limit. The base type is
    /// returned because this method checks the limit and decides which leaf it
    /// produces, so the limit check is made in this method on every failure.
    /// </returns>
    public OutboxMessage Fail(Instant at, string failureCode, int limit, Duration backoff)
    {
        var attempts = Attempts + 1;

        if (attempts < limit)
        {
            return this with
            {
                Attempts = attempts,
                NextAttemptAt = at + backoff,
                LastFailureCode = failureCode,
            };
        }

        return new AbandonedOutboxMessage
        {
            Id = Id,
            Kind = Kind,
            Payload = Payload,
            StagedAt = StagedAt,
            Attempts = attempts,
            AbandonedAt = at,
            LastFailureCode = failureCode,
        };
    }
}
