// -----------------------------------------------------------------------
// <copyright file="BoundedChannelPool.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Channels;

using System.Collections.Concurrent;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Telemetry;
using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Events;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Default bounded <see cref="IChannelPool"/>. Lazily creates up to
/// <see cref="ChannelPoolOptions.PublishPoolSize"/> channels; recycles
/// healthy channels on lease return; discards channels that have closed
/// (the next acquire creates a fresh one).
/// </summary>
[MustDisposeResource(false)]
internal sealed class BoundedChannelPool : IChannelPool
{
    private readonly ID2Connection r_connection;
    private readonly ChannelPoolOptions r_options;
    private readonly ILogger<BoundedChannelPool> r_logger;
    private readonly SemaphoreSlim r_slots;

    // Each entry pairs the channel with the tick count at which it was
    // returned to the pool. AcquireAsync compares that count with
    // ChannelPoolOptions.IdleTtl to evict idle entries. ConcurrentBag makes
    // insertion and removal lock-free. Its removal order is not strictly
    // last-in first-out, which does not matter because every acquire
    // re-checks each candidate.
    private readonly ConcurrentBag<IdleEntry> r_idle = [];
    private bool _disposed;

    /// <summary>Initializes the pool.</summary>
    /// <param name="connection">Connection wrapper.</param>
    /// <param name="options">Pool options.</param>
    /// <param name="logger">Logger.</param>
    [MustDisposeResource(false)]
    public BoundedChannelPool(
        ID2Connection connection,
        IOptions<ChannelPoolOptions> options,
        ILogger<BoundedChannelPool> logger)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        r_connection = connection;
        r_options = options.Value;
        r_logger = logger;

        if (r_options.PublishPoolSize < 1)
        {
            throw new InvalidOperationException(
                $"ChannelPoolOptions.PublishPoolSize must be >= 1; "
                + $"was {r_options.PublishPoolSize}.");
        }

        r_slots = new SemaphoreSlim(r_options.PublishPoolSize, r_options.PublishPoolSize);
    }

    /// <inheritdoc />
    public async ValueTask<ChannelLease> AcquireAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Wait for a slot - bounds total channel count to PublishPoolSize.
        var acquired = await r_slots.WaitAsync(r_options.AcquireTimeout, ct);
        if (!acquired)
        {
            throw new TimeoutException(
                $"Timed out after {r_options.AcquireTimeout.TotalSeconds:F1}s "
                + $"waiting for a publisher channel "
                + $"(pool size: {r_options.PublishPoolSize}). "
                + "Pool is likely backed up - check publisher confirm latency.");
        }

        // Try to reuse an idle channel; otherwise create one. Either way, the
        // slot semaphore keeps the number of live channels at or below
        // PublishPoolSize.
        IChannel? channel = null;
        var idleTtlTicks = (long)r_options.IdleTtl.TotalMilliseconds;
        var nowTicks = Environment.TickCount64;
        try
        {
            while (r_idle.TryTake(out var candidate))
            {
                if (!candidate.Channel.IsOpen)
                {
                    // Faulted channel - discard.
                    await DisposeChannelSafelyAsync(candidate.Channel);
                    continue;
                }

                // Idle-TTL eviction. A channel that stays in the pool
                // accumulates broker-side state (heartbeats, confirm-tracking
                // bookkeeping), which costs less to recreate than to keep
                // alive across hours of idleness.
                if (nowTicks - candidate.ReturnedAtTicks > idleTtlTicks)
                {
                    await DisposeChannelSafelyAsync(candidate.Channel);
                    continue;
                }

                channel = candidate.Channel;
                break;
            }

            channel ??= await CreateChannelAsync(ct);
            return new ChannelLease(channel, ReturnToPoolAsync);
        }
        catch
        {
            // The acquire failed, so release the slot to keep the pool's
            // capacity.
            r_slots.Release();
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;

        while (r_idle.TryTake(out var entry))
            await DisposeChannelSafelyAsync(entry.Channel);

        r_slots.Dispose();
    }

    [MustDisposeResource(false)]
    private async ValueTask<IChannel> CreateChannelAsync(CancellationToken ct)
    {
        var opts = new CreateChannelOptions(
            publisherConfirmationsEnabled: r_options.PublisherConfirmsEnabled,
            publisherConfirmationTrackingEnabled: r_options.PublisherConfirmsEnabled);
        var channel = await r_connection.CreateChannelAsync(opts, ct);

        // With confirmation tracking on, a return fails the publish itself and
        // the publisher handles it. With tracking off there is no sequence
        // number to match the return to and no publish left to fail, so the
        // pool counts and logs the return.
        if (!r_options.PublisherConfirmsEnabled)
            channel.BasicReturnAsync += OnBasicReturnAsync;

        return channel;
    }

    private Task OnBasicReturnAsync(object sender, BasicReturnEventArgs ea)
    {
        // Runs in the RabbitMQ client's event callback. It writes no shared
        // state: a counter add and a log call, both safe to call from any
        // thread.
        MessagingTelemetry.SR_PublishReturnsCounter.Add(1);
        MessagingLog.PublishReturnedUntracked(
            r_logger,
            ea.Exchange,
            ea.RoutingKey,
            ea.ReplyCode,
            ea.ReplyText);
        return Task.CompletedTask;
    }

    private async ValueTask ReturnToPoolAsync(IChannel channel)
    {
        try
        {
            // Dispose closed channels instead of pooling them.
            if (_disposed || !channel.IsOpen)
            {
                await DisposeChannelSafelyAsync(channel);
                return;
            }

            r_idle.Add(new IdleEntry(channel, Environment.TickCount64));
        }
        finally
        {
            r_slots.Release();
        }
    }

    private async ValueTask DisposeChannelSafelyAsync(IChannel channel)
    {
        if (!r_options.PublisherConfirmsEnabled)
            channel.BasicReturnAsync -= OnBasicReturnAsync;

        try
        {
            if (channel.IsOpen) await channel.CloseAsync();
        }
        catch (Exception ex)
        {
            ChannelPoolLog.ChannelCloseFailed(r_logger, ex.GetType().FullName ?? ex.GetType().Name);
        }

        try
        {
            await channel.DisposeAsync();
        }
        catch (Exception ex)
        {
            ChannelPoolLog.ChannelCloseFailed(r_logger, ex.GetType().FullName ?? ex.GetType().Name);
        }
    }

    private readonly record struct IdleEntry(IChannel Channel, long ReturnedAtTicks);
}
