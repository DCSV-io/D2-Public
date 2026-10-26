// -----------------------------------------------------------------------
// <copyright file="ReadyConnectionStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Subscribing.Fixtures;

using DcsvIo.D2.Messaging.RabbitMq.Connection;
using global::RabbitMQ.Client;

/// <summary>
/// Connection stub that is ready immediately and returns one recording channel
/// per <c>CreateChannelAsync</c> call. Setting <see cref="ParkAtCall"/> makes the
/// numbered call wait until <see cref="Release"/> is called, so the background
/// start waits midway through opening its channels.
/// </summary>
internal sealed class ReadyConnectionStub : ID2Connection
{
    private readonly List<RecordingChannelStub> r_created = [];

    private readonly TaskCompletionSource r_parked =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource r_release =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _calls;

    public bool IsOpen => true;

    public Task ReadyTask { get; } = Task.CompletedTask;

    /// <summary>Gets the one-based call number to park on; zero never parks.</summary>
    public int ParkAtCall { get; init; }

    /// <summary>Gets a task that completes once the parked call has been reached.</summary>
    public Task Parked => r_parked.Task;

    /// <summary>Gets every channel returned so far.</summary>
    public IReadOnlyList<RecordingChannelStub> Created
    {
        get
        {
            lock (r_created)
                return r_created.ToArray();
        }
    }

    /// <summary>Gets or sets an observer run on the calling thread as each
    /// channel is requested, before any park.</summary>
    public Action? CreateChannelObserver { get; set; }

    /// <summary>Lets the parked call return its channel.</summary>
    public void Release() => r_release.TrySetResult();

    public void StartReconnectLoop()
    {
    }

    public async ValueTask<IChannel> CreateChannelAsync(
        CreateChannelOptions? options = null,
        CancellationToken ct = default)
    {
        CreateChannelObserver?.Invoke();

        if (Interlocked.Increment(ref _calls) == ParkAtCall)
        {
            r_parked.TrySetResult();
            await r_release.Task;
        }

        var channel = new RecordingChannelStub();
        lock (r_created)
            r_created.Add(channel);

        return channel;
    }

    public ValueTask DisposeAsync() => default;
}
