// -----------------------------------------------------------------------
// <copyright file="ForgeryRefusalMeterRecorderFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.AspNetCore.Fixtures;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using DcsvIo.D2.AspNetCore.Forgery;

/// <summary>
/// Listens to the forgery refusal counter for the life of a <c>using</c>
/// block and keeps the outcome tag of every measurement it saw.
/// </summary>
/// <remarks>
/// The counter is process-global, so a test asserts that the reason it
/// provoked is among the reasons recorded rather than asserting a count.
/// </remarks>
internal sealed class ForgeryRefusalMeterRecorderFixture : IDisposable
{
    private readonly ConcurrentQueue<string> r_reasons = new();
    private readonly MeterListener r_listener;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="ForgeryRefusalMeterRecorderFixture"/> class and starts
    /// listening.
    /// </summary>
    public ForgeryRefusalMeterRecorderFixture()
    {
        r_listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ForgeryProtectionTelemetry.METER_NAME)
                    listener.EnableMeasurementEvents(instrument);
            },
        };

        r_listener.SetMeasurementEventCallback<long>(
            (_, _, tags, _) =>
            {
                foreach (var tag in tags)
                {
                    if (tag.Key == ForgeryProtectionTelemetry.TAG_OUTCOME
                        && tag.Value is string reason)
                    {
                        r_reasons.Enqueue(reason);
                    }
                }
            });

        r_listener.Start();
    }

    /// <summary>Gets the outcome tag of every measurement seen so far.</summary>
    public IReadOnlyCollection<string> Reasons => [.. r_reasons];

    /// <inheritdoc />
    public void Dispose() => r_listener.Dispose();
}
