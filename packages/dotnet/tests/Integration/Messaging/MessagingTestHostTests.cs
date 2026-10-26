// -----------------------------------------------------------------------
// <copyright file="MessagingTestHostTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Tests.Integration.Messaging.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

/// <summary>
/// Pins that a messaging integration test actually shuts its host down.
/// </summary>
[Collection("RabbitMq")]
public sealed class MessagingTestHostTests
{
    private readonly RabbitMqFixture r_fixture;

    public MessagingTestHostTests(RabbitMqFixture fixture)
    {
        r_fixture = fixture;
    }

    // Disposing the wrapper stops the host before disposing it. Neither
    // IHost.Dispose nor IHost.DisposeAsync runs StopAsync, so a raw IHost fails this.
    [Fact]
    [Trait("Category", "Integration")]
    public async Task DisposeAsync_AfterStart_StopsTheHostBeforeDisposingIt()
    {
        var queue = "host-stop." + Guid.NewGuid().ToString("N")[..8];
        var host = await MessagingHostBuilder.BuildAndStartAsync(
            r_fixture,
            services =>
            {
                services.AddTransient<AuditCapturingHandler>();
                services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                    IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 1));
            });

        // The dispose under test is the explicit call below, not an `await using` -
        // the assertion sits either side of it. The flag tells the finally block
        // whether the host was already disposed, so a failing assertion still
        // disposes the host exactly once.
        var disposedUnderTest = false;
        try
        {
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            lifetime.ApplicationStopped.IsCancellationRequested.Should().BeFalse(
                "the host is still running before the handle is disposed");

            await host.DisposeAsync();
            disposedUnderTest = true;

            lifetime.ApplicationStopped.IsCancellationRequested.Should().BeTrue(
                "disposing the handle must stop the host, which is what cancels the "
                + "subscriber channel and drains its in-flight deliveries");
        }
        finally
        {
            if (!disposedUnderTest)
                await host.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task BuildAndStartAsync_WhenStartupFails_DisposesTheHostItStarted()
    {
        // The stub is disposed only by the host's own service provider, so its
        // disposal shows that the host was disposed after the failed build.
        var disposed = false;

        var act = async () => await MessagingHostBuilder.BuildAndStartAsync(
            r_fixture,
            services => services.AddSingleton<IHostedService>(
                _ => new StartupFailureHostedServiceStub(() => disposed = true)));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage(StartupFailureHostedServiceStub.FAILURE_MESSAGE);

        disposed.Should().BeTrue(
            "a build that throws with the host already started must still stop and "
            + "dispose it, or the connection, its channels and its queues outlive the test");
    }
}
