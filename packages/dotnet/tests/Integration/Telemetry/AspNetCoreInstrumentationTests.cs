// -----------------------------------------------------------------------
// <copyright file="AspNetCoreInstrumentationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Telemetry;

using AwesomeAssertions;
using DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.AspNetCore.TestHost;
using Xunit;

/// <summary>
/// Pins the AspNetCore-instrumentation contract: business endpoints emit inbound
/// HTTP spans; infrastructure endpoints (<c>/health</c>, <c>/alive</c>,
/// <c>/metrics</c>, <c>/.well-known/*</c>) are filtered out and emit zero spans.
/// The negative-regression tests request an excluded path and assert that no
/// span is emitted for it, so every CI run exercises the suppression.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class AspNetCoreInstrumentationTests
{
    [Fact]
    public async Task ApiEcho_Request_EmitsInboundSpanForRequestedPath()
    {
        await using var handle = await TelemetryTestHostBuilder.BuildAsync();
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync(
            new Uri("/api/echo", UriKind.Relative),
            TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.Should().BeTrue();

        // Filter on the specific request path, because the OTel SDK's
        // process-wide listener can deliver spans emitted by other
        // integration tests that share the same listener collection.
        var captured = await handle.WaitForActivityAsync(
            a => IsServerSpanFor(a, "/api/echo"),
            TestContext.Current.CancellationToken);
        var serverActivities = captured
            .Where(a => IsServerSpanFor(a, "/api/echo"))
            .ToList();

        serverActivities.Should().NotBeEmpty(
            because: "AspNetCore inbound instrumentation should emit a "
            + "Server-kind activity tagged with url.path=/api/echo.");
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    [InlineData("/.well-known/openid-configuration")]
    public async Task InfrastructurePath_Request_EmitsZeroInboundSpansForThatPath(string path)
    {
        await using var handle = await TelemetryTestHostBuilder.BuildAsync();
        var client = handle.Host.GetTestClient();

        await client.GetAsync(
            new Uri(path, UriKind.Relative),
            TestContext.Current.CancellationToken);

        // The server span of a request can be exported after the client has
        // its response. A span for the excluded path would stop before the
        // span of the included request sent after it, so the assertion
        // below runs once the included span has been exported.
        await client.GetAsync(
            new Uri("/api/echo", UriKind.Relative),
            TestContext.Current.CancellationToken);
        var captured = await handle.WaitForActivityAsync(
            a => IsServerSpanFor(a, "/api/echo"),
            TestContext.Current.CancellationToken);
        captured.Should().Contain(
            a => IsServerSpanFor(a, "/api/echo"),
            because: "the included request's span marks the point after which "
            + "a span for the excluded path would have been exported.");

        var serverActivities = captured
            .Where(a => IsServerSpanFor(a, path))
            .ToList();

        serverActivities.Should().BeEmpty(
            because: $"InstrumentationExcludedPaths covers '{path}' so the "
            + "AspNetCore Filter callback should suppress span emission "
            + "for requests targeting that path.");
    }

    private static bool IsServerSpanFor(System.Diagnostics.Activity activity, string path) =>
        activity.Kind == System.Diagnostics.ActivityKind.Server && MatchesPath(activity, path);

    private static bool MatchesPath(System.Diagnostics.Activity activity, string path)
    {
        var tag = activity.GetTagItem("url.path") as string
            ?? activity.GetTagItem("http.target") as string;
        if (tag.Falsey())
            return false;

        return tag!.StartsWith(path, StringComparison.OrdinalIgnoreCase);
    }
}
