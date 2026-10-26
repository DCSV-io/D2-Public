// -----------------------------------------------------------------------
// <copyright file="PublicationProbeConnectionStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Subscribing.Fixtures;

using DcsvIo.D2.Messaging.RabbitMq.Connection;
using global::RabbitMQ.Client;

/// <summary>
/// Connection stub that records what the host had published at the moment the
/// background start first read it. <c>ReadyTask</c> is the first member the
/// background start reads, so its getter runs at the earliest instant the body
/// can observe anything.
/// </summary>
internal sealed class PublicationProbeConnectionStub : ID2Connection
{
    private readonly TaskCompletionSource r_observed =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _reads;

    public bool IsOpen => true;

    public Task ReadyTask
    {
        get
        {
            if (Interlocked.Increment(ref _reads) == 1)
            {
                PublishedWhenObserved = Probe?.Invoke();
                r_observed.TrySetResult();
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>Gets or sets the probe the getter runs on its first read.</summary>
    internal Func<bool>? Probe { get; set; }

    /// <summary>
    /// Gets the value that <see cref="Probe"/> returned on the first read, or
    /// <see langword="null"/> when the background start never got that far.
    /// </summary>
    internal bool? PublishedWhenObserved { get; private set; }

    /// <summary>Gets a task that completes once the first read has happened.</summary>
    internal Task Observed => r_observed.Task;

    public void StartReconnectLoop()
    {
    }

    public ValueTask<IChannel> CreateChannelAsync(
        CreateChannelOptions? options = null,
        CancellationToken ct = default)
        => new(new RecordingChannelStub());

    public ValueTask DisposeAsync() => default;
}
