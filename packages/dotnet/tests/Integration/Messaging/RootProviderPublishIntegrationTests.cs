// -----------------------------------------------------------------------
// <copyright file="RootProviderPublishIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Pins the "publish from root provider without a wrapping scope" contract
/// - <see cref="IMessageBus"/> is registered as Singleton + builds its own
/// transient scope per <c>PublishAsync</c> to resolve scoped dependencies
/// (keyed crypto, IRequestContext). A publish from the root provider therefore
/// needs no <c>await using var scope = sp.CreateAsyncScope()</c> wrapper.
/// </summary>
[Collection("RabbitMq")]
public sealed class RootProviderPublishIntegrationTests
{
    // Stuck-guard for the async-delivery waits below: a poll-ATTEMPT budget, not
    // a wall-clock deadline. Each iteration awaits pollInterval, so a slow machine
    // lengthens the wait instead of expiring it mid-progress. A healthy delivery
    // resolves in well under a second; exhausting this budget means the message is
    // not arriving at all, so raise the budget only after establishing that the
    // delivery is slow rather than absent. The budget is also what terminates a
    // permanently stuck test - this project ships no framework test timeout.
    private const int _POLL_ATTEMPT_BUDGET = 2400;

    private readonly RabbitMqFixture r_fixture;

    /// <summary>Initializes the test class with the shared fixture.</summary>
    /// <param name="fixture">Testcontainers RabbitMQ.</param>
    public RootProviderPublishIntegrationTests(RabbitMqFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PublishFromRootProvider_NoScopeNeeded_Succeeds()
    {
        var queue = "rootpub." + Guid.NewGuid().ToString("N")[..8];
        TestCollector.Reset<AuditCapturingHandler>();

        await using var host = await MessagingHostBuilder.BuildAndStartAsync(
            r_fixture,
            services =>
            {
                services.AddTransient<AuditCapturingHandler>();
                services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                    IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 1));
            });

        // Resolve the bus DIRECTLY from the root provider. IMessageBus is a
        // Singleton that builds its own transient scope per PublishAsync, so
        // resolving from root does not throw InvalidOperationException.
        var bus = host.Services.GetRequiredService<IMessageBus>();
        var publish = await bus.PublishAsync(
            new IntegrationAuditEvent { Marker = "from-root-sp" });
        publish.Failed.Should().BeFalse(
            "Singleton bus must publish without a wrapping DI scope");

        await WaitFor(
            () => TestCollector.Count<AuditCapturingHandler>() > 0);
        TestCollector.Count<AuditCapturingHandler>().Should().Be(1);
    }

    // Attempt-budgeted stuck-guard - see _POLL_ATTEMPT_BUDGET; no wall-clock deadline.
    private static async Task WaitFor(
        Func<bool> predicate, TimeSpan? pollInterval = null)
    {
        pollInterval ??= TimeSpan.FromMilliseconds(50);
        for (var attempt = 0; attempt < _POLL_ATTEMPT_BUDGET; attempt++)
        {
            if (predicate()) return;
            await Task.Delay(pollInterval.Value);
        }

        throw new TimeoutException(
            $"Predicate did not become true within {_POLL_ATTEMPT_BUDGET} poll attempts.");
    }
}
