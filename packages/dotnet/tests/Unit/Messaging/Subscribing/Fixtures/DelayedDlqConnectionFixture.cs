// -----------------------------------------------------------------------
// <copyright file="DelayedDlqConnectionFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Subscribing.Fixtures;

using System.Reflection;
using RabbitMQ.Client;

/// <summary>Controls acquisition completion and records late resource disposal.</summary>
public class DelayedDlqConnectionFixture : DispatchProxy
{
    private readonly TaskCompletionSource<IConnection> r_acquisition =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource r_started =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _disposals;
    private int _channelAcquisitions;
    private CancellationTokenRegistration _cancellationRegistration;

    public bool FailCancellation { get; set; }

    public bool FailDisposal { get; set; }

    public Task Started => r_started.Task;

    public int Disposals => Volatile.Read(ref _disposals);

    public int ChannelAcquisitions => Volatile.Read(ref _channelAcquisitions);

    public static (IConnection Connection, DelayedDlqConnectionFixture Fixture) Create()
    {
        object proxy = Create<IConnection, DelayedDlqConnectionFixture>();
        return ((IConnection)proxy, (DelayedDlqConnectionFixture)proxy);
    }

    public Task<IConnection> Acquire(CancellationToken cancellationToken)
    {
        if (FailCancellation)
        {
            _cancellationRegistration = cancellationToken.Register(
                () => throw new InvalidOperationException("fixture-private-cancellation-detail"));
        }

        r_started.TrySetResult();
        return r_acquisition.Task;
    }

    public void Complete(IConnection connection) => r_acquisition.TrySetResult(connection);

    protected override object Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        switch (targetMethod?.Name)
        {
            case nameof(IAsyncDisposable.DisposeAsync):
                Interlocked.Increment(ref _disposals);
                _cancellationRegistration.Dispose();
                if (FailDisposal)
                    throw new InvalidOperationException("fixture-private-disposal-detail");
                return ValueTask.CompletedTask;
            case nameof(IConnection.CreateChannelAsync):
                Interlocked.Increment(ref _channelAcquisitions);
                throw new InvalidOperationException("A retired connection cannot create a channel.");
            default:
                throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
