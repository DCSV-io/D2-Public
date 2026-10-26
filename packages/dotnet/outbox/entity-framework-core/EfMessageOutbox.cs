// -----------------------------------------------------------------------
// <copyright file="EfMessageOutbox.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox.EntityFrameworkCore;

using System.Text.Json;
using DcsvIo.D2.Result;
using DcsvIo.D2.Time;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// The staging seam over a <typeparamref name="TDbContext"/>. It adds a row to
/// the unit of work of that context and commits nothing, so the change and the
/// pending announcement become durable together or not at all.
/// </summary>
/// <typeparam name="TDb">
/// The database this seam stages onto, identified by its application-layer
/// context interface.
/// </typeparam>
/// <typeparam name="TDbContext">
/// The concrete context, which holds the table. It must implement
/// <typeparamref name="TDb"/>, so a context can only be bound under an
/// interface it actually is.
/// </typeparam>
/// <param name="db">The context whose unit of work receives the staged row.</param>
/// <param name="clock">The clock the staged instant is read from.</param>
public sealed class EfMessageOutbox<TDb, TDbContext>(TDbContext db, IClock clock)
    : IMessageOutbox<TDb>
    where TDbContext : DbContext, TDb
{
    /// <inheritdoc/>
    public ValueTask<D2Result> StageAsync<TKind, TPayload>(
        TKind kind,
        TPayload payload,
        CancellationToken ct)
        where TKind : struct, Enum
        where TPayload : class
        => StageAsync(kind, OutboxMessageId.New(), payload, ct);

    /// <inheritdoc/>
    public ValueTask<D2Result> StageAsync<TKind, TPayload>(
        TKind kind,
        OutboxMessageId id,
        TPayload payload,
        CancellationToken ct = default)
        where TKind : struct, Enum
        where TPayload : class
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (!Enum.IsDefined(kind))
            return ValueTask.FromResult(D2Result.ValidationFailed());

        // The default identity is not a row anything can refer back to, and the
        // caller minted this one precisely so the payload could contain it.
        if (id.Value.Falsey())
            return ValueTask.FromResult(D2Result.ValidationFailed());

        string text;

        try
        {
            text = JsonSerializer.Serialize(payload, OutboxPayloadSerializer.Options);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // A payload the serializer refuses is a caller mistake rather than an
            // outage, and it must not reach the table: the relay would read it
            // back, fail every attempt, and abandon it an hour later.
            return ValueTask.FromResult(D2Result.ValidationFailed());
        }

        // The row is exempt from anonymization, so a personal value staged here
        // would outlive its subject's erasure for the whole retention window. A
        // payload that contains one is refused before it reaches the row. The
        // check runs after the serializer accepted the payload, so the metadata
        // it reads belongs to a payload the serializer can write.
        if (OutboxPayloadPersonalDataCheck.CarriesPersonalData(payload))
            return ValueTask.FromResult(D2Result.ValidationFailed());

        var staged = new StagedOutboxMessage
        {
            Id = id,
            Kind = kind.ToString(),
            Payload = text,
            StagedAt = clock.GetCurrentInstant(),
            Attempts = 0,
        };

        db.Set<OutboxMessageRow>().Add(staged.ToNewRow());

        return ValueTask.FromResult(D2Result.Ok());
    }
}
