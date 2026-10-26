// -----------------------------------------------------------------------
// <copyright file="OutboxRelay.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres;

using System.Diagnostics;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Outbox;
using DcsvIo.D2.Outbox.EntityFrameworkCore;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Duration = NodaTime.Duration;
using IClock = DcsvIo.D2.Time.IClock;
using Instant = NodaTime.Instant;

/// <summary>
/// Delivers staged messages. One tick claims a batch, hands each message to the
/// dispatcher whose kind matches, and records what happened.
/// </summary>
/// <typeparam name="TDb">
/// The database this relay drains, identified by its application-layer context
/// interface. This parameter identifies the relay: the store it reads through,
/// the dispatchers it may hand messages to, and the settings it takes its lock
/// key and connection string from are each resolved by it.
/// </typeparam>
/// <remarks>
/// <para>
/// <b>One instance dispatches at a time.</b> Each tick takes a PostgreSQL
/// try-advisory-lock on the consumer's own key, skip-if-held, so exactly one
/// instance dispatches per window and the others wait for their next tick. The
/// lock auto-releases on connection drop, so a crashed instance never blocks
/// delivery.
/// </para>
/// <para>
/// <b>Delivery is at-least-once.</b> A relay that dies after a dispatcher
/// confirmed and before the state was recorded dispatches the message again, so
/// consumers must be idempotent.
/// </para>
/// <para>
/// <b>At-least-once is still bounded.</b> The attempt is counted by the claim
/// rather than by the record, so a message whose outcome could not be written -
/// a row-version conflict, or a database serving reads while refusing writes -
/// still advances toward <see cref="OutboxOptions.RetryLimit"/> and still waits
/// before it is attempted again. Counting on the record instead would leave that
/// message eligible on the very next tick with its attempt count unchanged, and
/// bounded at-least-once would become an unbounded publish loop.
/// </para>
/// </remarks>
public sealed class OutboxRelay<TDb> : BackgroundService
{
    /// <summary>
    /// Recorded when a staged kind has no dispatcher. It means a deployment is
    /// missing a component rather than that the message is bad, which is why the
    /// message consumes the retry window rather than being abandoned at once - a
    /// rolling deploy that adds the dispatcher delivers it.
    /// </summary>
    /// <remarks>
    /// <b>This is a relay marker rather than an error code</b>, and its shape
    /// distinguishes it. The same column also receives a dispatcher's own
    /// <see cref="D2Result.ErrorCode"/>, which comes from a generated catalog and
    /// is upper-case and underscored; anything lower-case and dotted was written
    /// by the relay about itself, describes no caller-visible failure, and has no
    /// HTTP status, category or user message because none of those exist for it.
    /// </remarks>
    public const string NO_DISPATCHER_MARKER = "outbox.no-dispatcher";

    /// <summary>
    /// Recorded when a dispatcher threw instead of returning a result. A relay marker on
    /// the same terms as <see cref="NO_DISPATCHER_MARKER"/>.
    /// </summary>
    public const string DISPATCHER_THREW_MARKER = "outbox.dispatcher-threw";

    private readonly ISystemWorkScopeFactory r_systemWork;
    private readonly IEnumerable<IOutboxDispatcherBinding<TDb>> r_dispatchers;
    private readonly IClock r_clock;
    private readonly ILogger<OutboxRelay<TDb>> r_logger;
    private readonly OutboxOptions r_options;

    /// <summary>Initializes a new <see cref="OutboxRelay{TDb}"/>.</summary>
    /// <param name="systemWork">Opens the System-plane work scope each tick runs in.</param>
    /// <param name="dispatchers">
    /// Every dispatcher bound to this database, indexed at start by kind.
    /// </param>
    /// <param name="clock">The clock the due predicate and every stamp read from.</param>
    /// <param name="options">The relay's tick, batch size, retry limit and backoff.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="storePresence">
    /// The container's report of whether a service is registered, used once
    /// to reject a relay whose store is missing.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any argument is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no <see cref="IOutboxStore{TDb}"/> is registered for
    /// <typeparamref name="TDb"/>.
    /// </exception>
    public OutboxRelay(
        ISystemWorkScopeFactory systemWork,
        IEnumerable<IOutboxDispatcherBinding<TDb>> dispatchers,
        IClock clock,
        IOptionsMonitor<OutboxOptionsFor<TDb>> options,
        ILogger<OutboxRelay<TDb>> logger,
        IServiceProviderIsService storePresence)
    {
        ArgumentNullException.ThrowIfNull(systemWork);
        ArgumentNullException.ThrowIfNull(dispatchers);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(storePresence);

        RequireStore(storePresence);

        r_systemWork = systemWork;
        r_dispatchers = dispatchers;
        r_clock = clock;
        r_logger = logger;
        r_options = options.CurrentValue;
    }

    /// <summary>
    /// The wait before the next attempt on a message that has already been
    /// tried <paramref name="attemptsAlreadyMade"/> times. Exponential on the
    /// base, doubling per attempt already made, capped at the ceiling.
    /// </summary>
    /// <param name="options">The curve's three parameters.</param>
    /// <param name="attemptsAlreadyMade">
    /// How many attempts this message has had, counting the one that just failed.
    /// </param>
    /// <returns>The wait, never longer than the ceiling.</returns>
    /// <remarks>
    /// No jitter is added: one relay holds the advisory lock, so there is no
    /// herd to spread, and jitter would only make the wait harder to reason
    /// about from a row.
    /// </remarks>
    internal static Duration ComputeBackoff(OutboxOptions options, int attemptsAlreadyMade)
    {
        var ceiling = Duration.FromTimeSpan(options.RetryBackoffCeiling);
        var doublings = attemptsAlreadyMade - 1;

        // The shift is bounded before it is taken. A retry limit an operator set
        // high enough would otherwise overflow the multiplier and produce a
        // negative wait, which reads as attemptable-now and turns the backoff
        // into a hot loop against a broker that is already down.
        if (doublings is < 0 or > 30)
            return doublings < 0 ? Duration.FromTimeSpan(options.RetryBackoffBase) : ceiling;

        var wait = Duration.FromTimeSpan(options.RetryBackoffBase) * (1L << doublings);

        return wait > ceiling ? ceiling : wait;
    }

    /// <summary>
    /// Runs one tick's worth of work inside an already-open work scope, with the
    /// relay lock already held. Internal so a test drives it directly - the real
    /// advisory-lock acquire needs a live PostgreSQL connection.
    /// </summary>
    /// <param name="store">The store this tick reads and records through.</param>
    /// <param name="index">The dispatcher index this relay built at start.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How many messages this tick dispatched successfully.</returns>
    internal async Task<int> DrainAsync(
        IOutboxStore<TDb> store,
        IReadOnlyDictionary<string, IOutboxDispatcher> index,
        CancellationToken ct)
    {
        var now = r_clock.GetCurrentInstant();

        // The claim keeps the retry budget correct against a database that
        // reads and will not write. It advances the attempt count and pushes the
        // next attempt out before anything is dispatched, so a message whose
        // outcome could not be recorded is neither immediately re-eligible nor
        // stuck at the attempt count it had. The lease is the longest wait
        // the curve reaches, because nothing on that path indicates when the
        // write will start working again.
        var lease = Duration.FromTimeSpan(r_options.RetryBackoffCeiling);
        var claimed = await store.ClaimDueAsync(now, r_options.BatchSize, lease, ct);

        if (!claimed.IsOk)
        {
            OutboxLog.ClaimFailed(
                r_logger, "batch", "any", claimed.ErrorCode ?? "unknown");

            return 0;
        }

        var published = 0;

        foreach (var message in claimed.Data!)
        {
            var moved = await DispatchOneAsync(message, index, ct);

            if (moved is PublishedOutboxMessage)
                published++;

            var recorded = await store.RecordAsync(moved, ct);

            if (!recorded.IsOk)
            {
                OutboxLog.RecordFailed(
                    r_logger,
                    message.Id.ToString(),
                    message.Kind,
                    moved.Status.ToString(),
                    recorded.ErrorCode ?? "unknown");
            }
        }

        await PruneAsync(store, now, ct);

        return published;
    }

    /// <summary>
    /// Builds the dispatcher index. A duplicate kind makes the index fail to
    /// build instead of choosing one dispatcher: a group-and-take-first would
    /// deliver that kind to whichever dispatcher was registered first, and no
    /// registration selects that dispatcher for the kind.
    /// </summary>
    /// <returns>The dispatchers bound to this database, by kind.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when two bound dispatchers claim the same kind.
    /// </exception>
    internal IReadOnlyDictionary<string, IOutboxDispatcher> BuildIndex()
        => r_dispatchers.ToDictionary(
            b => b.Dispatcher.Kind, b => b.Dispatcher, StringComparer.Ordinal);

    /// <summary>
    /// One tick: take the per-database advisory lock, drain a batch and prune
    /// settled rows past their windows. A tick that does not get the lock does
    /// none of that, because another instance is already doing it.
    /// </summary>
    /// <param name="index">The dispatchers, keyed by stored kind name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The running tick.</returns>
    internal async Task RunTickAsync(
        IReadOnlyDictionary<string, IOutboxDispatcher> index, CancellationToken ct)
    {
        try
        {
            await using var relayLock = await PgAdvisoryLock.TryAcquireSessionAsync(
                r_options.RelayConnectionString, r_options.RelayAdvisoryLockKey, ct);

            if (!relayLock.IsHeld)
            {
                OutboxLog.TickSkippedLockHeld(r_logger);
                return;
            }

            await using var work = await r_systemWork.BeginAsync(ct);
            var store = work.Services.GetRequiredService<IOutboxStore<TDb>>();
            _ = await DrainAsync(store, index, ct);
        }
        catch (OperationCanceledException)
        {
            // The host is shutting down; let the loop exit cleanly.
            throw;
        }
        catch (Exception ex)
        {
            // A relay tick must never crash the host. Log the failure, then wait
            // for the next tick - the cadence bounds the retry rate on its own.
            OutboxLog.TickFailed(
                r_logger,
                SanitizedExceptionRender.TypeName(ex),
                SanitizedExceptionRender.FirstFrame(ex));
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The first thing the relay does, so a duplicate kind surfaces when the
        // relay starts rather than on a later tick.
        var index = BuildIndex();

        OutboxLog.RelayStarted(
            r_logger, r_options.RelayTick.TotalSeconds, r_options.BatchSize);

        using var timer = new PeriodicTimer(r_options.RelayTick);

        do
        {
            await RunTickAsync(index, stoppingToken);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitForNextTickAsync(
        PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static void RecordDispatchDuration(
        long startedAt, KeyValuePair<string, object?> tags)
        => OutboxTelemetry.SR_DispatchDurationSeconds.Record(
            Stopwatch.GetElapsedTime(startedAt).TotalSeconds, tags);

    /// <summary>
    /// Rejects a relay whose database has no store. The relay resolves its store
    /// inside each tick, and a tick that throws is caught so a bad tick cannot
    /// crash the host - so without this the relay runs for the life of the host,
    /// claiming nothing and logging a failed tick each time.
    /// </summary>
    /// <param name="storePresence">The container's is-registered answer.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no <see cref="IOutboxStore{TDb}"/> is registered.
    /// </exception>
    private static void RequireStore(IServiceProviderIsService storePresence)
    {
        if (storePresence.IsService(typeof(IOutboxStore<TDb>)))
            return;

        throw new InvalidOperationException(
            $"An outbox relay is registered for '{typeof(TDb).FullName}', but no "
            + $"IOutboxStore<{typeof(TDb).Name}> is registered, so the relay would "
            + "claim nothing for the life of the host. Call "
            + $"services.AddD2OutboxEntityFrameworkCore<{typeof(TDb).Name}, TDbContext>() "
            + "on this host.");
    }

    private async Task<OutboxMessage> DispatchOneAsync(
        StagedOutboxMessage message,
        IReadOnlyDictionary<string, IOutboxDispatcher> index,
        CancellationToken ct)
    {
        var at = r_clock.GetCurrentInstant();
        var tags = new KeyValuePair<string, object?>(OutboxTelemetry.TAG_KIND, message.Kind);

        if (!index.TryGetValue(message.Kind, out var dispatcher))
        {
            return Failed(
                message,
                at,
                NO_DISPATCHER_MARKER,
                tags,
                OutboxTelemetry.Outcome.NO_DISPATCHER);
        }

        D2Result sent;

        // A monotonic source rather than the clock: this measures elapsed real
        // time, which a clock a test moves by hand does not.
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            sent = await dispatcher.DispatchAsync(message, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            RecordDispatchDuration(startedAt, tags);

            // A dispatcher that throws is a transport fault that surfaces as an
            // exception, and it must not stop the whole tick - the rest of the
            // batch is still deliverable.
            OutboxLog.TickFailed(
                r_logger,
                SanitizedExceptionRender.TypeName(ex),
                SanitizedExceptionRender.FirstFrame(ex));

            return Failed(
                message,
                at,
                DISPATCHER_THREW_MARKER,
                tags,
                OutboxTelemetry.Outcome.DISPATCHER_THREW);
        }

        RecordDispatchDuration(startedAt, tags);

        if (!sent.IsOk)
        {
            return Failed(
                message,
                at,
                sent.ErrorCode ?? "unknown",
                tags,
                OutboxTelemetry.Outcome.DISPATCHER_REFUSED);
        }

        OutboxTelemetry.SR_PublishedTotal.Add(1, tags);

        return message.Publish(at);
    }

    private OutboxMessage Failed(
        StagedOutboxMessage message,
        Instant at,
        string failureCode,
        KeyValuePair<string, object?> tags,
        string outcome)
    {
        var attemptsAfterThis = message.Attempts + 1;
        var backoff = ComputeBackoff(r_options, attemptsAfterThis);
        var moved = message.Fail(at, failureCode, r_options.RetryLimit, backoff);

        // Both error counters are tagged with the failure class. The published
        // counter is tagged with the kind alone: there is one way to succeed.
        var errorTags = new TagList
        {
            tags,
            new(OutboxTelemetry.TAG_OUTCOME, outcome),
        };

        if (moved is AbandonedOutboxMessage abandoned)
        {
            OutboxTelemetry.SR_AbandonedTotal.Add(1, errorTags);
            OutboxLog.MessageAbandoned(
                r_logger,
                abandoned.Id.ToString(),
                abandoned.Kind,
                abandoned.Attempts,
                abandoned.LastFailureCode);

            return moved;
        }

        OutboxTelemetry.SR_DispatchFailuresTotal.Add(1, errorTags);
        OutboxLog.DispatchFailed(
            r_logger,
            message.Id.ToString(),
            message.Kind,
            attemptsAfterThis,
            failureCode,
            backoff.TotalSeconds);

        return moved;
    }

    /// <summary>
    /// Deletes the settled rows past their retention window, after the tick's
    /// deliveries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It runs on the same tick and under the same advisory lock as the drain, so
    /// exactly one instance deletes and the deletion needs no schedule of its
    /// own. It runs after the deliveries, so a sweep never uses the tick's
    /// budget before a message that is waiting for delivery.
    /// </para>
    /// <para>
    /// A staged row is never deleted, whatever its age. The batch bound is the
    /// tick's own <see cref="OutboxOptions.BatchSize"/>, so a backlog drains over
    /// as many ticks as it takes rather than in one statement.
    /// </para>
    /// </remarks>
    /// <param name="store">The store this tick deletes through.</param>
    /// <param name="now">The instant the windows are measured back from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the sweep has been attempted.</returns>
    private async Task PruneAsync(IOutboxStore<TDb> store, Instant now, CancellationToken ct)
    {
        var pruned = await store.PruneSettledAsync(
            now - Duration.FromTimeSpan(r_options.PublishedRetention),
            now - Duration.FromTimeSpan(r_options.AbandonedRetention),
            r_options.BatchSize,
            ct);

        if (!pruned.IsOk)
        {
            OutboxLog.PruneFailed(r_logger, pruned.ErrorCode ?? "unknown");

            return;
        }

        if (pruned.Data > 0)
            OutboxTelemetry.SR_PrunedTotal.Add(pruned.Data);
    }
}
