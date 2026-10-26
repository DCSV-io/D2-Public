// -----------------------------------------------------------------------
// <copyright file="ProductMeterSeamTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Telemetry;

using AwesomeAssertions;
using DcsvIo.D2.Telemetry;
using DcsvIo.D2.Telemetry.Internal;
using DcsvIo.D2.Tests.Unit.Telemetry.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using Xunit;

/// <summary>
/// <c>AddD2Meter</c> registers a service's metrics type and adds its meter name
/// to the provider in one call. Two export cases check this: a type
/// registered through the seam exports, and a type registered as a bare
/// singleton does not export, which is the drift this seam prevents.
/// </summary>
/// <remarks>
/// Neither the D2 gate nor the OTel SDK reads <c>OTEL_SDK_DISABLED</c> from the
/// process environment here: the gate is pinned for the test's own flow, and
/// the container carries an empty configuration the SDK reads in place of the
/// environment. The collection still serializes these cases against the other
/// tests that build a provider.
/// </remarks>
[Collection("LogLoggerStaticState")]
public sealed class ProductMeterSeamTests
{
    /// <summary>
    /// Gets the empty-string row. <see cref="string.Empty"/> is not a
    /// constant, so it cannot be an <c>InlineData</c> argument.
    /// </summary>
    public static TheoryData<string> EmptyStringRows => [string.Empty];

    [Fact]
    public void AddD2Meter_TheMetricsTypeItNames_ResolvesFromTheContainer()
    {
        WithCleanEnv(() =>
        {
            var services = new ServiceCollection();
            services.AddD2Meter<ListeningProductMetricsFixture>(
                ListeningProductMetricsFixture.METER_NAME);

            using var provider = services.BuildServiceProvider();

            provider.GetRequiredService<ListeningProductMetricsFixture>()
                .Should().NotBeNull(
                    "the seam registers the meter factory the type is built from as "
                    + "well as the type itself");
        });
    }

    [Fact]
    public void AddD2Meter_AddsTheMeterNameToTheOptions()
    {
        WithCleanEnv(() =>
        {
            var services = new ServiceCollection();
            services.AddD2Meter<ListeningProductMetricsFixture>(
                ListeningProductMetricsFixture.METER_NAME);

            using var provider = services.BuildServiceProvider();

            provider.GetRequiredService<IOptions<D2TelemetryOptions>>()
                .Value.AdditionalMeters
                .Should().Equal(ListeningProductMetricsFixture.METER_NAME);
        });
    }

    [Fact]
    public void AddD2Meter_TheSameMeterTwice_HoldsOneName()
    {
        WithCleanEnv(() =>
        {
            var services = new ServiceCollection();
            services.AddD2Meter<ListeningProductMetricsFixture>(
                ListeningProductMetricsFixture.METER_NAME);
            services.AddD2Meter<ListeningProductMetricsFixture>(
                ListeningProductMetricsFixture.METER_NAME);

            using var provider = services.BuildServiceProvider();

            provider.GetRequiredService<IOptions<D2TelemetryOptions>>()
                .Value.AdditionalMeters
                .Should().Equal(ListeningProductMetricsFixture.METER_NAME);
            provider.GetServices<ListeningProductMetricsFixture>()
                .Should().ContainSingle("the singleton is registered through TryAdd");
        });
    }

    [Fact]
    public void AddD2Meter_TheMeterItRegistered_ReachesTheExporter()
    {
        ExportedMetrics().Should().Contain(
            (ListeningProductMetricsFixture.METER_NAME,
             "d2.tests.telemetry.listening_events"),
            "the seam is what puts a service's meter name on the provider");
    }

    [Fact]
    public void AddD2Meter_AMeterRegisteredAsABareSingleton_DoesNotReachTheExporter()
    {
        // The negative half of the pair: a provider listening to every meter would
        // pass the case above whether or not the seam did anything.
        ExportedMetrics().Should().NotContain(
            m => m.MeterName == UnlistenedProductMetricsFixture.METER_NAME);
    }

    [Fact]
    public void AddD2Meter_NullServices_Throws()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddD2Meter<ListeningProductMetricsFixture>(
            ListeningProductMetricsFixture.METER_NAME);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [MemberData(nameof(EmptyStringRows))]
    [InlineData("   ")]
    public void AddD2Meter_FalseyMeterName_Throws(string? meterName)
    {
        var services = new ServiceCollection();

        var act = () =>
            services.AddD2Meter<ListeningProductMetricsFixture>(meterName!);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Composes the seam over a container of its own beside a metrics type
    /// registered as a bare singleton, emits one measurement on each, and returns
    /// what the exporter received.
    /// </summary>
    /// <returns>The meter and instrument name of every exported metric.</returns>
    private static IReadOnlyCollection<(string MeterName, string Name)> ExportedMetrics()
    {
        var exporter = new CapturingMetricExporterFixture();
        IReadOnlyCollection<(string MeterName, string Name)> exported = [];

        WithCleanEnv(() =>
        {
            var services = new ServiceCollection();
            IConfiguration cfg = new ConfigurationBuilder().Build();

            services.AddSingleton(cfg);
            services.AddD2Telemetry(cfg, opts => opts.ServiceName = "product-meter-seam");
            services.AddD2Meter<ListeningProductMetricsFixture>(
                ListeningProductMetricsFixture.METER_NAME);

            // The drift: the type is registered, but its meter name is not added
            // to the provider.
            services.AddMetrics();
            services.AddSingleton<UnlistenedProductMetricsFixture>();

            services.ConfigureOpenTelemetryMeterProvider(metrics =>
                metrics.AddReader(new BaseExportingMetricReader(exporter)
                {
                    TemporalityPreference = MetricReaderTemporalityPreference.Delta,
                }));

            using var provider = services.BuildServiceProvider();
            var meterProvider = provider.GetRequiredService<MeterProvider>();

            provider.GetRequiredService<ListeningProductMetricsFixture>().RecordEvent();
            provider.GetRequiredService<UnlistenedProductMetricsFixture>().RecordEvent();

            meterProvider.ForceFlush();

            exported = exporter.Snapshot();
        });

        return exported;
    }

    private static void WithCleanEnv(Action body)
    {
        using var unset = OtelSdkDisabledGate.Pin(null);

        body();
    }
}
