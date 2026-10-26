// -----------------------------------------------------------------------
// <copyright file="D2TelemetryOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Telemetry;

using AwesomeAssertions;
using DcsvIo.D2.Telemetry;
using Xunit;

public sealed class D2TelemetryOptionsTests
{
    [Fact]
    public void ParameterlessCtor_AllDefaultsApplied()
    {
        var opts = new D2TelemetryOptions();

        opts.ServiceName.Should().BeNull();
        opts.OtlpTracesEndpoint.Should().BeNull();
        opts.OtlpMetricsEndpoint.Should().BeNull();
        opts.OtlpLogsEndpoint.Should().BeNull();
        opts.InstrumentationExcludedPaths.Should().BeEquivalentTo(
            "/health", "/alive", "/metrics", "/.well-known");
        opts.AdditionalActivitySources.Should().BeEmpty();
        opts.AdditionalMeters.Should().BeEmpty();
        opts.EnableAspNetCoreInstrumentation.Should().BeTrue();
        opts.EnableHttpClientInstrumentation.Should().BeTrue();
        opts.EnableGrpcNetClientInstrumentation.Should().BeTrue();
        opts.EnableProcessInstrumentation.Should().BeTrue();
        opts.EnableRuntimeInstrumentation.Should().BeTrue();
        opts.EnablePrometheusExporter.Should().BeTrue();
    }

    [Fact]
    public void WithExpression_OverridesSingleField_PreservesOthers()
    {
        var baseline = new D2TelemetryOptions();

        var overridden = baseline with { EnablePrometheusExporter = false };

        overridden.EnablePrometheusExporter.Should().BeFalse();
        overridden.EnableAspNetCoreInstrumentation.Should().BeTrue();
        overridden.EnableHttpClientInstrumentation.Should().BeTrue();
        overridden.InstrumentationExcludedPaths
            .Should().BeSameAs(baseline.InstrumentationExcludedPaths);
    }

    [Fact]
    public void Setters_AllowConfigureCallbackMutation_OnNullableFields()
    {
        var opts = new D2TelemetryOptions
        {
            ServiceName = null,
            OtlpTracesEndpoint = null,
        };

        // The settable nullable fields exist so the
        // AddD2Telemetry(Action<D2TelemetryOptions>) configure delegate
        // can populate values after the DI container constructs the
        // options instance.
        opts.ServiceName = "edge";
        opts.OtlpTracesEndpoint = "https://otlp.example.com/v1/traces";

        opts.ServiceName.Should().Be("edge");
        opts.OtlpTracesEndpoint.Should().Be("https://otlp.example.com/v1/traces");
    }

    [Fact]
    public void InstrumentationExcludedPaths_Override_AppliesNewList()
    {
        var custom = new[] { "/internal", "/probe" };

        var opts = new D2TelemetryOptions { InstrumentationExcludedPaths = custom };

        opts.InstrumentationExcludedPaths.Should().BeSameAs(custom);
    }

    [Fact]
    public void AdditionalActivitySources_Override_AppliesNewList()
    {
        var custom = new[] { "Sample.Product", "Sample.Product.Auth" };

        var opts = new D2TelemetryOptions { AdditionalActivitySources = custom };

        opts.AdditionalActivitySources.Should().BeEquivalentTo("Sample.Product", "Sample.Product.Auth");
    }

    [Fact]
    public void AdditionalMeters_Override_AppliesNewSet()
    {
        var custom = new HashSet<string>(StringComparer.Ordinal)
        {
            "Sample.Product",
            "Sample.Product.Auth",
        };

        var opts = new D2TelemetryOptions { AdditionalMeters = custom };

        opts.AdditionalMeters.Should().BeEquivalentTo("Sample.Product", "Sample.Product.Auth");
    }

    [Fact]
    public void AdditionalMeters_AddedAfterConstruction_HoldsEachNameOnce()
    {
        // The set is mutable so a configure callback can add to it after the
        // container has built the instance; adding a name twice adds one name.
        var opts = new D2TelemetryOptions();

        opts.AdditionalMeters.Add("Sample.Product.Auth");
        opts.AdditionalMeters.Add("Sample.Product.Auth");
        opts.AdditionalMeters.Add("Sample.Product.Keys");

        opts.AdditionalMeters.Should().BeEquivalentTo(
            "Sample.Product.Auth", "Sample.Product.Keys");
    }

    [Fact]
    public void AdditionalMeters_NamesDifferingInCase_AreDifferentNames()
    {
        var opts = new D2TelemetryOptions();

        opts.AdditionalMeters.Add("Sample.Product.Auth");
        opts.AdditionalMeters.Add("sample.product.auth");

        opts.AdditionalMeters.Should().HaveCount(2);
    }

    [Fact]
    public void Sealed_CannotInherit()
    {
        typeof(D2TelemetryOptions).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void RecordEquality_SameInitValues_AreEqual()
    {
        var paths = new[] { "/x" };
        var sources = new[] { "S" };
        var meters = new HashSet<string>(StringComparer.Ordinal) { "M" };

        var a = new D2TelemetryOptions
        {
            InstrumentationExcludedPaths = paths,
            AdditionalActivitySources = sources,
            AdditionalMeters = meters,
        };
        var b = new D2TelemetryOptions
        {
            InstrumentationExcludedPaths = paths,
            AdditionalActivitySources = sources,
            AdditionalMeters = meters,
        };

        // Records compare by-value on init properties; settable nullable
        // fields participate too. Both sides hold same default Enable*
        // toggles + same list references so equality holds.
        a.Should().Be(b);
    }

    [Fact]
    public void DefaultExcludedPaths_Count_IsFour()
    {
        var opts = new D2TelemetryOptions();

        opts.InstrumentationExcludedPaths.Should().HaveCount(4);
    }
}
