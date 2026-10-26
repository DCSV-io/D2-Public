// -----------------------------------------------------------------------
// <copyright file="PublishedOutboxMessage.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox;

using NodaTime;

/// <summary>
/// A message a dispatcher confirmed. Delivery is at-least-once, so a consumer
/// that sees the same message twice must be idempotent.
/// </summary>
public sealed record PublishedOutboxMessage : OutboxMessage
{
    /// <summary>
    /// Gets the instant the dispatcher confirmed. Category 1. The retention
    /// sweep also prunes a published row by this value.
    /// </summary>
    public required Instant PublishedAt { get; init; }

    /// <inheritdoc/>
    public override OutboxMessageStatus Status => OutboxMessageStatus.Published;
}
