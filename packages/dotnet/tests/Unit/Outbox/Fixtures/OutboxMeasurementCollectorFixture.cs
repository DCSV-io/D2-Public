// -----------------------------------------------------------------------
// <copyright file="OutboxMeasurementCollectorFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using DcsvIo.D2.Outbox.EntityFrameworkCore;

/// <summary>
/// Captures every measurement the relay's instruments emit, with the tags each
/// one had, so a test can assert the kind tag reached the wire verbatim
/// rather than only that a counter moved.
/// </summary>
/// <remarks>
/// The meter is a process-wide static, so a collector that took every
/// measurement would see the ones parallel test classes produce. Each test
/// therefore stages under its own kind name and reads the counters through
/// <see cref="CountFor(string, string)"/>, which filters on that tag.
/// </remarks>
internal sealed class OutboxMeasurementCollectorFixture : IDisposable
{
    private readonly MeterListener r_listener;
    private readonly
        ConcurrentQueue<(string Instrument, long Value, string? Kind, string? Outcome)>
        r_taken = new();

    private readonly ConcurrentQueue<(string Instrument, double Value, string? Kind)>
        r_durations = new();

    /// <summary>
    /// Initializes a new <see cref="OutboxMeasurementCollectorFixture"/> and
    /// starts listening.
    /// </summary>
    public OutboxMeasurementCollectorFixture()
    {
        r_listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == OutboxTelemetry.METER_NAME)
                    listener.EnableMeasurementEvents(instrument);
            },
        };

        r_listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            string? kind = null;
            string? outcome = null;

            foreach (var tag in tags)
            {
                if (tag.Key == OutboxTelemetry.TAG_KIND)
                    kind = tag.Value as string;

                if (tag.Key == OutboxTelemetry.TAG_OUTCOME)
                    outcome = tag.Value as string;
            }

            r_taken.Enqueue((instrument.Name, value, kind, outcome));
        });

        // The duration histogram records seconds as a double, and a listener
        // that subscribes only to long never sees it at all.
        r_listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            string? kind = null;

            foreach (var tag in tags)
            {
                if (tag.Key == OutboxTelemetry.TAG_KIND)
                    kind = tag.Value as string;
            }

            r_durations.Enqueue((instrument.Name, value, kind));
        });

        r_listener.Start();
    }

    /// <summary>Sums one instrument's measurements carrying one kind tag.</summary>
    /// <param name="instrumentName">The instrument's wire name.</param>
    /// <param name="kind">The kind tag value to filter on.</param>
    /// <returns>The total.</returns>
    public long CountFor(string instrumentName, string kind)
        => r_taken
            .Where(m => m.Instrument == instrumentName && m.Kind == kind)
            .Sum(m => m.Value);

    /// <summary>
    /// Sums one instrument's measurements carrying one kind tag AND one outcome
    /// tag, which is how a failure class is told apart from every other.
    /// </summary>
    /// <param name="instrumentName">The instrument's wire name.</param>
    /// <param name="kind">The kind tag value to filter on.</param>
    /// <param name="outcome">The outcome tag value to filter on.</param>
    /// <returns>The total.</returns>
    public long CountFor(string instrumentName, string kind, string outcome)
        => r_taken
            .Where(m => m.Instrument == instrumentName
                && m.Kind == kind
                && m.Outcome == outcome)
            .Sum(m => m.Value);

    /// <summary>
    /// Counts one histogram's recordings carrying one kind tag.
    /// </summary>
    /// <param name="instrumentName">The instrument's wire name.</param>
    /// <param name="kind">The kind tag value to filter on.</param>
    /// <returns>How many measurements it recorded.</returns>
    public int RecordingsFor(string instrumentName, string kind)
        => r_durations.Count(m => m.Instrument == instrumentName && m.Kind == kind);

    /// <inheritdoc/>
    public void Dispose() => r_listener.Dispose();
}
