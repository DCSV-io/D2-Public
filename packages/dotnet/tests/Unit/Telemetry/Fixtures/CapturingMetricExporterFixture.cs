// -----------------------------------------------------------------------
// <copyright file="CapturingMetricExporterFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Telemetry.Fixtures;

using System.Collections.Concurrent;
using OpenTelemetry;
using OpenTelemetry.Metrics;

/// <summary>
/// Keeps the meter name and instrument name of every metric a MeterProvider
/// exports, so a test can read which meters the provider is listening to.
/// </summary>
internal sealed class CapturingMetricExporterFixture : BaseExporter<Metric>
{
    private readonly ConcurrentBag<(string MeterName, string Name)> r_captured = [];

    /// <summary>Gets every metric exported so far.</summary>
    /// <returns>The meter name and instrument name of each.</returns>
    public IReadOnlyCollection<(string MeterName, string Name)> Snapshot() =>
        [.. r_captured];

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<Metric> batch)
    {
        foreach (var metric in batch)
            r_captured.Add((metric.MeterName, metric.Name));

        return ExportResult.Success;
    }
}
