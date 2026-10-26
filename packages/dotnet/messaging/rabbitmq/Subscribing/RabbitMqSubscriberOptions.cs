// -----------------------------------------------------------------------
// <copyright file="RabbitMqSubscriberOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Subscribing;

using NodaTime;

/// <summary>Bounds dead-letter transport operations and retained cleanup waits.</summary>
public sealed class RabbitMqSubscriberOptions
{
    /// <summary>Initializes subscriber options with the default durations.</summary>
    public RabbitMqSubscriberOptions()
        : this(null, null)
    {
    }

    /// <summary>Initializes subscriber options with optional duration overrides.</summary>
    /// <param name="dlqRepublishTimeout">Operation duration, defaulting to five seconds.</param>
    /// <param name="dlqCleanupTimeout">Cleanup wait duration, defaulting to one second.</param>
    public RabbitMqSubscriberOptions(Duration? dlqRepublishTimeout, Duration? dlqCleanupTimeout)
    {
        DlqRepublishTimeout = dlqRepublishTimeout ?? Duration.FromSeconds(5);
        DlqCleanupTimeout = dlqCleanupTimeout ?? Duration.FromSeconds(1);
    }

    /// <summary>
    /// Gets or sets the duration from admission through confirmed mandatory publication.
    /// Defaults to five seconds. Requires whole milliseconds from 1 through 2147483647.
    /// </summary>
    public Duration DlqRepublishTimeout { get; set; }

    /// <summary>
    /// Gets or sets the additional wait for cleanup after forced transport termination.
    /// Defaults to one second. Requires whole milliseconds from 1 through 2147483647.
    /// Pending cleanup remains owned after this wait ends.
    /// </summary>
    public Duration DlqCleanupTimeout { get; set; }

    /// <summary>Checks the common .NET and Node timer range without rounding.</summary>
    /// <param name="duration">The configured duration.</param>
    /// <returns>Whether the duration is an exact supported millisecond count.</returns>
    internal static bool IsValidDuration(Duration duration)
    {
        if (duration < Duration.FromMilliseconds(1)
            || duration > Duration.FromMilliseconds(int.MaxValue)) return false;

        return duration == Duration.FromMilliseconds((long)duration.TotalMilliseconds);
    }
}
