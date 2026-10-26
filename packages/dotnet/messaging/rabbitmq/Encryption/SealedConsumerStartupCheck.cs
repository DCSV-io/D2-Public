// -----------------------------------------------------------------------
// <copyright file="SealedConsumerStartupCheck.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Encryption;

using DcsvIo.D2.Encryption;
using DcsvIo.D2.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Fails host startup when a subscriber consumes a message on a sealed
/// encryption domain but no keyed <see cref="IPayloadOpener"/> is registered for
/// that domain's consumer service. Such a consumer could never open a delivery,
/// so every message would go to the DLQ.
/// </summary>
/// <remarks>
/// <para>
/// <c>AddD2MessagingRabbitMq</c> registers this check unconditionally and
/// never through the sealing registration call that the check guards: a check
/// registered by the call it protects would not run when that call is
/// forgotten, which is the case the check exists to catch.
/// </para>
/// <para>
/// Per registration the resolution chain is
/// <c>SubscriberRegistry.All -&gt; ISubscriberRegistration.MessageType -&gt;
/// MessagingCatalog.RequireDescriptor -&gt; MqMessageDescriptor -&gt; IsSealed</c>; a sealed
/// descriptor requires a keyed <see cref="IPayloadOpener"/> for its
/// <see cref="MqMessageDescriptor.ConsumerService"/>. Presence is probed via
/// <see cref="IServiceProviderIsKeyedService"/> (the registration only: the
/// opener's own boot fetch fails loudly when the sealed self-check resolves
/// it, not here). The throw happens at host start, before any consumer channel
/// opens, and its message lists the queue, handler, domain, consumer service and
/// the fix.
/// </para>
/// <para>
/// The check has no effect on a host whose catalog contains no sealed domain.
/// The shipped message catalog has no entry on a sealed domain today, so no
/// production subscriber can trip the check. It is exercised by handing the check a <see cref="MessagingCatalog"/> of
/// fixture message types whose descriptors carry real sealed domain values.
/// </para>
/// </remarks>
internal sealed class SealedConsumerStartupCheck : IHostedService
{
    private readonly SubscriberRegistry r_registry;
    private readonly MessagingCatalog r_catalog;
    private readonly IServiceProvider r_serviceProvider;

    /// <summary>Initializes the check.</summary>
    /// <param name="registry">Subscriber registry.</param>
    /// <param name="catalog">The message descriptors this host consumes
    /// against.</param>
    /// <param name="serviceProvider">
    /// Used to probe for a keyed <see cref="IPayloadOpener"/> registration.
    /// </param>
    public SealedConsumerStartupCheck(
        SubscriberRegistry registry,
        MessagingCatalog catalog,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        r_registry = registry;
        r_catalog = catalog;
        r_serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var isKeyed = r_serviceProvider.GetService<IServiceProviderIsKeyedService>();

        foreach (var registration in r_registry.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var descriptor = MessageWireResolver.Resolve(r_catalog, registration.MessageType);

            if (!descriptor.IsSealed)
                continue;

            // A sealed descriptor always carries a consumer service (the catalog
            // binds the two together); guard defensively regardless.
            var consumerService = descriptor.ConsumerService
                ?? throw new InvalidOperationException(
                    $"Subscriber '{registration.HandlerType.FullName}' (queue "
                    + $"'{registration.ResolvedQueueName}') consumes sealed domain "
                    + $"'{descriptor.Encryption}' but the generated catalog names no "
                    + "consumer service for it - the encryption-domain catalog is "
                    + "internally inconsistent.");

            var hasOpener = isKeyed?.IsKeyedService(typeof(IPayloadOpener), consumerService)
                ?? false;

            if (!hasOpener)
            {
                throw new InvalidOperationException(
                    $"Subscriber '{registration.HandlerType.FullName}' (queue "
                    + $"'{registration.ResolvedQueueName}') consumes messages on the sealed "
                    + $"encryption domain '{descriptor.Encryption}' (consumer service "
                    + $"'{consumerService}'), but no keyed IPayloadOpener is registered for "
                    + "that consumer service - every delivery would fail to open and DLQ. "
                    + $"Register a keyed IPayloadOpener for \"{consumerService}\", backed by a "
                    + "managed keyring on the consumer host, so it can open its own sealed "
                    + "frames.");
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
