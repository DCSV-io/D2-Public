// -----------------------------------------------------------------------
// <copyright file="RedisCacheTelemetry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching.Distributed.Redis;

using System.Diagnostics.Metrics;

/// <summary>
/// Static OTel <see cref="Meter"/> for the Redis distributed cache.
/// The meter contains aggregate counters. Per-call instrumentation costs a few ns
/// per counter increment, which is invisible next to a network-bound Redis
/// operation (1-5ms).
/// </summary>
/// <remarks>
/// <para>
/// The class is <c>public</c> so the meter name constant
/// (<see cref="METER_NAME"/>) is reachable from other assemblies, which
/// register the meter so cache hits / misses / sets reach the OTLP /
/// Prometheus exporters without per-host opt-in. The counter fields remain
/// <c>internal</c> because only the lib's own hot-path code increments them;
/// code outside the lib uses the <c>IDistributedCache</c> abstractions, not
/// the meter directly.
/// </para>
/// </remarks>
public static class RedisCacheTelemetry
{
    /// <summary>
    /// The OpenTelemetry <see cref="Meter"/> name. Register it with
    /// <c>.WithMetrics(m =&gt; m.AddMeter(RedisCacheTelemetry.METER_NAME))</c>
    /// when wiring telemetry without <c>DcsvIo.D2.Telemetry</c>'s aggregator.
    /// </summary>
    public const string METER_NAME = "DcsvIo.D2.Caching.Distributed.Redis";

    /// <summary>OTel meter for the Redis distributed cache.</summary>
    internal static readonly Meter SR_Meter = new(METER_NAME, "1.0.0");

    /// <summary>Counter incremented on every cache hit.</summary>
    internal static readonly Counter<long> SR_Hits =
        SR_Meter.CreateCounter<long>("d2.cache.redis.hits", unit: "{hit}", description: "Redis cache hits.");

    /// <summary>Counter incremented on every cache miss.</summary>
    internal static readonly Counter<long> SR_Misses =
        SR_Meter.CreateCounter<long>("d2.cache.redis.misses", unit: "{miss}", description: "Redis cache misses.");

    /// <summary>Counter incremented on every cache write.</summary>
    internal static readonly Counter<long> SR_Sets =
        SR_Meter.CreateCounter<long>("d2.cache.redis.sets", unit: "{write}", description: "Redis cache writes.");

    /// <summary>Counter incremented on every explicit removal.</summary>
    internal static readonly Counter<long> SR_Removes = SR_Meter.CreateCounter<long>(
        "d2.cache.redis.removes",
        unit: "{removal}",
        description: "Redis cache removals.");

    /// <summary>Counter incremented on backplane publish.</summary>
    internal static readonly Counter<long> SR_Broadcasts = SR_Meter.CreateCounter<long>(
        "d2.cache.redis.broadcasts",
        unit: "{broadcast}",
        description: "Invalidation messages published to backplane.");

    /// <summary>Counter incremented on RedisException.</summary>
    internal static readonly Counter<long> SR_Errors =
        SR_Meter.CreateCounter<long>("d2.cache.redis.errors", unit: "{error}", description: "Redis-side failures.");
}
