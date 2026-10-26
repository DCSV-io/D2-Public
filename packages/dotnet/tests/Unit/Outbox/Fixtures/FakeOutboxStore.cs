// -----------------------------------------------------------------------
// <copyright file="FakeOutboxStore.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using DcsvIo.D2.Outbox;
using DcsvIo.D2.Outbox.EntityFrameworkCore;
using DcsvIo.D2.Result;
using NodaTime;

/// <summary>
/// An in-memory <see cref="IOutboxStore{TDb}"/> that behaves like the real one: it
/// returns only messages whose next attempt is due, and it keeps the state the
/// relay records so a multi-tick test follows the same message across ticks.
/// </summary>
internal sealed class FakeOutboxStore : IOutboxStore<IOutboxProbeDbFixture>
{
    private readonly Dictionary<Guid, OutboxMessage> r_byId = [];

    /// <summary>Gets the instants each claim was asked to compare against.</summary>
    public List<Instant> ClaimedAt { get; } = [];

    /// <summary>Gets the batch sizes each claim was asked for.</summary>
    public List<int> ClaimedBatchSizes { get; } = [];

    /// <summary>Gets the lease each claim was asked to stamp.</summary>
    public List<Duration> ClaimedLeases { get; } = [];

    /// <summary>Gets the states recorded, in order.</summary>
    public List<OutboxMessage> Recorded { get; } = [];

    /// <summary>
    /// Gets or sets a failure the claim returns instead of a batch.
    /// </summary>
    public D2Result<IReadOnlyList<StagedOutboxMessage>>? ClaimAnswer { get; set; }

    /// <summary>Gets or sets a failure every record returns.</summary>
    public D2Result? RecordAnswer { get; set; }

    /// <summary>Gets the windows each retention sweep was asked for.</summary>
    public List<(Instant PublishedBefore, Instant AbandonedBefore, int BatchSize)> Pruned { get; }
        = [];

    /// <summary>
    /// Gets or sets what the retention sweep returns instead of deleting.
    /// </summary>
    public D2Result<int>? PruneAnswer { get; set; }

    /// <summary>Gets the current state of a message this store holds.</summary>
    /// <param name="id">The message identity.</param>
    /// <returns>The message.</returns>
    public OutboxMessage this[OutboxMessageId id] => r_byId[id.Value];

    /// <summary>Seeds a message into the store.</summary>
    /// <param name="message">The staged message.</param>
    public void Seed(StagedOutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        r_byId[message.Id.Value] = message;
    }

    /// <inheritdoc/>
    public ValueTask<D2Result<IReadOnlyList<StagedOutboxMessage>>> ClaimDueAsync(
        Instant now, int batchSize, Duration lease, CancellationToken ct = default)
    {
        ClaimedAt.Add(now);
        ClaimedBatchSizes.Add(batchSize);
        ClaimedLeases.Add(lease);

        if (ClaimAnswer is { } canned)
            return ValueTask.FromResult(canned);

        IReadOnlyList<StagedOutboxMessage> due =
        [
            .. r_byId.Values
                .OfType<StagedOutboxMessage>()
                .Where(m => m.NextAttemptAt is not { } next || next <= now)
                .OrderBy(m => m.StagedAt)
                .Take(batchSize),
        ];

        // The claim is stamped here exactly as the real store stamps it: the
        // attempt is counted and the next attempt pushed out before the message
        // is handed over, and the message handed over has the count it had
        // before the claim. A double that returned the batch without stamping would hide
        // the arm this store exists to reproduce - the one where the outcome is
        // never recorded.
        foreach (var claimed in due)
        {
            r_byId[claimed.Id.Value] = claimed with
            {
                Attempts = claimed.Attempts + 1,
                NextAttemptAt = now + lease,
            };
        }

        return ValueTask.FromResult(
            D2Result<IReadOnlyList<StagedOutboxMessage>>.Ok(due));
    }

    /// <inheritdoc/>
    public ValueTask<D2Result> RecordAsync(
        OutboxMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        Recorded.Add(message);

        if (RecordAnswer is { } canned)
            return ValueTask.FromResult(canned);

        r_byId[message.Id.Value] = message;

        return ValueTask.FromResult(D2Result.Ok());
    }

    /// <inheritdoc/>
    public ValueTask<D2Result<int>> PruneSettledAsync(
        Instant publishedBefore,
        Instant abandonedBefore,
        int batchSize,
        CancellationToken ct = default)
    {
        Pruned.Add((publishedBefore, abandonedBefore, batchSize));

        if (PruneAnswer is { } canned)
            return ValueTask.FromResult(canned);

        var due = r_byId.Values
            .Where(m => m switch
            {
                PublishedOutboxMessage published => published.PublishedAt < publishedBefore,
                AbandonedOutboxMessage abandoned => abandoned.AbandonedAt < abandonedBefore,
                _ => false,
            })
            .Take(batchSize)
            .Select(m => m.Id.Value)
            .ToList();

        foreach (var id in due)
            r_byId.Remove(id);

        return ValueTask.FromResult(D2Result<int>.Ok(due.Count));
    }
}
