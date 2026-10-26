// -----------------------------------------------------------------------
// <copyright file="RecordingChannelStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Subscribing.Fixtures;

using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Events;

/// <summary>Channel stub that counts its own disposals so a test can tell
/// a disposed subscriber channel from a leaked one.</summary>
internal sealed class RecordingChannelStub : IChannel
{
    private int _disposeAsyncCount;

    // The interface requires these; nothing here raises them. Declaring the
    // accessors rather than a field means there is no unused backing field to
    // suppress a warning about.

    public event AsyncEventHandler<BasicAckEventArgs>? BasicAcksAsync
    {
        add { }
        remove { }
    }

    public event AsyncEventHandler<BasicNackEventArgs>? BasicNacksAsync
    {
        add { }
        remove { }
    }

    public event AsyncEventHandler<BasicReturnEventArgs>? BasicReturnAsync
    {
        add { }
        remove { }
    }

    public event AsyncEventHandler<CallbackExceptionEventArgs>? CallbackExceptionAsync
    {
        add { }
        remove { }
    }

    public event AsyncEventHandler<FlowControlEventArgs>? FlowControlAsync
    {
        add { }
        remove { }
    }

    public event AsyncEventHandler<ShutdownEventArgs>? ChannelShutdownAsync
    {
        add { }
        remove { }
    }

    /// <summary>Gets the number of times this channel was disposed.</summary>
    public int DisposeAsyncCallCount => Volatile.Read(ref _disposeAsyncCount);

    /// <summary>Gets or sets an observer run on the calling thread by each
    /// shutdown call that reaches the broker: consumer cancel, close and
    /// dispose.</summary>
    public Action? BrokerCallObserver { get; set; }

    public int ChannelNumber => 1;

    public ShutdownEventArgs? CloseReason => null;

    public IAsyncBasicConsumer? DefaultConsumer { get; set; }

    public bool IsClosed => false;

    public bool IsOpen => true;

    public string? CurrentQueue => null;

    public TimeSpan ContinuationTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public Task BasicQosAsync(
        uint prefetchSize,
        ushort prefetchCount,
        bool global,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<string> BasicConsumeAsync(
        string queue,
        bool autoAck,
        string consumerTag,
        bool noLocal,
        bool exclusive,
        IDictionary<string, object?>? arguments,
        IAsyncBasicConsumer consumer,
        CancellationToken cancellationToken = default)
        => Task.FromResult(consumerTag.Length > 0 ? consumerTag : "ctag-fixture");

    public Task BasicCancelAsync(
        string consumerTag,
        bool noWait = false,
        CancellationToken cancellationToken = default)
    {
        BrokerCallObserver?.Invoke();
        return Task.CompletedTask;
    }

    public ValueTask BasicAckAsync(
        ulong deliveryTag,
        bool multiple,
        CancellationToken cancellationToken = default)
        => default;

    public ValueTask BasicNackAsync(
        ulong deliveryTag,
        bool multiple,
        bool requeue,
        CancellationToken cancellationToken = default)
        => default;

    public ValueTask BasicRejectAsync(
        ulong deliveryTag,
        bool requeue,
        CancellationToken cancellationToken = default)
        => default;

    public Task CloseAsync(
        ushort replyCode,
        string replyText,
        bool abort,
        CancellationToken cancellationToken = default)
    {
        BrokerCallObserver?.Invoke();
        return Task.CompletedTask;
    }

    public Task CloseAsync(ShutdownEventArgs reason, bool abort) => Task.CompletedTask;

    // Marked obsolete because the interface member it implements is. The
    // [Obsolete] attribute on this overload lets the stub implement a
    // deprecated IChannel member without suppressing the warning elsewhere.
    // Non-optional ct so this overload does not hide the two-arg form.
    [Obsolete("Implements an obsolete IChannel overload.")]
    public Task CloseAsync(
        ShutdownEventArgs reason,
        bool abort,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _disposeAsyncCount);
        BrokerCallObserver?.Invoke();
        return default;
    }

    public void Dispose()
    {
    }

    public Task ExchangeDeclareAsync(
        string exchange,
        string type,
        bool durable,
        bool autoDelete,
        IDictionary<string, object?>? arguments = null,
        bool passive = false,
        bool noWait = false,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<QueueDeclareOk> QueueDeclareAsync(
        string queue,
        bool durable,
        bool exclusive,
        bool autoDelete,
        IDictionary<string, object?>? arguments = null,
        bool passive = false,
        bool noWait = false,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new QueueDeclareOk(queue, 0, 0));

    public Task QueueBindAsync(
        string queue,
        string exchange,
        string routingKey,
        IDictionary<string, object?>? arguments = null,
        bool noWait = false,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<BasicGetResult?> BasicGetAsync(
        string queue,
        bool autoAck,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public ValueTask BasicPublishAsync<TProperties>(
        string exchange,
        string routingKey,
        bool mandatory,
        TProperties basicProperties,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default)
        where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        => throw new NotImplementedException();

    public ValueTask BasicPublishAsync<TProperties>(
        CachedString exchange,
        CachedString routingKey,
        bool mandatory,
        TProperties basicProperties,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default)
        where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        => throw new NotImplementedException();

    public Task ExchangeDeclarePassiveAsync(
        string exchange,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task ExchangeDeleteAsync(
        string exchange,
        bool ifUnused = false,
        bool noWait = false,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task ExchangeBindAsync(
        string destination,
        string source,
        string routingKey,
        IDictionary<string, object?>? arguments = null,
        bool noWait = false,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task ExchangeUnbindAsync(
        string destination,
        string source,
        string routingKey,
        IDictionary<string, object?>? arguments = null,
        bool noWait = false,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<QueueDeclareOk> QueueDeclarePassiveAsync(
        string queue,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<uint> QueueDeleteAsync(
        string queue,
        bool ifUnused,
        bool ifEmpty,
        bool noWait = false,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<uint> QueuePurgeAsync(
        string queue,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task QueueUnbindAsync(
        string queue,
        string exchange,
        string routingKey,
        IDictionary<string, object?>? arguments = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<uint> MessageCountAsync(
        string queue,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<uint> ConsumerCountAsync(
        string queue,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task TxCommitAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task TxRollbackAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task TxSelectAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public ValueTask<ulong> GetNextPublishSequenceNumberAsync(
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
