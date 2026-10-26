// -----------------------------------------------------------------------
// <copyright file="MessagingServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging;

using System.Reflection;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Result;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Transport-agnostic DI helpers for DcsvIo.D2.Messaging. The
/// transport-specific composition root (e.g.
/// <c>services.AddD2MessagingRabbitMq(...)</c>) lives in the impl lib and
/// builds on these primitives.
/// </summary>
public static class MessagingServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// CANONICAL subscriber registration path. Scans the supplied
        /// assemblies (defaults to the calling assembly) for classes
        /// marked with <see cref="MqSubAttribute"/>. For each one:
        /// validates the handler derives from
        /// <c>BaseHandler&lt;THandler, TMessage, Unit&gt;</c>; resolves
        /// the subscription descriptor from
        /// <see cref="MessagingCatalog.Default"/>; verifies the
        /// handler's <c>TMessage</c> matches the descriptor's
        /// <c>MessageTypeName</c>; registers the handler as Transient
        /// plus an <see cref="ISubscriberRegistration"/> entry.
        /// </summary>
        /// <param name="assemblies">Assemblies to scan. Empty / null
        /// defaults to <see cref="Assembly.GetCallingAssembly"/>.</param>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2SubscribersFromAssembly(
            params Assembly[] assemblies)
        {
            var scan = assemblies is { Length: > 0 }
                ? assemblies
                : [Assembly.GetCallingAssembly()];

            return services.AddD2SubscribersFromAssembly(MessagingCatalog.Default, scan);
        }

        /// <summary>
        /// The same scan against a supplied catalog, which can contain
        /// subscription constants beyond the framework's own; the
        /// parameterless overload reads
        /// <see cref="MessagingCatalog.Default"/>, which contains only the
        /// framework's own entries.
        /// </summary>
        /// <param name="catalog">The catalog whose subscription descriptors
        /// each <c>[MqSub]</c> constant is looked up in.</param>
        /// <param name="assemblies">Assemblies to scan. Empty / null
        /// defaults to <see cref="Assembly.GetCallingAssembly"/>.</param>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2SubscribersFromAssembly(
            MessagingCatalog catalog,
            params Assembly[] assemblies)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(catalog);
            var scan = assemblies is { Length: > 0 }
                ? assemblies
                : [Assembly.GetCallingAssembly()];

            services.TryAddSingleton<SubscriberRegistry>();

            foreach (var assembly in scan)
            {
                foreach (var handlerType in assembly.GetTypes())
                {
                    var attr = handlerType.GetCustomAttribute<MqSubAttribute>(inherit: false);
                    if (attr is null) continue;

                    SubscriberRegistrar.Register(
                        services, handlerType, attr.Constant, catalog.Subscriptions);
                }
            }

            return services;
        }

        /// <summary>
        /// Programmatic subscriber registration - useful for tests + dynamic
        /// scenarios where the assembly scan isn't appropriate. Production
        /// code should prefer <c>AddD2SubscribersFromAssembly</c>.
        /// </summary>
        /// <typeparam name="THandler">Handler type.</typeparam>
        /// <typeparam name="TMessage">Message type the handler consumes.</typeparam>
        /// <param name="descriptor">The pre-built subscription descriptor.</param>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2Subscriber<THandler, TMessage>(
            MqSubscriptionDescriptor descriptor)
            where THandler : BaseHandler<THandler, TMessage, Unit>
            where TMessage : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(descriptor);

            services.TryAddTransient<THandler>();
            services.TryAddSingleton<SubscriberRegistry>();

            var resolvedQueueName = SubscriberRegistrar.ResolveQueueName(descriptor);
            services.AddSingleton<ISubscriberRegistration>(
                _ => new SubscriberRegistration(
                    typeof(THandler),
                    typeof(TMessage),
                    descriptor,
                    resolvedQueueName));

            return services;
        }
    }
}
