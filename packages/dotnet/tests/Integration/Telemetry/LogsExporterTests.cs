// -----------------------------------------------------------------------
// <copyright file="LogsExporterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Telemetry;

using AwesomeAssertions;
using DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Pins the OTel logs bridge to Microsoft.Extensions.Logging (MEL): a log
/// call on an <c>ILogger&lt;T&gt;</c> resolved from DI passes through the
/// OpenTelemetry MEL provider that <c>AddD2Telemetry</c> registers and
/// reaches the in-memory log exporter.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class LogsExporterTests
{
    [Fact]
    public async Task ILoggerInformation_IsCapturedByExporter()
    {
        await using var handle = await TelemetryTestHostBuilder.BuildAsync();

        var logger = handle.Host.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("LogsExporterTests");

        // Call ILogger through the lowest-level Log overload, which avoids
        // CA1848 and CA1727. Production code uses LoggerMessage
        // source-generated delegates; this test pins the pipeline wiring and
        // not the log-call syntax.
        logger.Log(
            logLevel: LogLevel.Information,
            eventId: 0,
            state: "logs pipeline pin",
            exception: null,
            formatter: (s, _) => s);

        // Disposing the LoggerFactory triggers the shutdown path of the OTel
        // logs processor, which flushes pending records.
        handle.Host.Services.GetRequiredService<ILoggerFactory>().Dispose();

        handle.Logs.Snapshot().Should().NotBeEmpty(
            because: "the logs pipeline should route the ILogger.Log call "
            + "through the OTel MEL provider to the in-memory exporter.");
    }
}
