// -----------------------------------------------------------------------
// <copyright file="EfOutboxStore.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox.EntityFrameworkCore;

using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using NodaTime;

/// <summary>
/// The <see cref="IOutboxStore{TDb}"/> over the consumer's own context.
/// </summary>
/// <typeparam name="TDb">
/// The database this store belongs to, identified by its application-layer
/// context interface.
/// </typeparam>
/// <typeparam name="TDbContext">
/// The consumer's context, which holds the table. It must implement
/// <typeparamref name="TDb"/>, so a context can only be bound under an
/// interface it actually is.
/// </typeparam>
/// <param name="db">The consumer's context.</param>
public sealed class EfOutboxStore<TDb, TDbContext>(TDbContext db) : IOutboxStore<TDb>
    where TDbContext : DbContext, TDb
{
    // A PostgreSQL timestamp keeps microseconds, so a lease under one
    // microsecond is stored as now. The floor is the minimum that
    // OutboxOptions.RetryBackoffCeiling enforces.
    private const long _MINIMUM_LEASE_MILLISECONDS = 1;

    /// <inheritdoc/>
    public async ValueTask<D2Result<IReadOnlyList<StagedOutboxMessage>>> ClaimDueAsync(
        Instant now, int batchSize, Duration lease, CancellationToken ct = default)
    {
        if (batchSize <= 0 || lease < Duration.FromMilliseconds(_MINIMUM_LEASE_MILLISECONDS))
            return D2Result<IReadOnlyList<StagedOutboxMessage>>.ValidationFailed();

        try
        {
            var rows = await db.Set<OutboxMessageRow>()
                .Where(m => m.Status == OutboxMessageStatus.Staged
                    && (m.NextAttemptAt == null || m.NextAttemptAt <= now))
                .OrderBy(m => m.StagedAt)
                .Take(batchSize)
                .ToListAsync(ct);

            // Read into the domain before the claim is stamped, so each message
            // has the attempt count it had going in. The retry decision
            // counts from that value and the claim has already written the count
            // it will reach, so the two agree whether or not the outcome is
            // recorded afterwards.
            IReadOnlyList<StagedOutboxMessage> claimed =
                [.. rows.Select(r => (StagedOutboxMessage)r.ToDomain())];

            if (claimed.Falsey())
                return D2Result<IReadOnlyList<StagedOutboxMessage>>.Ok(claimed);

            var until = now + lease;

            foreach (var row in rows)
            {
                row.Attempts += 1;
                row.NextAttemptAt = until;
            }

            await db.SaveChangesAsync(ct);

            return D2Result<IReadOnlyList<StagedOutboxMessage>>.Ok(claimed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another claimant wrote a row of this batch after it was read here.
            // That claimant owns the attempt, so nothing is returned and nothing
            // is dispatched; the database responded, so this is not an outage.
            Forget();

            return D2Result<IReadOnlyList<StagedOutboxMessage>>.Conflict();
        }
        catch
        {
            // Nothing is returned on this arm, so nothing is dispatched. A claim
            // that could not be written must not become a delivery whose attempt
            // nobody counted.
            Forget();

            return D2Result<IReadOnlyList<StagedOutboxMessage>>.ServiceUnavailable();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<D2Result> RecordAsync(
        OutboxMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var id = message.Id.Value;

            var row = await db.Set<OutboxMessageRow>()
                .FirstOrDefaultAsync(m => m.Id == id, ct);

            if (row is null)
                return D2Result.NotFound();

            message.ProjectOnto(row);
            await db.SaveChangesAsync(ct);

            return D2Result.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateConcurrencyException)
        {
            Forget();

            return D2Result.Conflict();
        }
        catch
        {
            Forget();

            return D2Result.ServiceUnavailable();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<D2Result<int>> PruneSettledAsync(
        Instant publishedBefore,
        Instant abandonedBefore,
        int batchSize,
        CancellationToken ct = default)
    {
        if (batchSize <= 0)
            return D2Result<int>.ValidationFailed();

        try
        {
            // The ids are read first and the delete lists them, because a
            // provider that cannot translate a bounded delete would otherwise
            // either delete the whole backlog in one statement or refuse. A row
            // another writer settles between the read and the delete is deleted
            // on a later tick instead.
            var due = await db.Set<OutboxMessageRow>()
                .Where(m =>
                    (m.Status == OutboxMessageStatus.Published
                        && m.PublishedAt != null
                        && m.PublishedAt < publishedBefore)
                    || (m.Status == OutboxMessageStatus.Abandoned
                        && m.AbandonedAt != null
                        && m.AbandonedAt < abandonedBefore))
                .OrderBy(m => m.StagedAt)
                .Select(m => m.Id)
                .Take(batchSize)
                .ToListAsync(ct);

            if (due.Falsey())
                return D2Result<int>.Ok();

            var deleted = await db.Set<OutboxMessageRow>()
                .Where(m => due.Contains(m.Id))
                .ExecuteDeleteAsync(ct);

            return D2Result<int>.Ok(deleted);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Nothing was deleted, or the delete itself failed. Either way the
            // rows are still there and the next tick finds them again.
            Forget();

            return D2Result<int>.ServiceUnavailable();
        }
    }

    /// <summary>
    /// Drops what the failed write left tracked, so the next tick on this scope
    /// does not re-send a stale entity along with its own work.
    /// </summary>
    /// <remarks>
    /// The clear is itself allowed to fail. A context whose connection died is
    /// often a context that has been disposed. Letting that second failure escape
    /// would make the store throw an exception where its contract returns a
    /// failure result.
    /// </remarks>
    private void Forget()
    {
        try
        {
            db.ChangeTracker.Clear();
        }
        catch (ObjectDisposedException)
        {
            // Nothing is tracked on a disposed context; there is nothing to drop.
        }
    }
}
