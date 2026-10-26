// -----------------------------------------------------------------------
// <copyright file="DefaultTopologyDeclarer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Topology;

using System.Diagnostics;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Encryption;
using global::RabbitMQ.Client;
using Microsoft.Extensions.Logging;

/// <summary>
/// Topology declarer driven by the registered <see cref="SubscriberRegistry"/>.
/// Acquires a single dedicated channel, declares everything, and closes it.
/// The channel is not reused by the publish or consume paths.
/// </summary>
/// <remarks>
/// Declares once per instance: a second caller awaits the first caller's
/// declaration, so two callers do not each send a set of declare and bind
/// commands for one topology.
/// </remarks>
internal sealed class DefaultTopologyDeclarer : ITopologyDeclarer
{
    private readonly ID2Connection r_connection;
    private readonly SubscriberRegistry r_registry;
    private readonly MessagingCatalog r_catalog;
    private readonly ILogger<DefaultTopologyDeclarer> r_logger;

    // Guards _declaration. Several callers can reach DeclareAsync on this
    // singleton at once. The gate makes the second caller join the first
    // declaration instead of running its own.
    private readonly Lock r_declarationGate = new();

    private Task? _declaration;

    /// <summary>Initializes the declarer.</summary>
    /// <param name="connection">Connection wrapper.</param>
    /// <param name="registry">Subscriber registry to drive declarations.</param>
    /// <param name="catalog">The message descriptors the declared exchanges
    /// are read from.</param>
    /// <param name="logger">Logger.</param>
    public DefaultTopologyDeclarer(
        ID2Connection connection,
        SubscriberRegistry registry,
        MessagingCatalog catalog,
        ILogger<DefaultTopologyDeclarer> logger)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(logger);
        r_connection = connection;
        r_registry = registry;
        r_catalog = catalog;
        r_logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask DeclareAsync(CancellationToken ct)
    {
        Task declaration;

        // The memoized declaration is shared, so it runs on a token no single
        // caller owns. Each caller bounds its own wait through WaitAsync(ct)
        // below; passing the first caller's token here would cancel the shared
        // task for every joiner when that one caller was canceled, and hand a
        // caller whose own token is fine an OperationCanceledException raised on
        // a token it does not own.
        //
        // The declaration is built unstarted before the gate, published under
        // it, and run by the caller that published it after releasing the gate.
        // Started inside the lock, the declaration would reach its broker calls
        // with the gate held whenever the connection answered without yielding.
        // A caller that finds a declaration already published drops its own
        // unstarted one.
        var launch = new Task<Task>(() => DeclareOnceAsync(CancellationToken.None));
        var candidate = launch.Unwrap();

        lock (r_declarationGate)
            declaration = _declaration ??= candidate;

        if (ReferenceEquals(declaration, candidate))
            launch.RunSynchronously(TaskScheduler.Default);

        try
        {
            await declaration.WaitAsync(ct);
        }
        catch (Exception)
        {
            // Drop a declaration that faulted or was canceled, so the next caller
            // declares again instead of inheriting the failure. A caller whose own
            // token was canceled leaves an unfinished declaration in place for
            // whoever is still waiting on it.
            if (declaration.IsFaulted || declaration.IsCanceled)
            {
                lock (r_declarationGate)
                {
                    if (ReferenceEquals(_declaration, declaration))
                        _declaration = null;
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Queue durability / exclusivity / auto-delete flags per <see cref="QueuePattern"/>.
    /// A single definition keeps the flags of
    /// <see cref="QueuePattern.FanoutExclusiveAutoDelete"/> from drifting between
    /// declaration sites.
    /// </summary>
    /// <param name="pattern">The subscription queue pattern.</param>
    /// <returns>The declare flags for the primary queue.</returns>
    internal static (bool Durable, bool Exclusive, bool AutoDelete) QueueFlagsFor(
        QueuePattern pattern) => pattern switch
    {
        QueuePattern.CompetingConsumer => (Durable: true, Exclusive: false, AutoDelete: false),
        QueuePattern.DurableShared => (Durable: true, Exclusive: false, AutoDelete: false),
        QueuePattern.FanoutExclusiveAutoDelete =>
            (Durable: false, Exclusive: true, AutoDelete: true),
        _ => throw new ArgumentOutOfRangeException(nameof(pattern), pattern, null),
    };

    private static string ResolveExchange(MessagingCatalog catalog, Type messageType) =>
        MessageWireResolver.Resolve(catalog, messageType).Exchange;

    private static string ResolveExchangeType(MessagingCatalog catalog, Type messageType) =>
        MessageWireResolver.Resolve(catalog, messageType).ExchangeType;

    private async Task DeclareOnceAsync(CancellationToken ct)
    {
        if (r_registry.All.Count == 0)
        {
            TopologyLog.NoSubscribersToDeclare(r_logger);
            return;
        }

        TopologyLog.DeclarationStarted(r_logger, r_registry.All.Count);
        var stopwatch = Stopwatch.StartNew();

        // One channel for the whole declaration; it is closed on the way out.
        await using var channel = await r_connection.CreateChannelAsync(
            options: null, ct);

        foreach (var registration in r_registry.All)
        {
            try
            {
                await DeclareForSubscriberAsync(channel, registration, ct);
            }
            catch (Exception ex)
            {
                // Pass the exception type name only, never the
                // exception itself - `OperationInterruptedException.Message`
                // from RabbitMQ.Client can include broker-side text such as
                // PRECONDITION_FAILED arg dumps that operators may have
                // configured (declaration arguments, queue settings, etc.).
                TopologyLog.DeclarationFailed(
                    r_logger, ex.GetType().Name, registration.ResolvedQueueName);
                throw;
            }
        }

        stopwatch.Stop();
        TopologyLog.DeclarationCompleted(r_logger, stopwatch.Elapsed.TotalMilliseconds);
    }

    private async ValueTask DeclareForSubscriberAsync(
        IChannel channel,
        ISubscriberRegistration registration,
        CancellationToken ct)
    {
        var descriptor = registration.Descriptor;
        var queueName = registration.ResolvedQueueName;
        var exchange = ResolveExchange(r_catalog, registration.MessageType);
        var exchangeType = ResolveExchangeType(r_catalog, registration.MessageType);

        await channel.ExchangeDeclareAsync(
            exchange: exchange,
            type: exchangeType,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        var dlxName = DlqNaming.DlxFor(queueName);
        var dlqName = DlqNaming.DlqFor(queueName);

        // DLX (fanout - routing key irrelevant) -> DLQ.
        await channel.ExchangeDeclareAsync(
            exchange: dlxName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        await channel.QueueDeclareAsync(
            queue: dlqName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: dlqName,
            exchange: dlxName,
            routingKey: string.Empty,
            arguments: null,
            cancellationToken: ct);

        // Main queue with DLX argument.
        var queueArgs = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["x-dead-letter-exchange"] = dlxName,
            ["x-dead-letter-routing-key"] = string.Empty,
        };

        var (durable, exclusive, autoDelete) = QueueFlagsFor(descriptor.Pattern);
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: durable,
            exclusive: exclusive,
            autoDelete: autoDelete,
            arguments: queueArgs,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: exchange,
            routingKey: descriptor.RoutingKeyBinding,
            arguments: null,
            cancellationToken: ct);

        TopologyLog.SubscriberTopologyDeclared(
            r_logger,
            exchange,
            exchangeType,
            queueName,
            descriptor.Pattern.ToString(),
            dlxName,
            dlqName);

        if (descriptor.TieredRetry is { } retry)
            await DeclareTieredRetryAsync(channel, queueName, retry, ct);
    }

    private async ValueTask DeclareTieredRetryAsync(
        IChannel channel,
        string queueName,
        TieredRetryDescriptor retry,
        CancellationToken ct)
    {
        // Retry queues dead-letter on TTL expiry onto a "return" fanout exchange
        // that is bound back to the primary queue, which puts the message back
        // in front of the handler for another attempt.
        var returnExchange = DlqNaming.RetryReturnExchangeFor(queueName);
        await channel.ExchangeDeclareAsync(
            exchange: returnExchange,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: returnExchange,
            routingKey: string.Empty,
            arguments: null,
            cancellationToken: ct);

        for (var i = 0; i < retry.Tiers.Length; i++)
        {
            var tierExchange = DlqNaming.RetryTierExchangeFor(queueName, i);
            var tierQueue = DlqNaming.RetryTierQueueFor(queueName, i);
            var ttlMs = (long)retry.Tiers[i].TotalMilliseconds;

            await channel.ExchangeDeclareAsync(
                exchange: tierExchange,
                type: ExchangeType.Fanout,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: ct);

            var tierArgs = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["x-message-ttl"] = ttlMs,
                ["x-dead-letter-exchange"] = returnExchange,
                ["x-dead-letter-routing-key"] = string.Empty,
            };

            await channel.QueueDeclareAsync(
                queue: tierQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: tierArgs,
                cancellationToken: ct);

            await channel.QueueBindAsync(
                queue: tierQueue,
                exchange: tierExchange,
                routingKey: string.Empty,
                arguments: null,
                cancellationToken: ct);
        }

        TopologyLog.RetryTopologyDeclared(
            r_logger,
            retry.Tiers.Length,
            queueName,
            string.Join(",", retry.Tiers));
    }
}
