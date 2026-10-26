// -----------------------------------------------------------------------
// <copyright file="OutboxMessageRow.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox.EntityFrameworkCore;

using DcsvIo.D2.DataGovernance.Abstractions;
using NodaTime;

/// <summary>
/// Flat EF row for a staged message (plural table <c>outbox_messages</c>). It
/// has the union of the per-state columns and a status discriminator; no
/// domain type appears on it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The row is exempt from the anonymization sweep, and the reason is a
/// prohibition on what may be staged rather than a claim about the sweep.</b>
/// A payload refers to its subjects by identifier and contains no personal
/// data, no secret, no code and no hash - see <see cref="Payload"/> - so an
/// erasure that reaches every other row in the database of this table leaves
/// nothing personal here to reach. An erasure sweep cannot rewrite the payload
/// anyway: it is one opaque JSON document to the engine, whose rules address
/// columns.
/// </para>
/// <para>
/// <b>The row's lifetime is bounded by the relay's retention sweep</b> rather
/// than by the erasure sweep. A published or abandoned row is deleted once it is
/// older than the window its status sets
/// (<c>OutboxOptions.PublishedRetention</c> and
/// <c>OutboxOptions.AbandonedRetention</c>), so the table does not accumulate
/// what was already delivered or already given up on. That deletion is
/// retention rather than erasure: it uses only the age of a settled row and
/// never looks at subjects, and it is not a route by which one subject's data is
/// removed on request.
/// </para>
/// </remarks>
public sealed class OutboxMessageRow : IExemptFromAnonymization
{
    /// <summary>Gets or sets the primary key. Version 7.</summary>
    public required Guid Id { get; set; }

    /// <summary>Gets or sets the consumer enum member's name.</summary>
    public required string Kind { get; set; }

    /// <summary>
    /// Gets or sets the serialized payload, stored as <c>jsonb</c>.
    /// </summary>
    /// <remarks>
    /// <b>A payload refers to its subjects by identifier and contains no
    /// personal data, no secret, no code and no hash.</b> It is stored as it
    /// was staged: this package does not encrypt it, no erasure sweep rewrites it, and it
    /// stays in the database of this table until the retention sweep deletes the
    /// row. An announcement that needs a personal value stages the identifier,
    /// and the dispatcher reads the value at delivery, or the value is sealed
    /// before it is staged.
    /// <para>
    /// <b>Staging rejects a payload type with a marker; nothing reads values.</b>
    /// The row is <see cref="IExemptFromAnonymization"/>, so
    /// <c>AnonymizationModelValidator</c> does not inspect it, and a personal
    /// value in this column survives for up to
    /// <c>OutboxOptions.AbandonedRetention</c> past the subject's erasure.
    /// <see cref="EfMessageOutbox{TDb, TDbContext}"/> returns
    /// <c>ValidationFailed</c> before the row is built when the payload contains
    /// a <c>[RedactData]</c> marker anywhere the serializer reaches, including a
    /// value held in a member declared as <see cref="object"/>. A
    /// personal value on an unmarked member still passes, so staging does not
    /// establish that the payload contains no personal value, and that
    /// precondition is met before the staging call.
    /// </para>
    /// </remarks>
    [KeepOnAnonymization(
        "The payload names subjects by identifier and carries no personal data, "
        + "so an erasure has nothing here to overwrite. The retention sweep "
        + "bounds how long the row itself lives.")]
    public required string Payload { get; set; }

    /// <summary>Gets or sets the instant the message was staged. Category 1.</summary>
    public required Instant StagedAt { get; set; }

    /// <summary>Gets or sets the state this row is in.</summary>
    public required OutboxMessageStatus Status { get; set; }

    /// <summary>Gets or sets how many delivery attempts have been made.</summary>
    public required int Attempts { get; set; }

    /// <summary>
    /// Gets or sets the earliest instant the relay may try again, which is what
    /// the relay's claim predicate reads. Category 2.
    /// </summary>
    public Instant? NextAttemptAt { get; set; }

    /// <summary>
    /// Gets or sets the instant the dispatcher confirmed, which is also what the
    /// retention sweep prunes a published row by. Category 1.
    /// </summary>
    public Instant? PublishedAt { get; set; }

    /// <summary>Gets or sets the instant the last attempt failed. Category 1.</summary>
    public Instant? AbandonedAt { get; set; }

    /// <summary>Gets or sets what went wrong on the last attempt, in one code.</summary>
    public string? LastFailureCode { get; set; }

    /// <summary>
    /// Gets or sets the PostgreSQL <c>xmin</c> concurrency token. Two relay
    /// instances never claim the same batch. The token enforces that on the row
    /// itself rather than relying on the lock alone.
    /// </summary>
    public uint Version { get; set; }
}
