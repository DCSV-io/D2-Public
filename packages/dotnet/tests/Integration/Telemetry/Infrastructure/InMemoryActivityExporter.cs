// -----------------------------------------------------------------------
// <copyright file="InMemoryActivityExporter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;

using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry;

/// <summary>
/// Hand-rolled <see cref="BaseExporter{T}"/> that captures every exported
/// <see cref="Activity"/> into an in-memory <see cref="ConcurrentQueue{T}"/>.
/// It records what the OTel pipeline emits without a dependency on the
/// <c>OpenTelemetry.Exporter.InMemory</c> NuGet package, because the surface
/// each test needs is small and the project keeps test dependencies few. The
/// Serilog tests make the same choice with <c>InMemorySink</c>.
/// </summary>
internal sealed class InMemoryActivityExporter : BaseExporter<Activity>
{
    private readonly ConcurrentQueue<Activity> r_activities = new();

    // Completed and replaced by every Export call. A waiter reads it before it
    // checks the captured set, so an Export that runs between the check and
    // the wait completes the task the waiter awaits.
    private TaskCompletionSource _exportSignal = NewSignal();

    /// <summary>
    /// Gets a snapshot of every <see cref="Activity"/> captured since the
    /// last <see cref="Reset"/>.
    /// </summary>
    /// <returns>The captured activities, in export order.</returns>
    public IReadOnlyCollection<Activity> Snapshot() => r_activities.ToArray();

    /// <summary>
    /// Gets the captured activities that were emitted by the named
    /// <see cref="ActivitySource"/>.
    /// </summary>
    /// <param name="sourceName">The source name to filter by.</param>
    /// <returns>
    /// The subset of captured activities whose source name matches.
    /// </returns>
    public IReadOnlyCollection<Activity> Snapshot(string sourceName) =>
        r_activities
            .Where(a => string.Equals(a.Source.Name, sourceName, StringComparison.Ordinal))
            .ToArray();

    /// <summary>
    /// Waits until a captured <see cref="Activity"/> matches
    /// <paramref name="predicate"/>, then returns every captured activity.
    /// The method checks the activities already captured first, so it returns
    /// at once when a match is present. Otherwise it waits for the next
    /// <see cref="Export"/> call and checks again.
    /// </summary>
    /// <param name="predicate">
    /// The condition the awaited activity meets. It runs on the calling thread;
    /// an exception it throws propagates to the caller.
    /// </param>
    /// <param name="timeout">
    /// How long to wait for a match. When it passes, the method returns the
    /// activities captured so far without throwing, so the caller's assertion
    /// reports the missing activity. <see cref="TimeSpan.Zero"/> checks once.
    /// </param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>
    /// A snapshot of every captured activity, taken after a match was found or
    /// after <paramref name="timeout"/> passed.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="predicate"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="timeout"/> is negative.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled before a match was found.
    /// </exception>
    public async Task<IReadOnlyCollection<Activity>> WaitForAsync(
        Func<Activity, bool> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);

        var elapsed = Stopwatch.StartNew();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Read the signal before the check: an Export that adds the match
            // after the check also completes this signal.
            var signal = Volatile.Read(ref _exportSignal);

            if (r_activities.Any(predicate))
                return Snapshot();

            var remaining = timeout - elapsed.Elapsed;

            if (remaining <= TimeSpan.Zero)
                return Snapshot();

            try
            {
                await signal.Task.WaitAsync(remaining, cancellationToken);
            }
            catch (TimeoutException)
            {
                return Snapshot();
            }
        }
    }

    /// <summary>
    /// Discards every captured activity, so a single test method can reuse
    /// the exporter across several operations without activities from an
    /// earlier operation appearing in a later snapshot.
    /// </summary>
    public void Reset() => r_activities.Clear();

    /// <inheritdoc />
    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (var activity in batch)
            r_activities.Enqueue(activity);

        // The exchange runs after the enqueue, so a waiter that reads the new
        // signal also sees the activities added above.
        Interlocked.Exchange(ref _exportSignal, NewSignal()).TrySetResult();
        return ExportResult.Success;
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
