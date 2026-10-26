// -----------------------------------------------------------------------
// <copyright file="IdempotencyStartupCheck.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Idempotency;

using DcsvIo.D2.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Fails host startup when one or more subscribers opt into idempotency
/// (the <c>idempotency</c> field on the spec entry, carried as
/// <see cref="MqSubscriptionDescriptor.Idempotency"/>) but neither a
/// functional <see cref="IDistributedCache"/> nor a host-provided
/// <see cref="IMessageIdempotencyStore"/> is registered.
/// </summary>
/// <remarks>
/// Without this guard the consumer would skip the idempotency check on every
/// delivery without any error, so the feature would look configured and do
/// nothing.
/// </remarks>
internal sealed class IdempotencyStartupCheck : IHostedService
{
    private readonly SubscriberRegistry r_registry;
    private readonly IServiceProvider r_serviceProvider;

    /// <summary>Initializes the check.</summary>
    /// <param name="registry">Subscriber registry.</param>
    /// <param name="serviceProvider">Used to probe for an
    /// <see cref="IDistributedCache"/> registration.</param>
    public IdempotencyStartupCheck(
        SubscriberRegistry registry, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        r_registry = registry;
        r_serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var idempotentSubs = r_registry.All
            .Where(r => r.Descriptor.Idempotency)
            .ToList();
        if (idempotentSubs.Count == 0) return Task.CompletedTask;

        // CacheIdempotencyStore needs IDistributedCache. If the cache is
        // registered, the default store works, so return early. If it is not,
        // the host may still have registered its own store (for example a
        // test's in-memory fake): resolve the registered instance and accept
        // any store that is not the default cache-backed type.
        var hasCache = r_serviceProvider.GetService<IDistributedCache>() is not null;
        if (hasCache) return Task.CompletedTask;

        try
        {
            var resolved = r_serviceProvider.GetService<IMessageIdempotencyStore>();
            if (resolved is not null and not CacheIdempotencyStore)
                return Task.CompletedTask;
        }
        catch
        {
            // Resolution threw - most likely the default CacheIdempotencyStore
            // failed to construct because IDistributedCache is missing, which
            // is the misconfiguration this check exists to detect.
            // Fall through to the throw below.
        }

        var subList = string.Join(
            ", ",
            idempotentSubs.Select(r =>
                $"{r.HandlerType.Name} (queue={r.ResolvedQueueName})"));
        throw new InvalidOperationException(
            $"One or more subscriptions declare idempotency=true in "
            + $"mq-subscriptions.spec.json but no IDistributedCache is "
            + $"registered to back the default IMessageIdempotencyStore. "
            + $"Either register IDistributedCache (e.g. "
            + $"AddD2DistributedCacheRedis) or set idempotency=false on "
            + $"the subscription spec entry. Affected: {subList}.");
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
