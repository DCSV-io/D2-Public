// -----------------------------------------------------------------------
// <copyright file="UnroutablePublishTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using System;
using System.Net;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.ErrorCodes.Category;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq.Channels;
using DcsvIo.D2.Messaging.RabbitMq.Errors;
using DcsvIo.D2.Messaging.RabbitMq.Publishing;
using DcsvIo.D2.Messaging.RabbitMq.Telemetry;
using DcsvIo.D2.Tests.Integration.Messaging.Fixtures;
using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Pins what a publish that matches no queue does. The broker drops and
/// confirms such a publish unless it went out mandatory, which is how a
/// message could be reported as published and be nowhere.
/// </summary>
[Collection("RabbitMq")]
public sealed class UnroutablePublishTests
{
    // A returned publish surfaces in the confirm await, so the wait below is
    // for a broker round trip rather than for a delivery. The budget exists to
    // terminate a permanently stuck test - this project ships no framework
    // test timeout - and a healthy run never reaches it.
    private const int _POLL_ATTEMPT_BUDGET = 2400;

    private readonly RabbitMqFixture r_fixture;

    /// <summary>Initializes the test class with the shared fixture.</summary>
    /// <param name="fixture">Testcontainers RabbitMQ.</param>
    public UnroutablePublishTests(RabbitMqFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PublishAsync_BindingAppearsAfterFirstReturn_SucceedsOnRetry()
    {
        // This is the cross-process residual the topology fix could not close:
        // two hosts declaring the same binding at once can leave a window where
        // bind-ok has been answered and the exchange still routes nothing. The
        // window is reproduced directly here by binding nothing until after the
        // broker has returned the first attempt.
        var exchange = "d2.test.unroutable." + Guid.NewGuid().ToString("N")[..8];
        var queue = "unroutable." + Guid.NewGuid().ToString("N")[..8];
        const string routing_key = "probe";

        await DeclareExchangeAsync(exchange);
        try
        {
            using var metrics = new PublishMeasurementCollectorFixture();
            await using var host = await MessagingHostBuilder.BuildAndStartAsync(r_fixture);
            var bus = host.Services.GetRequiredService<IMessageBus>();

            // Bind on the signal the publisher itself emits, so the binding
            // lands after a return has actually happened rather than after a
            // guessed number of milliseconds. The wait is bounded: a publisher
            // that reports no return at all - which is what publishing without
            // mandatory does - has to fail the assertions below rather than
            // leave this task waiting forever. Started here rather than captured, so
            // the binder holds the task and not the collector the outer scope
            // disposes.
            var firstReturn = metrics.WaitForFirstReturnAsync();
            var binder = Task.Run(async () =>
            {
                if (await firstReturn)
                    await BindQueueAsync(exchange, queue, routing_key);
            });

            var result = await bus.PublishAsync(
                new IntegrationPlaintextEvent { Marker = "returned-then-bound" },
                new PublisherOptions { Exchange = exchange, RoutingKey = routing_key });

            await binder;

            result.Success.Should().BeTrue(
                "the binding appeared inside the retry window, so a later attempt routes");
            metrics.Total(PublishMeasurementCollectorFixture.PUBLISH_RETURNS)
                .Should().BeGreaterThanOrEqualTo(1);
            metrics.Total(PublishMeasurementCollectorFixture.PUBLISH_RETRIES)
                .Should().BeGreaterThanOrEqualTo(1);
        }
        finally
        {
            await DeleteTopologyAsync(exchange, queue);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PublishAsync_NothingEverBoundToExchange_FailsUnroutable()
    {
        // MaxAttempts is overridden down from the shipped default so the test
        // exhausts the retries in one backoff step. The terminal path it
        // reaches is the same one the default reaches.
        var exchange = "d2.test.unroutable." + Guid.NewGuid().ToString("N")[..8];

        await DeclareExchangeAsync(exchange);
        try
        {
            using var metrics = new PublishMeasurementCollectorFixture();
            await using var host = await MessagingHostBuilder.BuildAndStartAsync(r_fixture);
            var bus = host.Services.GetRequiredService<IMessageBus>();

            var result = await bus.PublishAsync(
                new IntegrationPlaintextEvent { Marker = "never-routable" },
                new PublisherOptions
                {
                    Exchange = exchange,
                    RoutingKey = "probe",
                    MaxAttempts = 2,
                });

            result.Failed.Should().BeTrue();
            result.ErrorCode.Should().Be(
                MessagingErrorCodes.LIB_MESSAGING_PUBLISH_UNROUTABLE,
                "an operator has to be able to tell this from an unreachable broker");
            result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            result.Category.Should().Be(ErrorCategory.InfrastructureUnavailable);

            metrics.TotalFor(
                    PublishMeasurementCollectorFixture.PUBLISH_FAILURES,
                    MessagingMetricTags.PUBLISH_FAIL_UNROUTABLE)
                .Should().Be(1);
            metrics.TotalFor(
                    PublishMeasurementCollectorFixture.PUBLISH_FAILURES,
                    MessagingMetricTags.PUBLISH_FAIL_TRANSPORT)
                .Should().Be(0);
            metrics.Total(PublishMeasurementCollectorFixture.PUBLISH_RETURNS)
                .Should().Be(2, "every attempt was returned");
        }
        finally
        {
            await DeleteTopologyAsync(exchange, queue: null);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PublishAsync_QueueBoundBeforePublish_TakesNoReturn()
    {
        // The admitting case. Without it, a broken publisher that returned every
        // message would pass the tests above.
        var exchange = "d2.test.unroutable." + Guid.NewGuid().ToString("N")[..8];
        var queue = "unroutable." + Guid.NewGuid().ToString("N")[..8];
        const string routing_key = "probe";

        await DeclareExchangeAsync(exchange);
        try
        {
            await BindQueueAsync(exchange, queue, routing_key);

            using var metrics = new PublishMeasurementCollectorFixture();
            await using var host = await MessagingHostBuilder.BuildAndStartAsync(r_fixture);
            var bus = host.Services.GetRequiredService<IMessageBus>();

            var result = await bus.PublishAsync(
                new IntegrationPlaintextEvent { Marker = "routable" },
                new PublisherOptions { Exchange = exchange, RoutingKey = routing_key });

            result.Success.Should().BeTrue();
            metrics.Total(PublishMeasurementCollectorFixture.PUBLISH_RETURNS)
                .Should().Be(0);
            metrics.Total(PublishMeasurementCollectorFixture.PUBLISH_FAILURES)
                .Should().Be(0);
        }
        finally
        {
            await DeleteTopologyAsync(exchange, queue);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PublishAsync_ConfirmsDisabled_CountsTheReturnItCannotFail()
    {
        // With publisher confirmations off there is no sequence number to match
        // the return to and no publish left to fail, so the pool's listener is
        // the only code that can report the message as gone. The publish still
        // reports success, which is what that configuration means.
        var exchange = "d2.test.unroutable." + Guid.NewGuid().ToString("N")[..8];

        await DeclareExchangeAsync(exchange);
        try
        {
            using var metrics = new PublishMeasurementCollectorFixture();
            await using var host = await MessagingHostBuilder.BuildAndStartAsync(
                r_fixture,
                services =>
                {
                    // Both together: the composition-time check rejects
                    // WaitForConfirm on a pool that has confirms off.
                    services.Configure<ChannelPoolOptions>(
                        o => o.PublisherConfirmsEnabled = false);
                    services.Configure<RabbitMqPublisherOptions>(
                        o => o.WaitForConfirm = false);
                });

            var bus = host.Services.GetRequiredService<IMessageBus>();
            var result = await bus.PublishAsync(
                new IntegrationPlaintextEvent { Marker = "fire-and-forget" },
                new PublisherOptions { Exchange = exchange, RoutingKey = "probe" });

            result.Success.Should().BeTrue(
                "without confirmations the publish cannot learn its own outcome");

            (await metrics.WaitForFirstReturnAsync()).Should().BeTrue(
                "the pool's basic.return listener reports the return");
            metrics.Total(PublishMeasurementCollectorFixture.PUBLISH_RETURNS)
                .Should().BeGreaterThanOrEqualTo(1);
        }
        finally
        {
            await DeleteTopologyAsync(exchange, queue: null);
        }
    }

    private static async Task WaitForRoutingAsync(
        IChannel channel, string exchange, string queue, string routingKey)
    {
        for (var attempt = 0; attempt < _POLL_ATTEMPT_BUDGET; attempt++)
        {
            try
            {
                await channel.BasicPublishAsync(
                    exchange: exchange,
                    routingKey: routingKey,
                    mandatory: true,
                    basicProperties: new BasicProperties(),
                    body: ReadOnlyMemory<byte>.Empty);

                // It routed. Drain the probe so the queue holds only what the
                // current test put there.
                await channel.QueuePurgeAsync(queue);
                return;
            }
            catch (PublishException pex) when (pex.IsReturn)
            {
                await Task.Delay(25);
            }
        }

        throw new TimeoutException(
            $"{exchange} did not route to {queue} within {_POLL_ATTEMPT_BUDGET} attempts.");
    }

    private Task DeclareExchangeAsync(string exchange) =>
        WithChannelAsync(channel => channel.ExchangeDeclareAsync(
            exchange: exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null));

    private Task BindQueueAsync(string exchange, string queue, string routingKey) =>
        WithChannelAsync(
            async channel =>
            {
                await channel.QueueDeclareAsync(
                    queue: queue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);
                await channel.QueueBindAsync(
                    queue: queue,
                    exchange: exchange,
                    routingKey: routingKey,
                    arguments: null);

                // A bind-ok can be answered before the topic index the router reads
                // has been written. Wait until the broker actually routes to the
                // queue before letting the publisher conclude the binding is there.
                await WaitForRoutingAsync(channel, exchange, queue, routingKey);
            },
            withConfirms: true);

    private Task DeleteTopologyAsync(string exchange, string? queue) =>
        WithChannelAsync(async channel =>
        {
            if (queue is not null)
                await channel.QueueDeleteAsync(queue, ifUnused: false, ifEmpty: false);

            await channel.ExchangeDeleteAsync(exchange, ifUnused: false);
        });

    private async Task WithChannelAsync(
        Func<IChannel, Task> action, bool withConfirms = false)
    {
        var factory = new ConnectionFactory { Uri = new Uri(r_fixture.ConnectionString) };
        await using var connection = await factory.CreateConnectionAsync();
        var options = withConfirms
            ? new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true)
            : null;
        await using var channel = await connection.CreateChannelAsync(options);
        await action(channel);
    }
}
