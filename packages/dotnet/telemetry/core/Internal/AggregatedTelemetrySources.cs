// -----------------------------------------------------------------------
// <copyright file="AggregatedTelemetrySources.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry.Internal;

using DcsvIo.D2.Caching.Distributed.Redis;
using DcsvIo.D2.Caching.Local.Default;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Messaging.RabbitMq.Telemetry;

/// <summary>
/// Single source of truth for the
/// <see cref="System.Diagnostics.ActivitySource"/> /
/// <see cref="System.Diagnostics.Metrics.Meter"/> name aggregation that
/// <see cref="TelemetryServiceCollectionExtensions.AddD2Telemetry"/>
/// registers with the tracer / meter providers.
/// </summary>
/// <remarks>
/// <para>
/// Entries from public packages bind via published
/// <c>const string</c> symbols (compile-time rename safety). The
/// <c>"DcsvIo.D2.Auth"</c> and <c>"DcsvIo.D2.Auth.Outbound"</c> entries are
/// **literal** OTel wire names of components outside the public packages, kept so the
/// Tempo / Prometheus series labels stay continuous; they have no NuGet
/// PackageId or ProjectReference.
/// </para>
/// <para>
/// Spec-pinning unit tests assert the literal wire values
/// (<c>"DcsvIo.D2.Handler"</c>, <c>"DcsvIo.D2.Auth"</c>, etc.) so a const
/// symbol rename to a different value does not silently change the wire
/// format.
/// </para>
/// <para>
/// Aggregated sources / meters:
/// <list type="bullet">
///  <item><c>HandlerTelemetry.SourceName</c> (public symbol)</item>
///  <item>Wire name <c>"DcsvIo.D2.Auth"</c> (literal)</item>
///  <item>Wire name <c>"DcsvIo.D2.Auth.Outbound"</c> (literal)</item>
///  <item><c>MessagingTelemetry.SOURCE_NAME</c> (public symbol)</item>
///  <item><c>RedisCacheTelemetry.METER_NAME</c> / <c>LocalCacheTelemetry.METER_NAME</c></item>
/// </list>
/// </para>
/// </remarks>
internal static class AggregatedTelemetrySources
{
    /// <summary>
    /// OTel wire ActivitySource / Meter name of a component outside the
    /// public packages. Must stay equal to the ActivitySource and Meter names that the
    /// component registers.
    /// </summary>
    internal const string AUTH_WIRE_NAME = "DcsvIo.D2.Auth";

    /// <summary>
    /// OTel wire ActivitySource / Meter name of a second component outside
    /// the public packages. Must stay equal to the ActivitySource and Meter names that
    /// the component registers.
    /// </summary>
    internal const string AUTH_OUTBOUND_WIRE_NAME = "DcsvIo.D2.Auth.Outbound";

    /// <summary>
    /// The aggregated set of <see cref="System.Diagnostics.ActivitySource"/>
    /// names registered with the tracer provider so spans from those libs
    /// flow to the OTLP traces exporter.
    /// </summary>
    internal static readonly IReadOnlyList<string> SR_ActivitySourceNames =
    [
        HandlerTelemetry.SourceName,
        AUTH_WIRE_NAME,
        AUTH_OUTBOUND_WIRE_NAME,
        MessagingTelemetry.SOURCE_NAME,
    ];

    /// <summary>
    /// The aggregated set of <see cref="System.Diagnostics.Metrics.Meter"/>
    /// names registered with the meter provider so metrics from those libs
    /// flow to the OTLP / Prometheus metrics exporters.
    /// </summary>
    /// <remarks>
    /// Cache-lib meters
    /// (<see cref="RedisCacheTelemetry.METER_NAME"/> +
    /// <see cref="LocalCacheTelemetry.METER_NAME"/>) appear in the meter
    /// list but not in the tracer-source list - the cache libs publish
    /// counters only; no spans.
    /// </remarks>
    internal static readonly IReadOnlyList<string> SR_MeterNames =
    [
        HandlerTelemetry.SourceName,
        AUTH_WIRE_NAME,
        AUTH_OUTBOUND_WIRE_NAME,
        MessagingTelemetry.SOURCE_NAME,
        RedisCacheTelemetry.METER_NAME,
        LocalCacheTelemetry.METER_NAME,
    ];
}
