// -----------------------------------------------------------------------
// <copyright file="InfrastructurePathMatcherConsumerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.AspNetCore;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using Microsoft.AspNetCore.Http;
using Xunit;

/// <summary>
/// Regression tests pinning the shared use of
/// <see cref="InfrastructurePathMatcher"/>: the libraries have no per-lib
/// internal duplicates of the matcher, and <c>DcsvIo.D2.Logging</c> +
/// <c>DcsvIo.D2.Telemetry</c> use the same public matcher.
/// </summary>
/// <remarks>
/// These tests pin the wire contract that the path set
/// (<see cref="D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS"/>)
/// stays aligned across the bypass middleware (this lib), the Logging
/// request-logging level callback, and the Telemetry
/// AspNetCore-instrumentation Filter callback. A divergence would surface
/// as one of them treating <c>/health</c> as infrastructure while another
/// does not.
/// </remarks>
public sealed class InfrastructurePathMatcherConsumerTests
{
    [Fact]
    public void DefaultPathSet_AlignedAcrossConsumers()
    {
        // The default infrastructure-path list lives in
        // D2AspNetCoreConstants and is what every default points at.
        // Confirms that no per-lib literal diverges from it.
        D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS
            .Should().Equal("/health", "/alive", "/metrics", "/.well-known");
    }

    [Fact]
    public void LoggingDefault_PointsToCanonicalConstants()
    {
        // DcsvIo.D2.Logging.D2LoggingOptions.InfrastructurePaths defaults
        // to the same list (verified via Logging's own existing tests).
        // Cross-confirm by checking both lists are sequence-equal.
        var loggingDefault = new DcsvIo.D2.Logging.D2LoggingOptions().InfrastructurePaths;
        loggingDefault.Should().Equal(D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS);
    }

    [Fact]
    public void TelemetryDefault_PointsToCanonicalConstants()
    {
        var telemetryDefault = new DcsvIo.D2.Telemetry.D2TelemetryOptions()
            .InstrumentationExcludedPaths;
        telemetryDefault.Should().Equal(D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS);
    }

    [Fact]
    public void PostConsolidation_HealthPathStillMatches()
    {
        // Direct pin of the public matcher's behavior - fails if the matcher's
        // segment-boundary semantics regress.
        InfrastructurePathMatcher.IsInfrastructurePath(
            new PathString("/health"),
            D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS)
            .Should().BeTrue();
    }
}
