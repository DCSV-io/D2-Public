// -----------------------------------------------------------------------
// <copyright file="RabbitMqMessageBusReadinessTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Publishing;

using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq.Channels;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Publishing;
using global::RabbitMQ.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Tests of the <see cref="IMessageBus.WaitForReadyAsync"/> cancellation
/// contract. When the token is canceled before the connection is ready, the
/// method throws <see cref="OperationCanceledException"/>, so the host
/// shutdown sequence terminates cleanly.
/// </summary>
public sealed class RabbitMqMessageBusReadinessTests
{
    [Fact]
    public async Task WaitForReadyAsync_CanceledBeforeReady_ThrowsOperationCanceled()
    {
        // The connection never becomes ready, because its TaskCompletionSource
        // stays unresolved. The method must not complete normally, and the
        // exception must be OperationCanceledException or a type derived from
        // it.
        var bus = BuildBusWithStubConnection(neverReady: true);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));
        var token = cts.Token;

        var act = async () => await bus.WaitForReadyAsync(token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WaitForReadyAsync_AlreadyReady_CompletesImmediately()
    {
        var bus = BuildBusWithStubConnection(neverReady: false);
        await bus.WaitForReadyAsync(CancellationToken.None);
    }

    private static IMessageBus BuildBusWithStubConnection(bool neverReady)
    {
        var conn = new StubConnection(neverReady);
        var pool = new StubChannelPool();
        var publisherOpts = Options.Create(new RabbitMqPublisherOptions());
        var scopeFactory = new ServiceCollection().BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
        var logger = LoggerFactory.Create(_ => { })
            .CreateLogger<RabbitMqMessageBus>();
        return new RabbitMqMessageBus(
            pool,
            scopeFactory,
            conn,
            MessagingCatalog.Default,
            publisherOpts,
            logger);
    }

    private sealed class StubConnection : ID2Connection
    {
        public StubConnection(bool neverReady)
        {
            ReadyTask = neverReady
                ? new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously).Task
                : Task.CompletedTask;
        }

        public bool IsOpen => ReadyTask.IsCompletedSuccessfully;

        public Task ReadyTask { get; }

        public void StartReconnectLoop()
        {
        }

        public ValueTask<IChannel> CreateChannelAsync(
            CreateChannelOptions? options = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask DisposeAsync() => default;
    }

    private sealed class StubChannelPool : IChannelPool
    {
        public ValueTask<ChannelLease> AcquireAsync(CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask DisposeAsync() => default;
    }
}
