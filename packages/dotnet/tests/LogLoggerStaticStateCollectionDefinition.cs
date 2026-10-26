// -----------------------------------------------------------------------
// <copyright file="LogLoggerStaticStateCollectionDefinition.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests;

using Xunit;

/// <summary>
/// xUnit collection definitions for test classes that touch process-wide
/// static state - the Serilog <c>Log.Logger</c> singleton and the OTel
/// SDK's <c>MeterProvider</c> / <c>TracerProvider</c> / Prometheus-exporter
/// global registration. All such test classes share the single
/// <c>"LogLoggerStaticState"</c> collection so xUnit serializes them against
/// each other; <c>DisableParallelization = true</c> ensures the collection
/// does not race other collections either.
/// </summary>
/// <remarks>
/// <para>
/// Two separate implicit collections existed before this one:
/// <c>"LogLoggerStaticState"</c> (Serilog and host integration tests)
/// and <c>"OtelStaticState"</c> (OTel SDK / Telemetry unit + integration).
/// Neither had a <c>[CollectionDefinition]</c> declaration, so xUnit ran
/// them in parallel - racing the Prometheus exporter global
/// HttpListener, the process-level MeterProvider registration, and the
/// <c>Log.Logger</c> static facade. The race appeared as intermittent
/// middleware end-to-end test failures under the full parallel suite. The
/// single declared collection with <c>DisableParallelization = true</c>
/// removes the race: all tests in the collection run serially and no other
/// test collection overlaps with them.
/// </para>
/// </remarks>
[CollectionDefinition("LogLoggerStaticState", DisableParallelization = true)]
public sealed class LogLoggerStaticStateCollectionDefinition
{
}
