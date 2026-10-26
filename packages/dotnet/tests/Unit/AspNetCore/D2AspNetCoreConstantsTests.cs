// -----------------------------------------------------------------------
// <copyright file="D2AspNetCoreConstantsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AspNetCore;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using Xunit;

/// <summary>
/// Tests that pin each value of <see cref="D2AspNetCoreConstants"/>. The wire
/// format is the contract: operators point Kubernetes liveness probes at
/// <c>/health</c> and <c>/alive</c>, and renaming either path without a
/// failing test would break those probes. The same applies to header names,
/// which browsers and CDNs see, and to config keys, because environment
/// variables are observable from outside the binary.
/// </summary>
public sealed class D2AspNetCoreConstantsTests
{
    [Fact]
    public void HEALTH_ENDPOINT_PATH_PinsCanonicalLiteral()
    {
        D2AspNetCoreConstants.HEALTH_ENDPOINT_PATH.Should().Be("/health");
    }

    [Fact]
    public void ALIVE_ENDPOINT_PATH_PinsCanonicalLiteral()
    {
        D2AspNetCoreConstants.ALIVE_ENDPOINT_PATH.Should().Be("/alive");
    }

    [Fact]
    public void METRICS_ENDPOINT_PATH_PinsCanonicalLiteral()
    {
        D2AspNetCoreConstants.METRICS_ENDPOINT_PATH.Should().Be("/metrics");
    }

    [Fact]
    public void WELL_KNOWN_ENDPOINT_PATH_PinsCanonicalLiteral()
    {
        D2AspNetCoreConstants.WELL_KNOWN_ENDPOINT_PATH.Should().Be("/.well-known");
    }

    [Fact]
    public void LIVE_HEALTH_TAG_PinsCanonicalLiteral()
    {
        D2AspNetCoreConstants.LIVE_HEALTH_TAG.Should().Be("live");
    }

    [Fact]
    public void SELF_HEALTH_CHECK_NAME_PinsCanonicalLiteral()
    {
        D2AspNetCoreConstants.SELF_HEALTH_CHECK_NAME.Should().Be("self");
    }

    [Fact]
    public void CORS_ORIGINS_CONFIG_KEY_PinsCanonicalLiteral()
    {
        D2AspNetCoreConstants.CORS_ORIGINS_CONFIG_KEY.Should().Be("D2_CORS_ORIGINS");
    }

    [Fact]
    public void DEFAULT_CORS_POLICY_NAME_PinsCanonicalLiteral()
    {
        D2AspNetCoreConstants.DEFAULT_CORS_POLICY_NAME.Should().Be("D2_DEFAULT");
    }

    [Fact]
    public void MAX_CORRELATION_ID_LENGTH_PinsCanonicalLiteral()
    {
        D2AspNetCoreConstants.MAX_CORRELATION_ID_LENGTH.Should().Be(128);
    }

    [Fact]
    public void INFRASTRUCTURE_HTTP_CONTEXT_ITEM_KEY_PinsCanonicalLiteral()
    {
        D2AspNetCoreConstants.INFRASTRUCTURE_HTTP_CONTEXT_ITEM_KEY
            .Should().Be("D2.IsInfrastructure");
    }

    [Fact]
    public void DEFAULT_INFRASTRUCTURE_PATHS_ContainsCanonicalSet()
    {
        D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS
            .Should().Equal("/health", "/alive", "/metrics", "/.well-known");
    }

    [Fact]
    public void DEFAULT_INFRASTRUCTURE_PATHS_ReusesPathConstants()
    {
        // The default list must be composed of the path constants and not of
        // parallel literals, so that renaming a constant updates the list.
        // This test asserts that the list contains the value of each constant.
        D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS
            .Should().Contain(D2AspNetCoreConstants.HEALTH_ENDPOINT_PATH)
            .And.Contain(D2AspNetCoreConstants.ALIVE_ENDPOINT_PATH)
            .And.Contain(D2AspNetCoreConstants.METRICS_ENDPOINT_PATH)
            .And.Contain(D2AspNetCoreConstants.WELL_KNOWN_ENDPOINT_PATH);
    }
}
