// -----------------------------------------------------------------------
// <copyright file="DefaultTopologyDeclarerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Topology;

using System.Collections.Generic;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq.Topology;
using DcsvIo.D2.Tests.Unit.Messaging.Subscribing.Fixtures;
using DcsvIo.D2.Tests.Unit.Messaging.Topology.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Coverage for <see cref="DefaultTopologyDeclarer"/>.
/// </summary>
public sealed class DefaultTopologyDeclarerTests
{
    private const string _EXCHANGE = "d2.test.unit-topology";

    [Fact]
    public async Task DeclareAsync_SecondCallerWhileFirstInFlight_DeclaresTopologyOnce()
    {
        // Both TopologyHostedService and ConsumerHostedService call DeclareAsync on
        // every host start, so two callers overlap on the same declarer. Two
        // declarations mean two queue.bind commands for one binding, and RabbitMQ
        // responds to the second bind from the route table while the first bind's
        // exchange-index update is still pending - so bind-ok can return before the
        // exchange will route to the queue. The consumer gates the first publish on
        // that bind-ok, and a message published in that window is confirmed and
        // dropped as unroutable. One declaration per declarer removes the window.
        var registry = BuildRegistry("unit.topology.once");
        var connection = new ReadyConnectionStub { ParkAtCall = 1 };
        var declarer = new DefaultTopologyDeclarer(
            connection, registry, BuildCatalog(), NullLogger<DefaultTopologyDeclarer>.Instance);

        var first = declarer.DeclareAsync(CancellationToken.None).AsTask();

        // The first caller is now inside CreateChannelAsync and cannot proceed until
        // it is released, so the second caller arrives while the first is in flight.
        await connection.Parked;

        var second = declarer.DeclareAsync(CancellationToken.None).AsTask();

        connection.Release();
        await first;
        await second;

        connection.Created.Should().ContainSingle(
            "a declarer that is driven twice must declare once - the declarer takes "
            + "exactly one channel per declaration");
    }

    [Fact]
    public async Task DeclareAsync_TheFirstCallerCancels_TheSecondStillDeclares()
    {
        // The memoized declaration used to start with the first caller's
        // token, so canceling that caller canceled the shared task for every
        // joiner. The second caller's own token is not canceled, and the
        // declaration it joins no longer depends on the first caller's token.
        var registry = BuildRegistry("unit.topology.cancel");
        var connection = new ReadyConnectionStub { ParkAtCall = 1 };
        var declarer = new DefaultTopologyDeclarer(
            connection, registry, BuildCatalog(), NullLogger<DefaultTopologyDeclarer>.Instance);

        using var firstToken = new CancellationTokenSource();
        var first = declarer.DeclareAsync(firstToken.Token).AsTask();

        await connection.Parked;

        var second = declarer.DeclareAsync(CancellationToken.None).AsTask();

        await firstToken.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);

        connection.Release();

        await second;

        connection.Created.Should().ContainSingle(
            "the declaration the second caller joined runs on a token no caller owns");
    }

    [Fact]
    public async Task DeclareAsync_TheConnectionAnswersAtOnce_RequestsTheChannelWithoutHoldingTheGate()
    {
        // A connection that returns without yielding lets the declaration run
        // synchronously up to its broker calls. A declaration started inside
        // the gate makes those calls with it held, and a joining caller blocks
        // on the gate behind broker I/O rather than awaiting the shared task.
        var registry = BuildRegistry("unit.topology.gate");
        var connection = new ReadyConnectionStub();
        var declarer = new DefaultTopologyDeclarer(
            connection, registry, BuildCatalog(), NullLogger<DefaultTopologyDeclarer>.Instance);

        var gate = DeclarationGateOf(declarer);
        bool? heldDuringChannelRequest = null;
        connection.CreateChannelObserver = () =>
            heldDuringChannelRequest = gate.IsHeldByCurrentThread;

        await declarer.DeclareAsync(CancellationToken.None);

        heldDuringChannelRequest.Should().BeFalse(
            "the declaration is published under the gate and run after its release");
    }

    /// <summary>Reads the declarer's private gate, so a test can ask whether
    /// the calling thread holds it.</summary>
    /// <param name="declarer">The declarer under test.</param>
    /// <returns>The gate.</returns>
    private static Lock DeclarationGateOf(DefaultTopologyDeclarer declarer)
    {
        var field = typeof(DefaultTopologyDeclarer).GetField(
            "r_declarationGate", BindingFlags.Instance | BindingFlags.NonPublic);

        field.Should().NotBeNull("the declarer holds its gate in r_declarationGate");

        return (Lock)field.GetValue(declarer)!;
    }

    private static MessagingCatalog BuildCatalog() => new(
        new Dictionary<string, MqMessageDescriptor>(StringComparer.Ordinal)
        {
            [TopologyFixtureEvent.CONSTANT] = new MqMessageDescriptor(
                Constant: TopologyFixtureEvent.CONSTANT,
                MessageTypeName: typeof(TopologyFixtureEvent).FullName!,
                Exchange: _EXCHANGE,
                ExchangeType: "topic",
                Encryption: MqMessageDescriptor.PLAINTEXT,
                EncryptionReason: "Unit fixture for topology declaration.",
                DefaultRoutingKey: string.Empty),
        },
        new Dictionary<string, MqSubscriptionDescriptor>(StringComparer.Ordinal));

    private static SubscriberRegistry BuildRegistry(string queueName)
    {
        var descriptor = new MqSubscriptionDescriptor(
            Constant: TopologyFixtureEvent.CONSTANT,
            MessageTypeName: typeof(TopologyFixtureEvent).FullName!,
            QueueName: queueName,
            Pattern: QueuePattern.CompetingConsumer,
            RoutingKeyBinding: "#",
            Prefetch: 5,
            Idempotency: false,
            TieredRetry: null);

        return new SubscriberRegistry([
            new TopologyFixtureRegistration(
                HandlerType: typeof(TopologyFixtureEvent),
                MessageType: typeof(TopologyFixtureEvent),
                Descriptor: descriptor,
                ResolvedQueueName: queueName),
        ]);
    }
}
