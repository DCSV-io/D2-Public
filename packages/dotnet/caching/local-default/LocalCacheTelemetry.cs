// -----------------------------------------------------------------------
// <copyright file="LocalCacheTelemetry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching.Local.Default;

using System.Diagnostics.Metrics;

/// <summary>
/// Static OTel <see cref="Meter"/> for the default local-cache impl.
/// The cache emits no span or log per call, because a cache operation takes
/// about 60ns and a span or log per call would take longer than the operation
/// itself. It increments counters for hits, misses and evictions instead.
/// </summary>
/// <remarks>
/// <para>
/// The class is <c>public</c> so the meter name constant
/// (<see cref="METER_NAME"/>) is reachable from other assemblies, which
/// register the meter so local-cache hits / misses / evictions reach the OTLP /
/// Prometheus exporters without per-host opt-in. The counter fields remain
/// <c>internal</c> because only the lib's own hot-path code increments them.
/// </para>
/// </remarks>
public static class LocalCacheTelemetry
{
    /// <summary>
    /// The OpenTelemetry <see cref="Meter"/> name. Register it with
    /// <c>.WithMetrics(m =&gt; m.AddMeter(LocalCacheTelemetry.METER_NAME))</c>
    /// when wiring telemetry without <c>DcsvIo.D2.Telemetry</c>'s aggregator.
    /// </summary>
    public const string METER_NAME = "DcsvIo.D2.Caching.Local";

    /// <summary>OTel meter for the default local cache.</summary>
    internal static readonly Meter SR_Meter = new(METER_NAME, "1.0.0");

    /// <summary>Counter incremented on every cache hit.</summary>
    internal static readonly Counter<long> SR_Hits =
        SR_Meter.CreateCounter<long>("d2.cache.local.hits", unit: "{hit}", description: "Local cache hits.");

    /// <summary>Counter incremented on every cache miss.</summary>
    internal static readonly Counter<long> SR_Misses =
        SR_Meter.CreateCounter<long>("d2.cache.local.misses", unit: "{miss}", description: "Local cache misses.");

    /// <summary>Counter incremented on every cache write.</summary>
    internal static readonly Counter<long> SR_Sets =
        SR_Meter.CreateCounter<long>("d2.cache.local.sets", unit: "{write}", description: "Local cache writes.");

    /// <summary>Counter incremented on every explicit removal.</summary>
    internal static readonly Counter<long> SR_Removes = SR_Meter.CreateCounter<long>(
        "d2.cache.local.removes",
        unit: "{removal}",
        description: "Local cache removals (explicit).");

    /// <summary>Counter incremented on every implicit eviction (capacity / TTL).</summary>
    internal static readonly Counter<long> SR_Evictions = SR_Meter.CreateCounter<long>(
        "d2.cache.local.evictions",
        unit: "{eviction}",
        description: "Entries evicted by capacity / expiration.");
}
