// -----------------------------------------------------------------------
// <copyright file="DlqRepublishRoutingTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq.Telemetry;
using DcsvIo.D2.Messaging.RabbitMq.Topology;
using DcsvIo.D2.Tests.Integration.Messaging.Fixtures;
using global::RabbitMQ.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Covers the dead-letter republish on the consumer side. Before the fix, a
/// failed message republished to a dead-letter exchange with no queue bound
/// to it was reported as a successful republish, and the library then acked
/// the original, which deleted the last copy of the message although the
/// publish routed nowhere.
/// </summary>
[Collection("RabbitMq")]
public sealed class DlqRepublishRoutingTests
{
    /// <summary>Instrument name of the DLQ republish-failure counter.</summary>
    private const string _DLQ_REPUBLISH_FAILURES =
        "d2.messaging.rabbitmq.dlq_republish_failures";

    // Attempt-budgeted stuck-guard; each iteration awaits, so a slow machine
    // takes longer rather than expiring mid-progress. A healthy run resolves in
    // well under a second.
    private const int _POLL_ATTEMPT_BUDGET = 2400;

    private readonly RabbitMqFixture r_fixture;

    /// <summary>Initializes the test class with the shared fixture.</summary>
    /// <param name="fixture">Testcontainers RabbitMQ.</param>
    public DlqRepublishRoutingTests(RabbitMqFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task HandlerFails_DeadLetterExchangeHasNoQueue_ReportsUnroutableRepublish()
    {
        TestCollector.Reset<AlwaysFailsHandler>();
        var queue = "dlq.route." + Guid.NewGuid().ToString("N")[..8];
        var marker = "no-dlq-" + Guid.NewGuid().ToString("N")[..8];

        using var metrics = new PublishMeasurementCollectorFixture();

        await using var host = await MessagingHostBuilder.BuildAndStartAsync(
            r_fixture,
            services =>
            {
                services.AddTransient<AlwaysFailsHandler>();
                services.AddD2Subscriber<AlwaysFailsHandler, IntegrationAuditEvent>(
                    IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 5));
            });

        // Delete the dead-letter queue after the topology was declared. This is
        // the state of a deployment whose dead-letter queue is missing or was
        // removed by hand.
        await DeleteQueueAsync(DlqNaming.DlqFor(queue));

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            var publish = await bus.PublishAsync(
                new IntegrationAuditEvent { Marker = marker });
            publish.Failed.Should().BeFalse(
                "the message's own exchange is bound; only the dead-letter half is not");
        }

        await WaitFor(() => metrics.TotalFor(
                _DLQ_REPUBLISH_FAILURES,
                MessagingMetricTags.REPUBLISH_FAIL_UNROUTABLE) > 0);

        metrics.TotalFor(
                _DLQ_REPUBLISH_FAILURES,
                MessagingMetricTags.REPUBLISH_FAIL_UNROUTABLE)
            .Should().BeGreaterThanOrEqualTo(1);
        metrics.TotalFor(
                _DLQ_REPUBLISH_FAILURES,
                MessagingMetricTags.REPUBLISH_FAIL_TRANSPORT)
            .Should().Be(0, "the channel was healthy; the routing was not");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task HandlerFails_DeadLetterQueueBound_RepublishReportsNoFailure()
    {
        // The admitting case. Without it, the test above would also pass for a
        // republish path that reports every message as unroutable.
        TestCollector.Reset<AlwaysFailsHandler>();
        var queue = "dlq.route." + Guid.NewGuid().ToString("N")[..8];
        var marker = "with-dlq-" + Guid.NewGuid().ToString("N")[..8];

        using var metrics = new PublishMeasurementCollectorFixture();

        await using var host = await MessagingHostBuilder.BuildAndStartAsync(
            r_fixture,
            services =>
            {
                services.AddTransient<AlwaysFailsHandler>();
                services.AddD2Subscriber<AlwaysFailsHandler, IntegrationAuditEvent>(
                    IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 5));
            });

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            var publish = await bus.PublishAsync(
                new IntegrationAuditEvent { Marker = marker });
            publish.Failed.Should().BeFalse();
        }

        await WaitForQueueCountAsync(DlqNaming.DlqFor(queue), expected: 1);

        metrics.Total(_DLQ_REPUBLISH_FAILURES)
            .Should().Be(0, "the dead-letter queue is bound, so the republish routes");
    }

    private static async Task WaitFor(Func<bool> predicate)
    {
        for (var attempt = 0; attempt < _POLL_ATTEMPT_BUDGET; attempt++)
        {
            if (predicate()) return;
            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }

        throw new TimeoutException(
            $"Predicate did not become true within {_POLL_ATTEMPT_BUDGET} poll attempts.");
    }

    private async Task DeleteQueueAsync(string queueName)
    {
        var factory = new ConnectionFactory { Uri = new Uri(r_fixture.ConnectionString) };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.QueueDeleteAsync(queueName, ifUnused: false, ifEmpty: false);
    }

    private async Task WaitForQueueCountAsync(string queueName, int expected)
    {
        var factory = new ConnectionFactory { Uri = new Uri(r_fixture.ConnectionString) };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        for (var attempt = 0; attempt < _POLL_ATTEMPT_BUDGET; attempt++)
        {
            var declareOk = await channel.QueueDeclarePassiveAsync(queueName);
            if (declareOk.MessageCount >= expected) return;
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        var final = await channel.QueueDeclarePassiveAsync(queueName);
        throw new TimeoutException(
            $"Queue '{queueName}' had {final.MessageCount} messages after "
            + $"{_POLL_ATTEMPT_BUDGET} poll attempts, expected >= {expected}.");
    }
}
