// -----------------------------------------------------------------------
// <copyright file="TelemetryTestHostBuilder.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;

using DcsvIo.D2.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

/// <summary>
/// Static helper that builds an AspNetCore test host with the telemetry
/// wiring in place, for driving the OpenTelemetry SDK end-to-end through
/// <c>Microsoft.AspNetCore.TestHost</c>. Each call creates its own
/// <see cref="InMemoryActivityExporter"/> /
/// <see cref="InMemoryMetricExporter"/> /
/// <see cref="InMemoryLogRecordExporter"/>, so xUnit-parallel test classes
/// do not see each other's emissions. The host's own OpenTelemetry
/// pipeline writes to those exporters through
/// <c>SimpleActivityExportProcessor</c> and <c>SimpleLogRecordExportProcessor</c>,
/// which export each item on the thread that stops the activity or writes
/// the log record, and through
/// <c>BaseExportingMetricReader</c>, which exports when
/// <see cref="ForceFlushAsync"/> collects it. Under TestServer the server
/// activity of a request can stop after the client has its response, so a
/// test waits for a server span with
/// <see cref="TelemetryTestHostHandle.WaitForActivityAsync"/>; a flush does
/// not export an activity that has not stopped.
/// </summary>
internal static class TelemetryTestHostBuilder
{
    /// <summary>
    /// Builds and starts an in-process AspNetCore host with
    /// <c>AddD2Telemetry</c> wired plus the in-memory activity, metric and
    /// log exporters attached. Returns the started host and those exporters.
    /// </summary>
    /// <param name="configureOptions">
    /// Optional callback that changes the <see cref="D2TelemetryOptions"/>
    /// (for example, set <c>OtlpTracesEndpoint</c> to exercise the
    /// self-referential HttpClient filter).
    /// </param>
    /// <param name="extraServices">
    /// Optional callback that changes the services collection after
    /// <c>AddD2Telemetry</c> runs.
    /// </param>
    /// <param name="extraConfigure">
    /// Optional callback that changes the middleware pipeline after
    /// <c>UseRouting</c> and before <c>UseEndpoints</c>.
    /// </param>
    /// <param name="extraEndpoints">
    /// Optional callback that registers endpoints alongside the
    /// default endpoints.
    /// </param>
    /// <returns>
    /// A handle that holds the started host and the in-memory activity,
    /// metric and log exporters.
    /// </returns>
    internal static async Task<TelemetryTestHostHandle> BuildAsync(
        Action<D2TelemetryOptions>? configureOptions = null,
        Action<IServiceCollection>? extraServices = null,
        Action<IApplicationBuilder>? extraConfigure = null,
        Action<IEndpointRouteBuilder>? extraEndpoints = null)
    {
        var activityExporter = new InMemoryActivityExporter();
        var metricExporter = new InMemoryMetricExporter();
        var logExporter = new InMemoryLogRecordExporter();

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();

                        services.AddD2Telemetry(
                            new ConfigurationBuilder().Build(),
                            opts =>
                            {
                                opts.ServiceName = "telemetry-tests";
                                configureOptions?.Invoke(opts);
                            });

                        // Attach the per-test in-memory exporters after
                        // AddD2Telemetry, so they run alongside any OTLP
                        // exporters the library registered from
                        // environment-derived defaults. The processors are
                        // synchronous, so tests do not race the batch flush
                        // deadline.
                        services.ConfigureOpenTelemetryTracerProvider(tracing =>
                            tracing.AddProcessor(
                                new SimpleActivityExportProcessor(activityExporter)));

                        services.ConfigureOpenTelemetryMeterProvider(metrics =>
                            metrics.AddReader(new BaseExportingMetricReader(metricExporter)
                            {
                                TemporalityPreference = MetricReaderTemporalityPreference.Delta,
                            }));

                        services.Configure<OpenTelemetryLoggerOptions>(opts =>
                            opts.AddProcessor(
                                new SimpleLogRecordExportProcessor(logExporter)));

                        extraServices?.Invoke(services);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        extraConfigure?.Invoke(app);

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/api/echo", () => Results.Text("ok"));
                            endpoints.MapGet("/health", () => Results.Text("ok"));
                            endpoints.MapGet("/alive", () => Results.Text("ok"));
                            endpoints.MapGet(
                                "/.well-known/openid-configuration",
                                () => Results.Text("ok"));

                            endpoints.MapD2PrometheusEndpoint();

                            extraEndpoints?.Invoke(endpoints);
                        });
                    });
            });

        var host = await hostBuilder.StartAsync();
        return new TelemetryTestHostHandle(
            host,
            activityExporter,
            metricExporter,
            logExporter);
    }

    /// <summary>
    /// Flushes the tracer provider and the meter provider.
    /// The logs reach their exporter without a flush, because
    /// <c>SimpleLogRecordExportProcessor</c> exports each record on the
    /// calling thread.
    /// </summary>
    /// <param name="host">The host to flush.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A completed task; the flushes run synchronously before it returns.
    /// </returns>
    internal static Task ForceFlushAsync(IHost host, CancellationToken ct = default)
    {
        var sp = host.Services;
        sp.GetService<TracerProvider>()?.ForceFlush(5000);
        sp.GetService<MeterProvider>()?.ForceFlush(5000);
        return Task.CompletedTask;
    }
}
