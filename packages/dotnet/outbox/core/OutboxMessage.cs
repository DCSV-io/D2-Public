// -----------------------------------------------------------------------
// <copyright file="OutboxMessage.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox;

using NodaTime;

/// <summary>
/// A message staged in the same transaction as the change that produced it.
/// The base carries what every state carries; each sealed leaf carries what only
/// that state has.
/// </summary>
/// <remarks>
/// The payload crosses no boundary until a dispatcher sends it, and it is
/// written as text by the staging seam rather than by the caller - so the
/// serializer options are part of the persisted contract, and rows written by
/// one version stay readable by the next.
/// </remarks>
public abstract record OutboxMessage
{
    /// <summary>Gets the message identity.</summary>
    public required OutboxMessageId Id { get; init; }

    /// <summary>
    /// Gets the stored name of the consumer's own kind, which is what selects
    /// the dispatcher that sends it.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Gets the serialized payload. Stored as <c>jsonb</c>; carries no secret,
    /// no code and no hash.
    /// </summary>
    public required string Payload { get; init; }

    /// <summary>Gets the instant the message was staged. Category 1.</summary>
    public required Instant StagedAt { get; init; }

    /// <summary>Gets how many delivery attempts have already been made.</summary>
    public required int Attempts { get; init; }

    /// <summary>Gets the persistence discriminator for this state.</summary>
    public abstract OutboxMessageStatus Status { get; }
}
