// -----------------------------------------------------------------------
// <copyright file="DlqTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Headers.Amqp;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq.Topology;
using DcsvIo.D2.Tests.Integration.Messaging.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Xunit;

/// <summary>
/// Adversarial DLQ coverage. Handlers fail in different ways and we verify
/// the message lands in the per-queue DLQ. Uses a raw RabbitMQ.Client
/// connection to inspect the DLQ from outside the test host.
/// </summary>
[Collection("RabbitMq")]
public sealed class DlqTests
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

    public DlqTests(RabbitMqFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task HandlerThrows_MessageGoesToDlq()
    {
        TestCollector.Reset<AlwaysThrowsHandler>();
        var queue = "dlq.thr." + Guid.NewGuid().ToString("N")[..8];
        var marker = "boom-" + Guid.NewGuid().ToString("N")[..8];

        await using var host = await StartHostAsync(services =>
        {
            services.AddTransient<AlwaysThrowsHandler>();
            services.AddD2Subscriber<AlwaysThrowsHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 5));
        });

        // The host registers the fixture catalog, so the fixture descriptors
        // resolve for this publish.

        // Publish - handler throws - message gets nack'd to DLQ.
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            var publish = await bus.PublishAsync(
                new IntegrationAuditEvent { Marker = marker });
            publish.Failed.Should().BeFalse(
                "publish must succeed before waiting for handler/DLQ delivery");
        }

        // Wait for THIS test's delivery (marker-filtered; Count alone is racy
        // against collector Reset and ConcurrentBag.Count approximation).
        await WaitFor(
            () => TestCollector
                .Captured<AlwaysThrowsHandler, IntegrationAuditEvent>()
                .Any(m => m.Marker == marker),
            detail: () =>
                $"HandlerThrows marker={marker}; queue={queue}; "
                + $"collectorCount={TestCollector.Count<AlwaysThrowsHandler>()}; "
                + $"markers=[{string.Join(",", TestCollector
                    .Captured<AlwaysThrowsHandler, IntegrationAuditEvent>()
                    .Select(m => m.Marker ?? "<null>"))}]");

        // Wait for the message to surface in the DLQ. RabbitMQ's
        // dead-lettering is asynchronous from the consumer's NACK.
        var dlqName = DlqNaming.DlqFor(queue);
        await WaitForQueueCount(dlqName, expected: 1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task HandlerReturnsFailure_MessageGoesToDlq()
    {
        TestCollector.Reset<AlwaysFailsHandler>();
        var queue = "dlq.fail." + Guid.NewGuid().ToString("N")[..8];
        var marker = "fail-result-" + Guid.NewGuid().ToString("N")[..8];

        await using var host = await StartHostAsync(services =>
        {
            services.AddTransient<AlwaysFailsHandler>();
            services.AddD2Subscriber<AlwaysFailsHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 5));
        });

        // The host registers the fixture catalog, so the fixture descriptors
        // resolve for this publish.

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            var publish = await bus.PublishAsync(
                new IntegrationAuditEvent { Marker = marker });
            publish.Failed.Should().BeFalse(
                "publish must succeed before waiting for handler/DLQ delivery");
        }

        // Marker-filtered wait: Count alone can race with collector Reset if a
        // prior host's late delivery lands on AlwaysFailsHandler after Reset.
        await WaitFor(
            () => TestCollector
                .Captured<AlwaysFailsHandler, IntegrationAuditEvent>()
                .Any(m => m.Marker == marker),
            detail: () =>
                $"HandlerReturnsFailure marker={marker}; queue={queue}; "
                + $"collectorCount={TestCollector.Count<AlwaysFailsHandler>()}; "
                + $"markers=[{string.Join(",", TestCollector
                    .Captured<AlwaysFailsHandler, IntegrationAuditEvent>()
                    .Select(m => m.Marker ?? "<null>"))}]");

        var dlqName = DlqNaming.DlqFor(queue);
        await WaitForQueueCount(dlqName, expected: 1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DlqMessage_CarriesFailureReasonHeader_WithCauseAndErrorCode()
    {
        // DLX republish verification: the nacked message is republished to
        // the DLX with an x-d2-failure-reason header attached. Without this
        // behavior the DLQ message would arrive header-less (BasicNack-no-requeue
        // -> broker x-dead-letter-exchange routes a copy without our diagnostic header).
        TestCollector.Reset<AlwaysThrowsHandler>();
        var queue = "dlq.hdr." + Guid.NewGuid().ToString("N")[..8];
        var marker = "header-test-" + Guid.NewGuid().ToString("N")[..8];

        await using var host = await StartHostAsync(services =>
        {
            services.AddTransient<AlwaysThrowsHandler>();
            services.AddD2Subscriber<AlwaysThrowsHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 5));
        });

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            var publish = await bus.PublishAsync(
                new IntegrationAuditEvent { Marker = marker });
            publish.Failed.Should().BeFalse(
                "publish must succeed before waiting for handler/DLQ delivery");
        }

        await WaitFor(
            () => TestCollector
                .Captured<AlwaysThrowsHandler, IntegrationAuditEvent>()
                .Any(m => m.Marker == marker),
            detail: () =>
                $"DlqHeader marker={marker}; queue={queue}; "
                + $"collectorCount={TestCollector.Count<AlwaysThrowsHandler>()}");

        var dlqName = DlqNaming.DlqFor(queue);
        await WaitForQueueCount(dlqName, expected: 1);

        // Pull the DLQ message and inspect its x-d2-failure-reason header.
        var factory = new ConnectionFactory { Uri = new Uri(r_fixture.ConnectionString) };
        await using var conn = await factory.CreateConnectionAsync();
        await using var channel = await conn.CreateChannelAsync();
        var get = await channel.BasicGetAsync(dlqName, autoAck: true);
        get.Should().NotBeNull("DLQ should contain the failed message");

        var headers = get.BasicProperties.Headers;
        headers.Should().NotBeNull();
        headers.Should().ContainKey(AmqpHeaders.FAILURE_REASON);

        var failureBytes = (byte[])headers[AmqpHeaders.FAILURE_REASON]!;
        var failureJson = Encoding.UTF8.GetString(failureBytes);
        var doc = JsonDocument.Parse(failureJson);

        // BaseHandler's universal try/catch converts the handler's
        // InvalidOperationException into a D2Result.UnhandledException, so
        // the consumer sees a result-failure path (not the bare exception
        // path). Cause/errorCode reflect that conversion.
        doc.RootElement.GetProperty("cause").GetString()
            .Should().Be("HANDLER_RESULT_FAILURE");
        doc.RootElement.GetProperty("errorCode").GetString()
            .Should().Be("GEN_UNHANDLED_EXCEPTION");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TopologyDeclaration_IsIdempotent()
    {
        var queue = "dlq.idem." + Guid.NewGuid().ToString("N")[..8];

        // First host declares the topology, then goes away leaving it behind.
        // deleteQueuesOnDispose is false ON PURPOSE; the assertion below depends on it: teardown
        // normally deletes the queues a host declared, and if it deleted this one the
        // second host below would declare a FRESH queue rather than re-declare an
        // existing one. That also succeeds, so the case would still pass while no
        // longer testing idempotency at all. Do not remove it as redundant.
        await using (var host1 = await StartHostAsync(
            services =>
            {
                services.AddTransient<AuditCapturingHandler>();
                services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                    IntegrationSubscriptionFactory.ForAuditEvent(queue));
            },
            deleteQueuesOnDispose: false))
        {
            // Nothing to do in the block - the declaration happened during host start,
            // and the host coming up is what proves it. The block exists so the host is
            // disposed here, before the second one starts.
            _ = host1;
        }

        // The precondition this case rests on, asserted rather than assumed: the queue
        // must still EXIST between the publisher host and the consumer host.
        // QueueDeclarePassiveAsync throws if it
        // does not. Without this check, anything that deleted the queue at host1 teardown
        // would leave host2 declaring a fresh entity - which also succeeds - and the case
        // would keep passing while testing nothing.
        await AssertQueueExistsAsync(queue);

        // Second host re-declares against the same broker - must NOT throw
        // (RabbitMQ's *DeclareAsync calls are no-ops on identical pre-existing
        // entities). If we incorrectly tried to declare with different
        // arguments, the broker would reject with PRECONDITION_FAILED.
        await using var host2 = await StartHostAsync(services =>
        {
            services.AddTransient<AuditCapturingHandler>();
            services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(queue));
        });

        // Reaching here without exception is the assertion.
    }

    // Attempt-budgeted stuck-guard - see _POLL_ATTEMPT_BUDGET; no wall-clock deadline.
    private static async Task WaitFor(
        Func<bool> predicate,
        TimeSpan? pollInterval = null,
        Func<string>? detail = null)
    {
        pollInterval ??= TimeSpan.FromMilliseconds(50);
        for (var attempt = 0; attempt < _POLL_ATTEMPT_BUDGET; attempt++)
        {
            if (predicate()) return;
            await Task.Delay(pollInterval.Value);
        }

        var suffix = detail is null ? string.Empty : " " + detail();
        throw new TimeoutException(
            $"Predicate did not become true within {_POLL_ATTEMPT_BUDGET} poll attempts."
            + suffix);
    }

    /// <summary>Fails unless the queue already exists on the broker.</summary>
    /// <param name="queueName">The queue that must pre-exist.</param>
    private async Task AssertQueueExistsAsync(string queueName)
    {
        var factory = new ConnectionFactory { Uri = new Uri(r_fixture.ConnectionString) };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var declareOk = await channel.QueueDeclarePassiveAsync(queueName);
        declareOk.QueueName.Should().Be(
            queueName,
            "the second host must re-declare an EXISTING queue for this case to be "
            + "testing idempotency at all");
    }

    private async Task<MessagingTestHost> StartHostAsync(
        Action<IServiceCollection> configure, bool deleteQueuesOnDispose = true)
        => await MessagingHostBuilder.BuildAndStartAsync(
            r_fixture, configure, deleteQueuesOnDispose: deleteQueuesOnDispose);

    private async Task WaitForQueueCount(string queueName, int expected)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(r_fixture.ConnectionString),
        };

        await using var conn = await factory.CreateConnectionAsync();
        await using var channel = await conn.CreateChannelAsync();

        // Attempt-budgeted stuck-guard (see _POLL_ATTEMPT_BUDGET) - dead-lettering
        // is async broker-side, so poll a bounded number of times rather than
        // against a wall-clock deadline a loaded broker can outrun.
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
