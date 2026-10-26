// -----------------------------------------------------------------------
// <copyright file="IMessageOutbox.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox;

using DcsvIo.D2.Result;

/// <summary>
/// Stages an outward announcement in the same unit of work as the change, so
/// the change and the pending announcement become durable together or not at
/// all.
/// </summary>
/// <typeparam name="TDb">
/// The database this outbox belongs to, identified by its application-layer
/// context interface. An outbox is per database rather than per host, so a host
/// composing two modules over two databases resolves two of these and the
/// compiler distinguishes them. The parameter is unconstrained because this
/// package binds no data-access library and so cannot reference the base type
/// a consumer's context derives from.
/// </typeparam>
public interface IMessageOutbox<TDb>
{
    /// <summary>
    /// Joins the caller's unit of work. Stages the message and commits nothing,
    /// so a rollback discards the staged message together with the change and a
    /// commit makes both durable in one transaction.
    /// </summary>
    /// <typeparam name="TKind">
    /// The consumer's own closed kind enum. The kind is generic at the call site
    /// and a string at rest: a raw string everywhere would lose the closed
    /// set, and a shared enum would put product-specific names in a portable
    /// package.
    /// </typeparam>
    /// <typeparam name="TPayload">The payload's own type.</typeparam>
    /// <param name="kind">Which act this message announces.</param>
    /// <param name="payload">
    /// What to send. Serialized here rather than by the caller, so no call site
    /// invents a payload format.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok</c> when the message joined the unit of work.
    /// <c>ValidationFailed</c> when the kind is not a defined member of
    /// <typeparamref name="TKind"/>, the payload carries a value marked personal,
    /// or the payload cannot be serialized.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The staging call belongs above the caller's save. After the save, the row
    /// is added to the change tracker and is either committed by an unrelated
    /// later save or lost silently, and nothing fails.
    /// </para>
    /// <para>
    /// <b>The caller must meet one precondition:</b> <typeparamref name="TPayload"/>
    /// contains no personal data. It refers to its subjects by identifier and
    /// lets the dispatcher read the value at delivery, or it seals the value
    /// before staging. The staged payload is stored as plaintext on a row that
    /// the anonymization engine skips, so a personal value staged here survives
    /// that subject's erasure for as long as the storage half's retention window
    /// keeps the row - up to 90 days at the shipped default.
    /// The storage half returns <c>ValidationFailed</c> for a payload that
    /// has a <c>[RedactData]</c> marker anywhere its serializer reaches,
    /// including a value held in a member declared as <see cref="object"/>.
    /// It reads markers and not values, so a personal value on an unmarked
    /// member still passes, and no model validator or erasure sweep reports it.
    /// </para>
    /// </remarks>
    ValueTask<D2Result> StageAsync<TKind, TPayload>(
        TKind kind,
        TPayload payload,
        CancellationToken ct)
        where TKind : struct, Enum
        where TPayload : class;

    /// <summary>
    /// Stages a message under an identity the caller minted, for a payload that
    /// has to contain the id of the row that holds it.
    /// </summary>
    /// <remarks>
    /// Same unit of work, same failure results and the same no-personal-data
    /// precondition on the caller as the minting overload. It exists because
    /// a payload that contains its own row id cannot be built after the row: an
    /// acknowledgment has to state which message it acknowledges, and the only
    /// identifier both sides can agree on is the one the row already has.
    /// </remarks>
    /// <typeparam name="TKind">The consumer's own closed kind enum.</typeparam>
    /// <typeparam name="TPayload">The payload's own type.</typeparam>
    /// <param name="kind">Which act this message announces.</param>
    /// <param name="id">
    /// The identity to stage under, from <see cref="OutboxMessageId.New"/>. The
    /// caller mints it so the payload can carry it.
    /// </param>
    /// <param name="payload">What to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok</c> when the message joined the unit of work.
    /// <c>ValidationFailed</c> when the kind is not a defined member of
    /// <typeparamref name="TKind"/>, the identity is the default value, the
    /// payload carries a value marked personal, or the payload cannot be
    /// serialized.
    /// </returns>
    ValueTask<D2Result> StageAsync<TKind, TPayload>(
        TKind kind,
        OutboxMessageId id,
        TPayload payload,
        CancellationToken ct = default)
        where TKind : struct, Enum
        where TPayload : class;
}
