// -----------------------------------------------------------------------
// <copyright file="ListeningProductMetricsFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Telemetry.Fixtures;

using System.Diagnostics.Metrics;

/// <summary>
/// A product metrics type in the shape a service ships: a sealed class built
/// from the container's <see cref="IMeterFactory"/>, holding its instruments as
/// instance members, with its meter name a constant beside them. Registered
/// through <c>AddD2Meter</c> by the seam tests.
/// </summary>
internal sealed class ListeningProductMetricsFixture
{
    /// <summary>The meter name this fixture creates its instruments under.</summary>
    public const string METER_NAME = "DcsvIo.D2.Tests.Telemetry.Listening";

    private readonly Counter<long> r_events;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="ListeningProductMetricsFixture"/> class.
    /// </summary>
    /// <param name="meterFactory">The container's meter factory.</param>
    public ListeningProductMetricsFixture(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(METER_NAME);

        r_events = meter.CreateCounter<long>(
            name: "d2.tests.telemetry.listening_events",
            unit: "{event}",
            description: "Measurements the seam tests emit on a registered meter.");
    }

    /// <summary>Records one measurement.</summary>
    public void RecordEvent() => r_events.Add(1);
}
