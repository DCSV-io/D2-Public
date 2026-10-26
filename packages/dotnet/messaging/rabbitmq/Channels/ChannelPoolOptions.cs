// -----------------------------------------------------------------------
// <copyright file="ChannelPoolOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Channels;

/// <summary>
/// Configuration for the bounded shared channel pool used by the publisher
/// path. Subscriber-side channels are dedicated per subscriber (one channel
/// each, owned by the consumer host) and not pulled from this pool.
/// </summary>
/// <remarks>
/// AMQP channels are not thread-safe; one channel per concurrent publisher
/// is required. Confirm-tracking state is per-channel too - sharing a single
/// channel across many publishers means slow confirms back up the entire
/// queue.
/// </remarks>
public sealed class ChannelPoolOptions
{
    /// <summary>
    /// Gets or sets the maximum channels held by the publisher pool.
    /// The default is 4. Increase it for high-throughput publishers whose
    /// confirms back up under sustained load.
    /// </summary>
    public int PublishPoolSize { get; set; } = 4;

    /// <summary>
    /// Gets or sets a value indicating whether channels created by the pool
    /// have publisher confirms enabled. The default is true, which accepts
    /// the per-publish round-trip cost of confirms for durability.
    /// </summary>
    public bool PublisherConfirmsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout the pool waits for a free channel when all
    /// pool slots are leased. The default is 30 seconds, well above normal
    /// confirm latency and short enough that a stuck pool fails with a
    /// <see cref="TimeoutException"/> instead of hanging indefinitely.
    /// </summary>
    public TimeSpan AcquireTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the idle TTL for pooled channels. On
    /// <c>BoundedChannelPool.AcquireAsync</c>, channels that have been
    /// idle (not leased) longer than this are disposed and replaced with
    /// a fresh one before being handed out. The default is 5 minutes, which
    /// bounds the connection state held on the broker side: heartbeat and
    /// publisher-confirm state ages out instead of leaking slowly in
    /// low-traffic services.
    /// </summary>
    public TimeSpan IdleTtl { get; set; } = TimeSpan.FromMinutes(5);
}
