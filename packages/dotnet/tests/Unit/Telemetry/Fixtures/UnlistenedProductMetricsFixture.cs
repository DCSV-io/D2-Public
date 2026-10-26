// -----------------------------------------------------------------------
// <copyright file="UnlistenedProductMetricsFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Telemetry.Fixtures;

using System.Diagnostics.Metrics;

/// <summary>
/// The same shape as <see cref="ListeningProductMetricsFixture"/> under a meter
/// name of its own. The seam tests register it as a bare singleton, which is the
/// drift <c>AddD2Meter</c> exists to prevent: the type resolves, the counter
/// takes measurements, and no provider subscribes its meter.
/// </summary>
internal sealed class UnlistenedProductMetricsFixture
{
    /// <summary>The meter name this fixture creates its instruments under.</summary>
    public const string METER_NAME = "DcsvIo.D2.Tests.Telemetry.Unlistened";

    private readonly Counter<long> r_events;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="UnlistenedProductMetricsFixture"/> class.
    /// </summary>
    /// <param name="meterFactory">The container's meter factory.</param>
    public UnlistenedProductMetricsFixture(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(METER_NAME);

        r_events = meter.CreateCounter<long>(
            name: "d2.tests.telemetry.unlistened_events",
            unit: "{event}",
            description: "Measurements the seam tests emit on an unregistered meter.");
    }

    /// <summary>Records one measurement.</summary>
    public void RecordEvent() => r_events.Add(1);
}
