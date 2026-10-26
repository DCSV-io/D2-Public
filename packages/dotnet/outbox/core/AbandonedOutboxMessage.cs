// -----------------------------------------------------------------------
// <copyright file="AbandonedOutboxMessage.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox;

using NodaTime;

/// <summary>
/// A message that ran out of attempts. Abandonment is escalation rather than
/// loss: the row keeps its payload and its last failure code, and an operator
/// is alerted, so what was not delivered can be seen and re-staged.
/// </summary>
/// <remarks>
/// The bound exists so a permanently poisoned message stops consuming relay
/// ticks forever.
/// </remarks>
public sealed record AbandonedOutboxMessage : OutboxMessage
{
    /// <summary>Gets the instant the last attempt failed. Category 1.</summary>
    public required Instant AbandonedAt { get; init; }

    /// <summary>Gets what went wrong on that last attempt, in one code.</summary>
    public required string LastFailureCode { get; init; }

    /// <inheritdoc/>
    public override OutboxMessageStatus Status => OutboxMessageStatus.Abandoned;
}
