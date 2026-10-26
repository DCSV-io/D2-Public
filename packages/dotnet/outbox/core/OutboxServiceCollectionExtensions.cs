// -----------------------------------------------------------------------
// <copyright file="OutboxServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI registration helpers for the database-agnostic half of the outbox.
/// </summary>
public static class OutboxServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the settings for one database's outbox. The storage half
        /// registers separately, from the entity-framework package, over the same
        /// <typeparamref name="TDb"/> and the concrete context that implements it.
        /// </summary>
        /// <typeparam name="TDb">
        /// The database this outbox belongs to, identified by its application-layer
        /// context interface. It is the identity of the outbox: two modules over
        /// two databases use two interfaces and so get their own lock key,
        /// connection string, staging seam, store and dispatcher set.
        /// </typeparam>
        /// <param name="configure">
        /// Sets the options, including the required relay advisory-lock key.
        /// </param>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        /// <remarks>
        /// <b>The settings are registered as <see cref="OutboxOptionsFor{TDb}"/>
        /// under the default options name; the plain <see cref="OutboxOptions"/>
        /// type is not registered, and no other options name is used.</b> An
        /// outbox is per database rather than per process: the staged row has to
        /// be added to the same unit of work as the change it is staged with, so
        /// two modules over two databases have two of everything. Identifying the
        /// database by its context interface keeps them apart, and the compiler
        /// checks that identifier. A configuration section bound to
        /// <see cref="OutboxOptionsFor{TDb}"/> before this call is applied first,
        /// so the values set here win.
        /// </remarks>
        public IServiceCollection AddD2Outbox<TDb>(Action<OutboxOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            services.Configure<OutboxOptionsFor<TDb>>(configure);

            return services;
        }

        /// <summary>
        /// Binds one dispatcher to one database's outbox. Call once per kind that
        /// is staged.
        /// </summary>
        /// <typeparam name="TDb">
        /// The database whose relay may hand this dispatcher messages - the same
        /// interface its <see cref="AddD2Outbox{TDb}"/> and storage registration
        /// were given.
        /// </typeparam>
        /// <typeparam name="TDispatcher">The dispatcher implementation.</typeparam>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        /// <remarks>
        /// <para>
        /// This method is the dispatcher registry; there is no separate type. The relay
        /// resolves the bindings carrying its own <typeparamref name="TDb"/> once
        /// when it starts and indexes them by
        /// <see cref="IOutboxDispatcher.Kind"/>.
        /// </para>
        /// <para>
        /// The binding's implementation type closes over both parameters, so
        /// registration deduplicates on the database and dispatcher pair: calling
        /// this twice for one dispatcher yields one binding and no error, and two
        /// different dispatchers both register, which is the ordinary shape of
        /// one dispatcher per kind. The relay's index fails to build if two of
        /// them claim the same kind.
        /// </para>
        /// <para>
        /// <typeparamref name="TDispatcher"/> is registered under its own type
        /// with TryAdd, so an earlier registration of that type is kept and the
        /// binding wraps the instance it produces. One
        /// dispatcher type serving two databases is therefore one instance
        /// behind both bindings, which a dispatcher can be: it holds a transport
        /// rather than state belonging to the database whose relay called it.
        /// </para>
        /// </remarks>
        public IServiceCollection AddD2OutboxDispatcher<TDb, TDispatcher>()
            where TDispatcher : class, IOutboxDispatcher
        {
            ArgumentNullException.ThrowIfNull(services);

            // The dispatcher is registered under its own type so the binding can
            // take it through the constructor. Both are singletons, because the
            // relay, itself a singleton, holds them for the life of the host.
            services.TryAddSingleton<TDispatcher>();

            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IOutboxDispatcherBinding<TDb>,
                    OutboxDispatcherBinding<TDb, TDispatcher>>());

            return services;
        }
    }
}
