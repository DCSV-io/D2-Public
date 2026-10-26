// -----------------------------------------------------------------------
// <copyright file="MessagingRabbitMqServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq;

using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Messaging.RabbitMq.Channels;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Encryption;
using DcsvIo.D2.Messaging.RabbitMq.Idempotency;
using DcsvIo.D2.Messaging.RabbitMq.Publishing;
using DcsvIo.D2.Messaging.RabbitMq.Subscribing;
using DcsvIo.D2.Messaging.RabbitMq.Topology;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>
/// DI extensions wiring up the RabbitMQ messaging stack:
/// connection, channel pool, bus, topology declarer, and the connection +
/// topology hosted services.
/// </summary>
public static class MessagingRabbitMqServiceCollectionExtensions
{
    /// <param name="services">The service collection to extend.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers everything the RabbitMQ messaging lib needs:
        /// <see cref="ID2Connection"/> (singleton), <see cref="IChannelPool"/>
        /// (singleton), <see cref="IMessageBus"/> ->
        /// <c>RabbitMqMessageBus</c> (singleton - builds a transient scope
        /// per <c>PublishAsync</c> to resolve the keyed <c>IPayloadCrypto</c>
        /// + the calling scope's <c>IRequestContext</c> snapshot),
        /// <see cref="ITopologyDeclarer"/> (singleton),
        /// <see cref="MessagingCatalog"/> (singleton, defaulted to
        /// <see cref="MessagingCatalog.Default"/> when the host registered
        /// none), and the hosted services that drive
        /// idempotency-store-presence enforcement, connection startup,
        /// topology declaration, and consumer host.
        /// Idempotent - safe to call multiple times.
        /// </summary>
        /// <param name="configureConnection">
        /// Optional configuration for the connection (host / port / vhost /
        /// credentials / TLS / reconnect timing). No address is set by default,
        /// and starting the connection fails when the address is missing.
        /// Validation at composition rejects an address whose scheme is not <c>amqps</c>
        /// unless its host is loopback or
        /// <see cref="RabbitMqConnectionOptions.AllowUnencryptedTransport"/> is
        /// <c>true</c>.
        /// </param>
        /// <param name="configureChannelPool">
        /// Optional configuration for the publisher channel pool (pool size,
        /// acquire timeout, publisher confirms toggle).
        /// </param>
        /// <param name="configurePublisher">
        /// Optional configuration for the publisher (confirm timeout, retry
        /// counts, retry backoff curve).
        /// </param>
        /// <param name="configureSubscriber">Optional dead-letter operation and cleanup durations.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection AddD2MessagingRabbitMq(
            Action<RabbitMqConnectionOptions>? configureConnection = null,
            Action<ChannelPoolOptions>? configureChannelPool = null,
            Action<RabbitMqPublisherOptions>? configurePublisher = null,
            Action<RabbitMqSubscriberOptions>? configureSubscriber = null)
        {
            // Options - every block is independently configurable. The defaults
            // suit tests; production must override at least the connection.
            var connectionBuilder = services.AddOptions<RabbitMqConnectionOptions>();
            if (configureConnection is not null)
                connectionBuilder.Configure(configureConnection);

            // The address carries the broker credential, and every message body
            // crosses the same connection, so a transport an observer on the
            // network path can read is rejected before the host starts. The
            // dead-letter transport copies these options after validation.
            connectionBuilder
                .Validate(
                    options => options.IsTransportAdmitted(),
                    "RabbitMqConnectionOptions.ConnectionUri must use the amqps scheme unless its "
                    + "host is loopback or RabbitMqConnectionOptions.AllowUnencryptedTransport is true.")
                .ValidateOnStart();

            var channelPoolBuilder = services.AddOptions<ChannelPoolOptions>();
            if (configureChannelPool is not null)
                channelPoolBuilder.Configure(configureChannelPool);

            var publisherBuilder = services.AddOptions<RabbitMqPublisherOptions>();
            if (configurePublisher is not null)
                publisherBuilder.Configure(configurePublisher);

            var subscriberBuilder = services.AddOptions<RabbitMqSubscriberOptions>();
            if (configureSubscriber is not null)
                subscriberBuilder.Configure(configureSubscriber);

            subscriberBuilder
                .Validate(
                    options => RabbitMqSubscriberOptions.IsValidDuration(
                        options.DlqRepublishTimeout),
                    "RabbitMqSubscriberOptions.DlqRepublishTimeout requires whole milliseconds "
                    + "from 1 through 2147483647.")
                .Validate(
                    options => RabbitMqSubscriberOptions.IsValidDuration(options.DlqCleanupTimeout),
                    "RabbitMqSubscriberOptions.DlqCleanupTimeout requires whole milliseconds "
                    + "from 1 through 2147483647.")
                .ValidateOnStart();

            // Cross-options sanity check - WaitForConfirm requires
            // the channel to actually have publisher confirms enabled.
            // Without this guard, a typo in either option silently turns
            // every "confirmed" publish into an unsynchronized fire-and-
            // forget that the channel has no protocol mechanism to
            // confirm. Hard-fail at composition; mismatched intent must
            // be deliberate, not accidental.
            services
                .AddOptions<RabbitMqPublisherOptions>()
                .Validate(
                    publisher => publisher.ConfirmTimeout > TimeSpan.Zero
                        && publisher.ConfirmTimeout.TotalMilliseconds <= uint.MaxValue - 1L,
                    "RabbitMqPublisherOptions.ConfirmTimeout must be positive and no greater "
                    + "than 4294967294 milliseconds.")
                .Validate<IOptions<ChannelPoolOptions>>(
                    (publisher, channelPool) =>
                        !publisher.WaitForConfirm
                        || channelPool.Value.PublisherConfirmsEnabled,
                    "RabbitMqPublisherOptions.WaitForConfirm=true requires "
                    + "ChannelPoolOptions.PublisherConfirmsEnabled=true; the "
                    + "channel has no protocol mechanism to confirm a publish "
                    + "with confirms disabled.")
                .ValidateOnStart();

            // Connection + pool are singletons - one connection per process,
            // one channel pool over it. The bus is also a singleton; it
            // builds a transient DI scope per publish to resolve the keyed
            // IPayloadCrypto + the caller's IRequestContext snapshot, so
            // hosted services + other singletons can publish without
            // creating their own scope.
            // The message and subscription descriptors this host publishes and
            // consumes against. TryAdd, so a host that registered its own
            // catalog keeps it; a host that registered none gets the framework
            // catalog, in which a host-declared message type is absent and the first
            // publish of one throws InvalidOperationException.
            services.TryAddSingleton(MessagingCatalog.Default);

            services.TryAddSingleton<ID2Connection, RabbitMqConnection>();
            services.TryAddSingleton<IChannelPool, BoundedChannelPool>();
            services.TryAddSingleton<ITopologyDeclarer, DefaultTopologyDeclarer>();
            services.TryAddSingleton<IMessageBus, RabbitMqMessageBus>();

            // Scoped request context - handlers (BaseHandler) inject
            // IRequestContext through HandlerContext, so the per-message scope
            // must be able to resolve one. All fields are empty by default on
            // the consumer side; they are never populated from the wire (the
            // wire has no envelope, only the typed message payload).
            services.TryAddScoped<MutableRequestContext>();
            services.TryAddScoped<IRequestContext>(
                sp => sp.GetRequiredService<MutableRequestContext>());

            // Consumer host + dispatcher factory (singleton - pre-builds typed
            // dispatchers at startup from the SubscriberRegistry).
            services.TryAddSingleton<HandlerDispatcherFactory>();

            // Always-on registry. Subscribers register themselves via
            // AddD2Subscriber<,> in messaging-abstractions; the registry
            // collects them at construction time.
            services.TryAddSingleton<SubscriberRegistry>();

            // Default IMessageIdempotencyStore implementation - backed by
            // IDistributedCache with a 24-hour TTL. Applies only when (a) a
            // subscription's spec entry has idempotency=true AND (b)
            // IDistributedCache is registered. The IdempotencyStartupCheck
            // hosted service below fails fast when (a) is true but neither
            // (b) nor an operator-provided IMessageIdempotencyStore is
            // available - silent no-op on a safety feature is the worst
            // possible default.
            services.TryAddSingleton<IMessageIdempotencyStore, CacheIdempotencyStore>();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, IdempotencyStartupCheck>());

            // Sealed-consumer presence check - registered UNCONDITIONALLY (never
            // by the sealing call it guards), and BEFORE the consumer
            // host so a subscriber on a sealed domain with no matching
            // IPayloadOpener crashes boot before any consumer channel opens rather
            // than DLQ'ing every delivery. The forgotten-sealing-call case is
            // exactly what this catches.
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, SealedConsumerStartupCheck>());

            // Hosted services - registered as singletons through
            // TryAddEnumerable, which skips an implementation type that is
            // already registered (this keeps the method idempotent).
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, ConnectionStartupHostedService>());
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, TopologyHostedService>());
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, ConsumerHostedService>());

            return services;
        }
    }
}
