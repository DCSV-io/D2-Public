// -----------------------------------------------------------------------
// <copyright file="DlqTransportLifetimeIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Tests.Integration.Messaging.Fixtures;
using global::RabbitMQ.Client;
using Xunit;

/// <summary>Exercises forced termination while the broker withholds channel replies.</summary>
[Collection("RabbitMq")]
public sealed class DlqTransportLifetimeIntegrationTests(RabbitMqFixture fixture)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Terminate_WithheldChannelOpen_RemovesNetworkCapability()
    {
        await using var filter = new AmqpReplyFilterFixture(fixture.ConnectionString);
        await filter.InitializeAsync();
        var sharedFactory = new ConnectionFactory { Uri = new Uri(fixture.ConnectionString) };
        await using var shared = await sharedFactory.CreateConnectionAsync();
        await using var sharedChannel = await shared.CreateChannelAsync();
        var factory = new ConnectionFactory
        {
            Uri = new Uri(filter.ConnectionString),
            AutomaticRecoveryEnabled = false,
            TopologyRecoveryEnabled = false,
        };

        // Without the socket owner, cancellation leaves the sent channel-open RPC pending.
        using var sockets = new OwnedSocketSet(factory.SocketFactory);
        factory.SocketFactory = sockets.Create;
        await using var dedicated = await factory.CreateConnectionAsync();
        filter.SuppressChannelOpen();
        using var cancellation = new CancellationTokenSource();
        var acquisition = dedicated.CreateChannelAsync(cancellationToken: cancellation.Token);

        try
        {
            await filter.Suppressed.WaitAsync(TimeSpan.FromSeconds(10));
            await cancellation.CancelAsync();
            acquisition.IsCompleted.Should().BeFalse();
            sockets.Terminate();
            var observed = async () => await acquisition.WaitAsync(TimeSpan.FromSeconds(5));
            await observed.Should().ThrowAsync<Exception>()
                .Where(ex => ex.GetType() != typeof(TimeoutException));
            sockets.IsClosed.Should().BeTrue();
            sockets.SocketCount.Should().Be(0);
            shared.IsOpen.Should().BeTrue();
            var queue = await sharedChannel.QueueDeclareAsync();
            await sharedChannel.QueueDeleteAsync(queue.QueueName);
        }
        finally
        {
            sockets.Terminate();
            try
            {
                await acquisition;
            }
            catch (Exception)
            {
                // The assertion above distinguishes transport completion from a stuck guard.
            }
        }
    }
}
