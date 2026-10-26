// -----------------------------------------------------------------------
// <copyright file="HttpClientInstrumentationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Telemetry;

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;
using Xunit;

/// <summary>
/// Pins the wiring of the HttpClient instrumentation, which filters out outbound
/// calls to configured OTLP exporter endpoints so an export call produces no span
/// that is exported in turn. One test starts a host with the OTLP endpoints
/// configured and asserts that it resolves a <c>TracerProvider</c>; the other
/// sends a request to the OTLP endpoint and one elsewhere, through a loopback
/// listener the test owns, and asserts only the second is exported.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class HttpClientInstrumentationTests
{
    private const string _OTLP_TRACES = "https://otlp.example.test:4318/v1/traces";

    // HttpClient's default HttpClientHandler wraps the socket handler in the
    // diagnostics handler the instrumentation listens to.
    private static readonly HttpClient sr_client = new();

    [Fact]
    public async Task HttpClientInstrumentation_HostStartsCleanly_WithOtlpEndpointsConfigured()
    {
        // AddD2Telemetry wires the HttpClient instrumentation's
        // FilterHttpRequestMessage callback, so a wiring bug (a null
        // reference on the captured delegate, a missing using, and so on)
        // fails host startup here. The in-process TestServer's
        // TestServerHandler and a stubbed HttpMessageHandler both bypass the
        // diagnostic events the instrumentation hooks, so the test below
        // sends real loopback requests. The unit tests in
        // SelfReferentialOtlpRequestFilterTests call the predicate directly.
        await using var handle = await TelemetryTestHostBuilder.BuildAsync(
            opts =>
            {
                opts.OtlpTracesEndpoint = _OTLP_TRACES;
                opts.OtlpLogsEndpoint = "https://otlp.example.test:4318/v1/logs";
                opts.OtlpMetricsEndpoint = "https://otlp.example.test:4318/v1/metrics";
            });

        var tracerProvider = handle.Host.Services
            .GetService(typeof(OpenTelemetry.Trace.TracerProvider));
        tracerProvider.Should().NotBeNull();
    }

    [Fact]
    public async Task HttpClientInstrumentation_RequestToTheOtlpEndpoint_ExportsNoSpan()
    {
        // Both requests below reach a real loopback listener this test owns, and
        // the filter decides whether a span is exported.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serving = ServeAsync(listener, requests: 2);
        var otlpTraces = $"http://127.0.0.1:{port}/v1/traces";

        await using var handle = await TelemetryTestHostBuilder.BuildAsync(
            opts => opts.OtlpTracesEndpoint = otlpTraces);
        handle.Activities.Reset();

        using (await sr_client.GetAsync(new Uri(otlpTraces + "/export")))
        using (await sr_client.GetAsync(new Uri($"http://127.0.0.1:{port}/other")))
        {
        }

        await serving;

        // The trace exporter sends to the same port during the flush. With the listener
        // stopped the connection is refused at once, instead of waiting out the export timeout.
        listener.Stop();
        await TelemetryTestHostBuilder.ForceFlushAsync(handle.Host);

        var exportedUrls = handle.Activities.Snapshot("System.Net.Http")
            .Select(a => a.GetTagItem("url.full") as string)
            .ToList();
        exportedUrls.Should().Equal($"http://127.0.0.1:{port}/other");
    }

    // Answers each of the given number of connections with an empty 200 response.
    private static async Task ServeAsync(TcpListener listener, int requests)
    {
        for (var i = 0; i < requests; i++)
        {
            using var socket = await listener.AcceptTcpClientAsync();
            var stream = socket.GetStream();
            var buffer = new byte[4096];
            _ = await stream.ReadAsync(buffer);
            var response = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8;
            await stream.WriteAsync(response.ToArray());
        }
    }
}
