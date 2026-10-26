// -----------------------------------------------------------------------
// <copyright file="AggregatedSourceRegistrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Telemetry;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using AwesomeAssertions;
using DcsvIo.D2.Telemetry.Internal;
using DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;
using Xunit;

/// <summary>
/// Covers 100% of the <c>ActivitySource</c> and <c>Meter</c> names in
/// <see cref="AggregatedTelemetrySources"/>: for every name, the test emits a
/// synthetic activity or counter and asserts that the in-memory exporter
/// captured it, which shows the OTel SDK is wired to that source or meter.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class AggregatedSourceRegistrationTests
{
    public static IEnumerable<TheoryDataRow<string>> ActivitySourceNames =>
        AggregatedTelemetrySources.SR_ActivitySourceNames
            .Select(name => new TheoryDataRow<string>(name));

    public static IEnumerable<TheoryDataRow<string>> MeterNames =>
        AggregatedTelemetrySources.SR_MeterNames
            .Select(name => new TheoryDataRow<string>(name));

    [Theory]
    [MemberData(nameof(ActivitySourceNames))]
    public async Task EveryAggregatedActivitySource_EmitsAndIsCaptured(string sourceName)
    {
        await using var handle = await TelemetryTestHostBuilder.BuildAsync();

        using (var source = new ActivitySource(sourceName))
        {
            // The explicit name argument replaces the [CallerMemberName]
            // default, which would make the test method name
            // ("EveryAggregated...") the span name. The span is named
            // "aggregation-pin".
            // ReSharper disable once ExplicitCallerInfoArgument
            using var activity = source.StartActivity("aggregation-pin");
            activity?.SetTag("test", "true");
        }

        await TelemetryTestHostBuilder.ForceFlushAsync(handle.Host);

        handle.Activities.Snapshot(sourceName).Should().NotBeEmpty(
            because: $"AggregatedTelemetrySources lists '{sourceName}' "
            + "as wired via AddSource - emitting through that source should "
            + "reach the exporter.");
    }

    [Theory]
    [MemberData(nameof(MeterNames))]
    public async Task EveryAggregatedMeter_EmitsAndIsCaptured(string meterName)
    {
        await using var handle = await TelemetryTestHostBuilder.BuildAsync();

        using (var meter = new Meter(meterName))
        {
            var counter = meter.CreateCounter<long>("aggregation_pin_counter");
            counter.Add(1);
        }

        await TelemetryTestHostBuilder.ForceFlushAsync(handle.Host);

        handle.Metrics.Snapshot(meterName).Should().NotBeEmpty(
            because: $"AggregatedTelemetrySources lists '{meterName}' "
            + "as wired via AddMeter - emitting through that meter should "
            + "reach the exporter.");
    }
}
