// -----------------------------------------------------------------------
// <copyright file="SubscriberChannelBehaviorIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Subscribing;
using DcsvIo.D2.Messaging.RabbitMq.Topology;
using DcsvIo.D2.Tests.Integration.Messaging.Fixtures;
using DcsvIo.D2.Tests.Unit.Messaging.Subscribing.Fixtures;
using global::RabbitMQ.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Integration coverage for <see cref="SubscriberChannel"/> runtime behavior
/// against a real broker - republish-channel races, ack-failure boundary,
/// retries-exhausted DLQ routing, and disposal drain. Each test isolates
/// its own queue (GUID-suffixed) so they're safe to run in parallel.
/// </summary>
[Collection("RabbitMq")]
public sealed class SubscriberChannelBehaviorIntegrationTests
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
    public SubscriberChannelBehaviorIntegrationTests(RabbitMqFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Republish_SharedDeliveryFixture_PreservesOriginalBytesAndProperties()
    {
        using var fixture = DlqLifetimeContractFixture.Load();
        foreach (var row in fixture.RootElement.GetProperty("deliveries").EnumerateArray())
        {
            var queue = $"wire-parity-{Guid.NewGuid():N}";
            await using var host = await StartHostAsync(services =>
            {
                services.AddTransient<AuditCapturingHandler>();
                services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                    BuildAuditDescriptorWithTieredRetry(queue, maxAttempts: 3));
            });
            var factory = new ConnectionFactory { Uri = new Uri(r_fixture.ConnectionString) };
            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();
            var body = Convert.FromHexString(row.GetProperty("bodyHex").GetString()!);
            var properties = new BasicProperties
            {
                MessageId = row.GetProperty("messageId").GetString(),
                ContentType = row.GetProperty("contentType").GetString(),
                ContentEncoding = row.GetProperty("contentEncoding").GetString(),
                CorrelationId = row.GetProperty("correlationId").GetString(),
                Headers = new Dictionary<string, object?>
                {
                    ["fixture"] = row.GetProperty("header").GetString(),
                    ["x-death"] = new List<object?>
                    {
                        new Dictionary<string, object?> { ["reason"] = "expired", ["count"] = 5L },
                    },
                },
            };
            await channel.BasicPublishAsync(
                "d2.test.integration-audit", string.Empty, false, properties, body);
            var dlq = DlqNaming.DlqFor(queue);
            await WaitForQueueCount(dlq, 1);
            var received = await channel.BasicGetAsync(dlq, true);
            received.Should().NotBeNull();
            received.Body.ToArray().Should().Equal(body);
            received.BasicProperties.MessageId.Should().Be(properties.MessageId);
            received.BasicProperties.ContentType.Should().Be(properties.ContentType);
            received.BasicProperties.ContentEncoding.Should().Be(properties.ContentEncoding);
            received.BasicProperties.CorrelationId.Should().Be(properties.CorrelationId);
            var header = received.BasicProperties.Headers!["fixture"].Should().BeOfType<byte[]>().Subject;
            System.Text.Encoding.UTF8.GetString(header).Should().Be(row.GetProperty("header").GetString());
            await host.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConsumerStartup_ShutdownBeforeScheduling_CompletesReadiness()
    {
        await using var connection = await BuildRealConnectionAsync();
        var registry = new SubscriberRegistry([BuildSubscription($"startup-stop-{Guid.NewGuid():N}")]);
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var topology = new DefaultTopologyDeclarer(
            connection,
            registry,
            IntegrationMessageFixtures.Catalog,
            NullLogger<DefaultTopologyDeclarer>.Instance);
        await topology.DeclareAsync(CancellationToken.None);
        await using var consumer = new ConsumerHostedService(
            connection,
            registry,
            provider.GetRequiredService<IServiceScopeFactory>(),
            new HandlerDispatcherFactory(registry, IntegrationMessageFixtures.Catalog),
            IntegrationMessageFixtures.Catalog,
            topology,
            NullLoggerFactory.Instance,
            NullLogger<ConsumerHostedService>.Instance,
            Options.Create(new RabbitMqConnectionOptions
            {
                ConnectionUri = r_fixture.ConnectionString,
            }),
            Options.Create(new RabbitMqSubscriberOptions()));
        Task? stop = null;
        var stopConsumer = consumer.StopAsync;
        var disposeConsumer = consumer.DisposeAsync;
        consumer.StartupPublishedForTesting = () => stop = stopConsumer(CancellationToken.None);

        Exception? startFailure = null;
        try
        {
            await consumer.StartAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            startFailure = ex;
        }

        await disposeConsumer();
        stop.Should().NotBeNull();
        await stop;
        startFailure.Should().BeNull();
        var readiness = consumer.ReadyTask;
        readiness.IsCompleted.Should().BeTrue();
        var ready = async () => await readiness;
        await ready.Should().ThrowAsync<OperationCanceledException>();
        connection.IsOpen.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Integration")]
    public async Task DlqRepublish_ServerWithholdsReply_RecoversWithinDeadline(bool acquisition)
    {
        await using var filter = new AmqpReplyFilterFixture(r_fixture.ConnectionString);
        await filter.InitializeAsync();
        var filteredConnectionString = filter.ConnectionString;
        var queue = $"reply-timeout-{Guid.NewGuid():N}";
        await using var host = await StartHostAsync(services =>
        {
            services.Configure<RabbitMqConnectionOptions>(o =>
                o.ConnectionUri = filteredConnectionString);
            services.Configure<RabbitMqSubscriberOptions>(o =>
                o.DlqRepublishTimeout = NodaTime.Duration.FromMilliseconds(100));
            services.AddTransient<AuditCapturingHandler>();
            services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                BuildAuditDescriptorWithTieredRetry(queue, maxAttempts: 3));
        });
        var consumer = host.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<ConsumerHostedService>().Single();
        var channelsField = typeof(ConsumerHostedService).GetField(
            "r_channels", BindingFlags.Instance | BindingFlags.NonPublic);
        channelsField.Should().NotBeNull();
        var channels = (List<SubscriberChannel>)channelsField.GetValue(consumer)!;
        var subscriber = channels.Single();

        try
        {
            if (acquisition)
                filter.SuppressChannelOpen();
            else
                filter.SuppressConfirm();

            await PublishWithSyntheticXDeathAsync(
                "d2.test.integration-audit", string.Empty, 5);
            var suppressed = filter.Suppressed;
            await WaitFor(() => suppressed.IsCompleted);

            for (var attempt = 0; attempt < 500 && subscriber.InflightCallbackCountForTesting > 0;
                attempt++)
            {
                await Task.Delay(10);
            }

            subscriber.InflightCallbackCountForTesting.Should().Be(0);
            await WaitForQueueCount(DlqNaming.DlqFor(queue), acquisition ? 1 : 2);
        }
        finally
        {
            await filter.ReleaseAsync();
            await WaitFor(() => subscriber.InflightCallbackCountForTesting == 0);
            await host.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RepublishChannel_ConcurrentPublications_ReuseOneGeneration()
    {
        await using var realConn = await BuildRealConnectionAsync();
        var queue = $"republish-concurrent-{Guid.NewGuid():N}";
        var registration = BuildSubscription(queue);
        var registry = new SubscriberRegistry([registration]);
        var topology = new DefaultTopologyDeclarer(
            realConn,
            registry,
            IntegrationMessageFixtures.Catalog,
            NullLogger<DefaultTopologyDeclarer>.Instance);
        await topology.DeclareAsync(CancellationToken.None);
        await using var republisher = new DlqRepublisher(
            new RabbitMqConnectionOptions { ConnectionUri = r_fixture.ConnectionString },
            new RabbitMqSubscriberOptions(),
            16,
            NullLogger.Instance);
        var request = new DlqRepublishRequest(
            DlqNaming.DlxFor(queue),
            new byte[] { 1 },
            new Dictionary<string, object?>(),
            null,
            "application/octet-stream",
            null,
            null);
        var tasks = Enumerable.Range(0, 16)
            .Select(_ => republisher.RepublishAsync(request, CancellationToken.None).AsTask())
            .ToArray();
        var outcomes = await Task.WhenAll(tasks);
        outcomes.Should().OnlyContain(outcome => outcome == DlqRepublishOutcome.Confirmed);
        var ready = republisher.State.Should().BeOfType<DlqRepublisherState.Ready>().Subject;
        ready.Generation.Id.Should().Be(1);
        ready.Generation.ChannelAcquisition.Should().NotBeNull();
        (await ready.Generation.ChannelAcquisition).IsOpen.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RepublishChannel_ConfirmedPublication_ReleasesCompletedSend()
    {
        await using var realConn = await BuildRealConnectionAsync();
        var queue = $"republish-release-{Guid.NewGuid():N}";
        var topology = new DefaultTopologyDeclarer(
            realConn,
            new SubscriberRegistry([BuildSubscription(queue)]),
            IntegrationMessageFixtures.Catalog,
            NullLogger<DefaultTopologyDeclarer>.Instance);
        await topology.DeclareAsync(CancellationToken.None);
        await using var republisher = new DlqRepublisher(
            new RabbitMqConnectionOptions { ConnectionUri = r_fixture.ConnectionString },
            new RabbitMqSubscriberOptions(),
            1,
            NullLogger.Instance);
        var request = new DlqRepublishRequest(
            DlqNaming.DlxFor(queue),
            new byte[] { 1 },
            new Dictionary<string, object?>(),
            null,
            "application/octet-stream",
            null,
            null);
        (await republisher.RepublishAsync(request, CancellationToken.None))
            .Should().Be(DlqRepublishOutcome.Confirmed);
        var ready = republisher.State.Should().BeOfType<DlqRepublisherState.Ready>().Subject;
        ready.Generation.Send.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RepublishChannel_RepeatedSequentialCalls_ReuseSameChannel()
    {
        await using var realConn = await BuildRealConnectionAsync();
        var queue = $"republish-sequential-{Guid.NewGuid():N}";
        var registration = BuildSubscription(queue);
        var registry = new SubscriberRegistry([registration]);
        var topology = new DefaultTopologyDeclarer(
            realConn,
            registry,
            IntegrationMessageFixtures.Catalog,
            NullLogger<DefaultTopologyDeclarer>.Instance);
        await topology.DeclareAsync(CancellationToken.None);
        await using var republisher = new DlqRepublisher(
            new RabbitMqConnectionOptions { ConnectionUri = r_fixture.ConnectionString },
            new RabbitMqSubscriberOptions(),
            1,
            NullLogger.Instance);
        var request = new DlqRepublishRequest(
            DlqNaming.DlxFor(queue),
            new byte[] { 1 },
            new Dictionary<string, object?>(),
            null,
            "application/octet-stream",
            null,
            null);
        (await republisher.RepublishAsync(request, CancellationToken.None))
            .Should().Be(DlqRepublishOutcome.Confirmed);
        var first = republisher.State.Should().BeOfType<DlqRepublisherState.Ready>()
            .Subject.Generation.ChannelAcquisition;
        (await republisher.RepublishAsync(request, CancellationToken.None))
            .Should().Be(DlqRepublishOutcome.Confirmed);
        var second = republisher.State.Should().BeOfType<DlqRepublisherState.Ready>()
            .Subject.Generation.ChannelAcquisition;
        (await republisher.RepublishAsync(request, CancellationToken.None))
            .Should().Be(DlqRepublishOutcome.Confirmed);
        var third = republisher.State.Should().BeOfType<DlqRepublisherState.Ready>()
            .Subject.Generation.ChannelAcquisition;
        ReferenceEquals(first, second).Should().BeTrue();
        ReferenceEquals(second, third).Should().BeTrue();
        first.Should().NotBeNull();
        (await first).IsOpen.Should().BeTrue();
    }

    // ---------------------------------------------------------------------
    // Ack failure - narrow-catch around BasicAck. An ack throw on a
    // successful handler must NOT republish to DLX (ack failure is not a
    // handler failure).
    // ---------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AckFailureOnHandlerSuccess_LogsAckFailed_DoesNotRouteToDlq()
    {
        TestCollector.Reset<AuditCapturingHandler>();
        var queue = "ack-fail." + Guid.NewGuid().ToString("N")[..8];

        // Wire the hook to throw on every ack call. The handler runs OK,
        // returns Ok, then the ack throws - which the narrow catch
        // converts to AckFailed log + counter (NOT republish-to-DLX).
        SubscriberChannel.AckHookForTesting =
            (_, _) => throw new InvalidOperationException("ack-blew-up");

        try
        {
            await using var host = await StartHostAsync(services =>
            {
                services.AddTransient<AuditCapturingHandler>();
                services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                    IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 1));
            });

            await using (var scope = host.Services.CreateAsyncScope())
            {
                var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
                await bus.PublishAsync(new IntegrationAuditEvent { Marker = "ack-fail" });
            }

            // Handler runs once.
            await WaitFor(
                () => TestCollector.Count<AuditCapturingHandler>() > 0);
            TestCollector.Count<AuditCapturingHandler>().Should().Be(1);

            // The DLQ MUST be empty - narrow catch around BasicAck means
            // the ack failure does NOT reach the outer catch's republish-
            // to-DLX path. An ack throw must not reach the broad
            // handler-exception catch, which would falsely DLQ the
            // already-processed message.
            //
            // The handler is already confirmed complete (WaitFor above). If the
            // buggy republish-to-DLX path fires, it does so in the same async
            // continuation - any DLQ message appears within milliseconds.
            // 500 ms is generous but bounded; no fixed 2 s sleep required.
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            var dlqName = DlqNaming.DlqFor(queue);
            var dlqCount = await GetQueueCountAsync(dlqName);
            dlqCount.Should().Be(
                0, "ack failure on success path is NOT a handler failure "
                + "and must not republish to DLX");
        }
        finally
        {
            SubscriberChannel.AckHookForTesting = null;
        }
    }

    // ---------------------------------------------------------------------
    // RETRIES_EXHAUSTED enforcement - synthetic x-death header that exceeds
    // MaxAttempts routes the message direct to DLQ without invoking the
    // handler.
    // ---------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RetriesExhausted_RoutesToDlqWithoutInvokingHandler()
    {
        TestCollector.Reset<AuditCapturingHandler>();
        var queue = "retries-exhausted." + Guid.NewGuid().ToString("N")[..8];

        await using var host = await StartHostAsync(services =>
        {
            services.AddTransient<AuditCapturingHandler>();
            services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                BuildAuditDescriptorWithTieredRetry(queue, maxAttempts: 3));
        });

        // Publish RAW with x-death header reporting 5 expired-cycles -
        // exceeds MaxAttempts=3. The consumer reads x-death, ReadAttemptCount
        // returns 5, and the message routes direct to DLQ with cause
        // RETRIES_EXHAUSTED - the handler is NOT invoked.
        await PublishWithSyntheticXDeathAsync(
            exchange: "d2.test.integration-audit",
            routingKey: string.Empty,
            count: 5);

        var dlqName = DlqNaming.DlqFor(queue);
        await WaitForQueueCount(dlqName, expected: 1);

        TestCollector.Count<AuditCapturingHandler>().Should().Be(
            0, "RETRIES_EXHAUSTED routes direct to DLQ without invoking "
            + "the handler - the dispatch step is skipped entirely");
    }

    // ---------------------------------------------------------------------
    // Disposal drain - slow handler + fast dispose; the drain spin-wait
    // must let the handler complete its ack cleanly.
    // ---------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DisposeMidHandler_DrainsInflightCallbacksBeforeClose()
    {
        TestCollector.Reset<SlowHandler>();
        var queue = "dispose-drain." + Guid.NewGuid().ToString("N")[..8];
        var handlerStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        SlowHandler.HandlerStartedSignal = handlerStarted;
        SlowHandler.HandlerHoldDuration = TimeSpan.FromSeconds(2);

        var host = await StartHostAsync(services =>
        {
            services.AddTransient<SlowHandler>();
            services.AddD2Subscriber<SlowHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 1));
        });

        try
        {
            await using (var scope = host.Services.CreateAsyncScope())
            {
                var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
                await bus.PublishAsync(new IntegrationAuditEvent { Marker = "drain" });
            }

            // Wait for the handler to start (signals it's mid-flight).
            await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));

            // Now stop the host while the handler is still sleeping. The
            // drain spin-wait must let the handler complete + ack, so when
            // StopAsync returns, no message remains pending.
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await host.StopAsync(CancellationToken.None);
            stopwatch.Stop();

            // The drain must wait for the SlowHandler (~2 s). The
            // handlerStarted.Task gate fires once the handler is already
            // executing its 2 s sleep, so StopAsync must wait out the
            // remaining sleep. A lower bound of 1500 ms proves the drain
            // actually waited; 500 ms would allow a handler that returned
            // early (i.e. did NOT drain) to pass silently.
            stopwatch.Elapsed.Should().BeGreaterThan(
                TimeSpan.FromMilliseconds(1500),
                "drain should have waited for the slow handler to complete");
            stopwatch.Elapsed.Should().BeLessThan(
                TimeSpan.FromSeconds(15),
                "drain timeout (30s) should NOT have fired for a 2s handler");

            TestCollector.Count<SlowHandler>().Should().Be(
                1, "the slow handler must have been allowed to complete "
                + "before the channel closed");
        }
        finally
        {
            await host.DisposeAsync();
            SlowHandler.HandlerStartedSignal = null;
            SlowHandler.HandlerHoldDuration = TimeSpan.Zero;
        }
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static MqSubscriptionDescriptor BuildAuditDescriptorWithTieredRetry(
        string queueName, int maxAttempts)
    {
        return new MqSubscriptionDescriptor(
            Constant: "TestSub",
            MessageTypeName: typeof(IntegrationAuditEvent).FullName!,
            QueueName: queueName,
            Pattern: QueuePattern.CompetingConsumer,
            RoutingKeyBinding: "#",
            Prefetch: 1,
            Idempotency: false,
            TieredRetry: new TieredRetryDescriptor(
                Tiers: [TimeSpan.FromSeconds(5)],
                MaxAttempts: maxAttempts));
    }

    private static SubscriberRegistration BuildSubscription(string queueName)
    {
        var descriptor = new MqSubscriptionDescriptor(
            Constant: "TestSub",
            MessageTypeName: typeof(IntegrationAuditEvent).FullName!,
            QueueName: queueName,
            Pattern: QueuePattern.CompetingConsumer,
            RoutingKeyBinding: string.Empty,
            Prefetch: 1,
            Idempotency: false,
            TieredRetry: null);

        return new SubscriberRegistration(
            HandlerType: typeof(AuditCapturingHandler),
            MessageType: typeof(IntegrationAuditEvent),
            Descriptor: descriptor,
            ResolvedQueueName: queueName);
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

    private async Task<MessagingTestHost> StartHostAsync(Action<IServiceCollection> configure)
        => await MessagingHostBuilder.BuildAndStartAsync(r_fixture, configure);

    private async Task<ID2Connection> BuildRealConnectionAsync()
    {
        var optsBuilder = new ServiceCollection()
            .AddOptions<RabbitMqConnectionOptions>()
            .Configure(o =>
            {
                o.ConnectionUri = r_fixture.ConnectionString;
                o.ClientProvidedName = "subscriber-channel-tests";
            });
        var sp = optsBuilder.Services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<RabbitMqConnectionOptions>>();
        var conn = new RabbitMqConnection(opts, NullLogger<RabbitMqConnection>.Instance);
        conn.StartReconnectLoop();
        await conn.ReadyTask.WaitAsync(TestContext.Current.CancellationToken);
        return conn;
    }

    private async Task<int> GetQueueCountAsync(string queueName)
    {
        var factory = new ConnectionFactory { Uri = new Uri(r_fixture.ConnectionString) };
        await using var conn = await factory.CreateConnectionAsync();
        await using var channel = await conn.CreateChannelAsync();
        var ok = await channel.QueueDeclarePassiveAsync(queueName);
        return (int)ok.MessageCount;
    }

    private async Task WaitForQueueCount(string queueName, int expected)
    {
        // Attempt-budgeted stuck-guard (see _POLL_ATTEMPT_BUDGET) - dead-lettering
        // is async broker-side, so poll a bounded number of times rather than
        // against a wall-clock deadline a loaded broker can outrun.
        for (var attempt = 0; attempt < _POLL_ATTEMPT_BUDGET; attempt++)
        {
            if (await GetQueueCountAsync(queueName) >= expected) return;
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        var final = await GetQueueCountAsync(queueName);
        throw new TimeoutException(
            $"Queue '{queueName}' had {final} messages after "
            + $"{_POLL_ATTEMPT_BUDGET} poll attempts, expected >= {expected}.");
    }

    private async Task PublishWithSyntheticXDeathAsync(
        string exchange, string routingKey, long count)
    {
        var factory = new ConnectionFactory { Uri = new Uri(r_fixture.ConnectionString) };
        await using var conn = await factory.CreateConnectionAsync();
        await using var channel = await conn.CreateChannelAsync();

        var deathEntries = new List<object?>
        {
            new Dictionary<string, object?>
            {
                ["reason"] = "expired",
                ["count"] = count,
            },
        };

        var props = new BasicProperties
        {
            ContentType = "application/octet-stream",
            MessageId = Guid.CreateVersion7().ToString(),
            DeliveryMode = DeliveryModes.Persistent,
            Headers = new Dictionary<string, object?>
            {
                ["x-death"] = deathEntries,
            },
        };

        // The body is a JSON-serialized IntegrationAuditEvent - but the
        // consumer routes to DLQ before any deserialization, so empty
        // bytes work too.
        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: props,
            body: ReadOnlyMemory<byte>.Empty);
    }
}
