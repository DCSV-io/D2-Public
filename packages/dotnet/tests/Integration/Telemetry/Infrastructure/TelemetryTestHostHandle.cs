// -----------------------------------------------------------------------
// <copyright file="TelemetryTestHostHandle.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;

using System.Diagnostics;
using Microsoft.Extensions.Hosting;

/// <summary>
/// References to the started host and the in-memory activity, metric and log
/// exporters, returned by <see cref="TelemetryTestHostBuilder.BuildAsync"/>.
/// The test disposes the handle with <c>await using</c>.
/// </summary>
/// <param name="Host">The started in-process host.</param>
/// <param name="Activities">The captured-activity exporter.</param>
/// <param name="Metrics">The captured-metric exporter.</param>
/// <param name="Logs">The captured-log exporter.</param>
internal sealed record TelemetryTestHostHandle(
    IHost Host,
    InMemoryActivityExporter Activities,
    InMemoryMetricExporter Metrics,
    InMemoryLogRecordExporter Logs) : IAsyncDisposable
{
    // Caps a wait for a span that is never exported. A passing run returns
    // when the span is exported, long before this.
    private static readonly TimeSpan sr_activityWaitTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Waits until <see cref="Activities"/> has captured an activity that
    /// matches <paramref name="predicate"/>, through
    /// <see cref="InMemoryActivityExporter.WaitForAsync"/> with a 30-second
    /// timeout.
    /// </summary>
    /// <param name="predicate">The condition the awaited activity meets.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>
    /// Every captured activity, taken after a match was found or after the
    /// timeout passed.
    /// </returns>
    public Task<IReadOnlyCollection<Activity>> WaitForActivityAsync(
        Func<Activity, bool> predicate,
        CancellationToken cancellationToken) =>
        Activities.WaitForAsync(predicate, sr_activityWaitTimeout, cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Host.StopAsync(TimeSpan.FromSeconds(5));
        Host.Dispose();
    }
}
