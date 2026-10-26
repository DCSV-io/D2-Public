// -----------------------------------------------------------------------
// <copyright file="TestMetricCollector.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Handler;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using DcsvIo.D2.Handler;

/// <summary>
/// Captures every measurement on every instrument on the
/// <see cref="HandlerTelemetry.SR_Meter"/> for the lifetime of the collector.
/// Disposes the listener on <see cref="Dispose"/>. Uses a
/// <see cref="ConcurrentQueue{T}"/> internally so concurrent measurement
/// callbacks (10-way handler invocations etc.) don't race on collection
/// mutation.
/// </summary>
internal sealed class TestMetricCollector : IDisposable
{
    private readonly MeterListener r_listener;
    private readonly ConcurrentQueue<MeasurementFixture> r_measurements = new();

    public TestMetricCollector()
    {
        r_listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == HandlerTelemetry.SourceName)
                    listener.EnableMeasurementEvents(instrument);
            },
        };
        r_listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            r_measurements.Enqueue(
                new MeasurementFixture(instrument.Name, value, HandlerNameOf(tags))));
        r_listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            r_measurements.Enqueue(
                new MeasurementFixture(instrument.Name, value, HandlerNameOf(tags))));
        r_listener.Start();
    }

    public IReadOnlyCollection<MeasurementFixture> Measurements => r_measurements;

    public long CountFor(string instrumentName) =>
        r_measurements
            .Where(m => m.InstrumentName == instrumentName)
            .Sum(m => Convert.ToInt64(m.Value, CultureInfo.InvariantCulture));

    /// <summary>
    /// The sum over the measurements this instrument recorded for one handler
    /// name, so a test asserts an exact count against the handler type it
    /// declared rather than against everything the process-wide meter saw.
    /// </summary>
    /// <param name="instrumentName">The instrument to sum.</param>
    /// <param name="handlerName">The <c>d2.handler.name</c> tag value to match.</param>
    /// <returns>The summed count for that instrument and handler name.</returns>
    public long CountFor(string instrumentName, string handlerName) =>
        r_measurements
            .Where(m => m.InstrumentName == instrumentName && m.HandlerName == handlerName)
            .Sum(m => Convert.ToInt64(m.Value, CultureInfo.InvariantCulture));

    public IReadOnlyList<double> ValuesFor(string instrumentName) =>
        r_measurements
            .Where(m => m.InstrumentName == instrumentName)
            .Select(m => Convert.ToDouble(m.Value, CultureInfo.InvariantCulture))
            .ToList();

    public void Dispose() => r_listener.Dispose();

    private static string? HandlerNameOf(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == "d2.handler.name")
                return tag.Value as string;
        }

        return null;
    }
}
