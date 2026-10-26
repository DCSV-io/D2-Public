// -----------------------------------------------------------------------
// <copyright file="ConsumerHostedServiceDisposalTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Subscribing;

using System;
using System.Collections.Generic;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Subscribing;
using DcsvIo.D2.Messaging.RabbitMq.Topology;
using DcsvIo.D2.Tests.Unit.Messaging.Subscribing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Pins what <c>ConsumerHostedService.DisposeAsync</c> does when the host is
/// disposed without being stopped: it disposes every subscriber channel the
/// host opened, waits for the background start the host launched, and
/// releases the host's cancellation source.
/// </summary>
/// <remarks>
/// Disposal disposes the channels and, after a stop, the cancellation source. A
/// disposal that returns early on <c>r_cts.IsCancellationRequested</c> and
/// cancels and disposes the source alone fails the channel-disposal, start-wait
/// and cancellation-source tests.
/// </remarks>
public sealed class ConsumerHostedServiceDisposalTests : IDisposable
{
    /// <summary>
    /// Every provider a host was built from. The class owns them, so they are
    /// disposed with it rather than left to the finalizer.
    /// </summary>
    private readonly List<ServiceProvider> r_providers = [];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartAsync_ShutdownBeforeScheduling_CompletesReadiness(bool cancelStop)
    {
        var connection = new ReadyConnectionStub();
        var host = BuildHost(connection, new NoOpTopologyDeclarerStub(), "schedule-stop-q");
        using var stopCts = new CancellationTokenSource();
        Action cancel = stopCts.Cancel;
        var stopToken = stopCts.Token;
        Task? stop = null;
        host.StartupPublishedForTesting = () =>
        {
            if (cancelStop)
                cancel();

            stop = host.StopAsync(stopToken);
        };

        Exception? startFailure = null;

        try
        {
            await host.StartAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            startFailure = ex;
        }

        await host.DisposeAsync();
        stop.Should().NotBeNull();
        await stop;
        startFailure.Should().BeNull();

        var startup = async () => await host.StartTaskForTesting!;
        await startup.Should().ThrowAsync<OperationCanceledException>();
        host.ReadyTask.IsCompleted.Should().BeTrue();
        var ready = async () => await host.ReadyTask;
        await ready.Should().ThrowAsync<OperationCanceledException>();
        connection.Created.Should().BeEmpty();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var provider in r_providers)
            provider.Dispose();

        r_providers.Clear();
    }

    [Fact]
    public async Task DisposeAsync_WithoutStopAsync_DisposesEveryChannelItOpened()
    {
        var connection = new ReadyConnectionStub();
        var host = BuildHost(connection, new NoOpTopologyDeclarerStub(), "dispose-leak-q");

        await host.StartAsync(CancellationToken.None);
        await host.ReadyTask;

        await host.DisposeAsync();

        connection.Created.Should().ContainSingle(
            "the fixture registers one subscriber, so exactly one channel is opened");

        connection.Created[0].DisposeAsyncCallCount.Should().Be(
            1,
            "a host disposed without being stopped still owns the AMQP channel and "
            + "the semaphore behind every SubscriberChannel it opened");
    }

    [Fact]
    public async Task DisposeAsync_WhileTheStartIsStillOpeningChannels_DisposesTheLateOne()
    {
        // Park the second CreateChannelAsync so the background start is midway
        // through opening its channels when disposal arrives. A disposal that
        // returns without waiting leaves the late channel undisposed, with no
        // reference to it.
        var connection = new ReadyConnectionStub { ParkAtCall = 2 };
        var host = BuildHost(
            connection,
            new NoOpTopologyDeclarerStub(),
            "dispose-late-a-q",
            "dispose-late-b-q");

        await host.StartAsync(CancellationToken.None);
        await connection.Parked;

        var dispose = host.DisposeAsync().AsTask();
        connection.Release();
        await dispose;

        connection.Created.Should().HaveCount(
            2, "both subscribers opened a channel before the start finished");

        connection.Created.Should().OnlyContain(
            ch => ch.DisposeAsyncCallCount == 1,
            "disposal waits for the background start, so the channel opened after "
            + "disposal began is still owned by the host and gets disposed");
    }

    [Fact]
    public async Task DisposeAsync_AfterStopAsync_DisposesTheCancellationSource()
    {
        var connection = new ReadyConnectionStub();
        var host = BuildHost(connection, new NoOpTopologyDeclarerStub(), "dispose-cts-q");

        await host.StartAsync(CancellationToken.None);
        await host.ReadyTask;
        await host.StopAsync(CancellationToken.None);

        await host.DisposeAsync();

        CancellationSourceIsDisposed(host).Should().BeTrue(
            "a stop that already canceled the source must not stop disposal from "
            + "releasing it - the source is the host's to dispose on every path");
    }

    [Fact]
    public async Task DisposeAsync_AfterStopAsync_DisposesEachChannelOnce()
    {
        var connection = new ReadyConnectionStub();
        var host = BuildHost(connection, new NoOpTopologyDeclarerStub(), "dispose-once-q");

        await host.StartAsync(CancellationToken.None);
        await host.ReadyTask;
        await host.StopAsync(CancellationToken.None);

        await host.DisposeAsync();

        connection.Created[0].DisposeAsyncCallCount.Should().Be(
            1, "the shutdown sequence runs once however many callers ask for it");
    }

    [Fact]
    public async Task DisposeAsync_RacingStopAsync_DisposesEachChannelOnce()
    {
        var connection = new ReadyConnectionStub();
        var host = BuildHost(connection, new NoOpTopologyDeclarerStub(), "dispose-race-q");

        await host.StartAsync(CancellationToken.None);
        await host.ReadyTask;

        var stop = Task.Run(() => host.StopAsync(CancellationToken.None));
        var dispose = Task.Run(async () => await host.DisposeAsync());
        var both = async () => await Task.WhenAll(stop, dispose);

        await both.Should().NotThrowAsync();

        connection.Created[0].DisposeAsyncCallCount.Should().Be(
            1,
            "the shutdown gate hands the loser the winner's task rather than a "
            + "second pass over the same channel list");
    }

    [Fact]
    public async Task StopAsync_AfterACompletedStart_ReachesTheBrokerWithoutHoldingTheShutdownGate()
    {
        // The start has finished, so the shutdown sequence has nothing to wait
        // for and runs synchronously up to the channel's broker calls. A
        // sequence started inside the gate makes those calls with it held, and
        // every channel-tracking read blocks behind broker I/O.
        var connection = new ReadyConnectionStub();
        var host = BuildHost(connection, new NoOpTopologyDeclarerStub(), "stop-gate-q");

        await host.StartAsync(CancellationToken.None);
        await host.ReadyTask;
        await host.StartTaskForTesting!;

        var gate = ShutdownGateOf(host);
        var observed = 0;
        var heldDuringBrokerCall = false;
        connection.Created.Should().ContainSingle();
        connection.Created[0].BrokerCallObserver = () =>
        {
            Interlocked.Increment(ref observed);

            if (gate.IsHeldByCurrentThread)
                heldDuringBrokerCall = true;
        };

        await host.StopAsync(CancellationToken.None);
        await host.DisposeAsync();

        observed.Should().BePositive("the shutdown reached the channel's broker calls");
        heldDuringBrokerCall.Should().BeFalse(
            "the shutdown task is published under the gate and run after its release");
    }

    [Fact]
    public async Task DisposeAsync_WhenTheBackgroundStartFaulted_DoesNotThrow()
    {
        var connection = new ReadyConnectionStub();
        var host = BuildHost(
            connection, new ThrowingTopologyDeclarerStub(), "dispose-fault-q");

        await host.StartAsync(CancellationToken.None);

        var dispose = async () => await host.DisposeAsync();

        await dispose.Should().NotThrowAsync(
            "disposal observes the faulted start so it can dispose the channels "
            + "that start opened, and a fault already logged must not escape it");
    }

    [Fact]
    public async Task StartAsync_PublishesTheBackgroundStart_BeforeItObservesTheConnection()
    {
        // The background start reads the connection first, so its getter runs at
        // the earliest instant the body can observe anything. A shutdown running
        // concurrently reads a handle published after that instant as null,
        // waits for nothing, and releases the cancellation source the body is
        // about to read.
        //
        // The start task is published before the body launches. Publishing it
        // after the launch, with a delay, makes the probe find a null handle.
        var connection = new PublicationProbeConnectionStub();
        var host = BuildHost(connection, new NoOpTopologyDeclarerStub(), "publish-order-q");
        connection.Probe = () => host.StartTaskForTesting is not null;

        await host.StartAsync(CancellationToken.None);
        await connection.Observed;
        await host.DisposeAsync();

        connection.PublishedWhenObserved.Should().BeTrue(
            "the shutdown gate can only wait for a start it can see");
    }

    [Fact]
    public async Task StartAsync_AfterTheShutdownDrained_StartsNothing()
    {
        // Disposal releases the cancellation source, so a start arriving after
        // it must not reach the connection and must not read that source. The
        // drained flag and the token are read under one gate for that reason.
        var connection = new PublicationProbeConnectionStub();
        var host = BuildHost(connection, new NoOpTopologyDeclarerStub(), "post-shutdown-q");
        await host.DisposeAsync();

        var start = async () => await host.StartAsync(CancellationToken.None);

        await start.Should().NotThrowAsync();
        host.StartTaskForTesting.Should().BeNull("a drained shutdown owns nothing more to start");
        connection.PublishedWhenObserved.Should().BeNull("the background start never ran");
    }

    /// <summary>Reads the host's private cancellation source and checks
    /// whether it has been disposed. The source is deliberately not exposed:
    /// its disposal has no other observable, and a test seam for it would be
    /// production surface that exists for one assertion.</summary>
    /// <param name="host">The host under test.</param>
    /// <returns><see langword="true"/> when the source has been disposed.</returns>
    private static bool CancellationSourceIsDisposed(ConsumerHostedService host)
    {
        var field = typeof(ConsumerHostedService).GetField(
            "r_cts", BindingFlags.Instance | BindingFlags.NonPublic);

        field.Should().NotBeNull("the host holds its cancellation source in r_cts");

        var source = (CancellationTokenSource)field.GetValue(host)!;

        try
        {
            _ = source.Token;
            return false;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }

    /// <summary>Reads the host's private shutdown gate, so that a test can
    /// check whether the calling thread holds it.</summary>
    /// <param name="host">The host under test.</param>
    /// <returns>The gate.</returns>
    private static Lock ShutdownGateOf(ConsumerHostedService host)
    {
        var field = typeof(ConsumerHostedService).GetField(
            "r_shutdownGate", BindingFlags.Instance | BindingFlags.NonPublic);

        field.Should().NotBeNull("the host holds its shutdown gate in r_shutdownGate");

        return (Lock)field.GetValue(host)!;
    }

    private static SubscriberRegistration BuildRegistration(string queueName)
    {
        var descriptor = new MqSubscriptionDescriptor(
            Constant: "DisposalSub",
            MessageTypeName: typeof(DisposalFixtureMessage).FullName!,
            QueueName: queueName,
            Pattern: QueuePattern.CompetingConsumer,
            RoutingKeyBinding: "#",
            Prefetch: 1,
            Idempotency: false,
            TieredRetry: null);

        return new SubscriberRegistration(
            HandlerType: typeof(DisposalFixtureHandler),
            MessageType: typeof(DisposalFixtureMessage),
            Descriptor: descriptor,
            ResolvedQueueName: queueName);
    }

    private ConsumerHostedService BuildHost(
        ID2Connection connection,
        ITopologyDeclarer topology,
        params string[] queueNames)
    {
        var registry = new SubscriberRegistry(
            [.. queueNames.Select(BuildRegistration)]);

        var sp = new ServiceCollection().BuildServiceProvider();
        r_providers.Add(sp);

        return new ConsumerHostedService(
            connection,
            registry,
            sp.GetRequiredService<IServiceScopeFactory>(),
            new HandlerDispatcherFactory(registry, MessagingCatalog.Default),
            MessagingCatalog.Default,
            topology,
            NullLoggerFactory.Instance,
            NullLogger<ConsumerHostedService>.Instance,
            Options.Create(new RabbitMqConnectionOptions()),
            Options.Create(new RabbitMqSubscriberOptions()));
    }
}
