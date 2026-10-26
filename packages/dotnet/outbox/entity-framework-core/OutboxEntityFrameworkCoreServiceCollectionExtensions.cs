// -----------------------------------------------------------------------
// <copyright file="OutboxEntityFrameworkCoreServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI registration helpers for the storage half of the outbox.
/// </summary>
public static class OutboxEntityFrameworkCoreServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the store and the staging seam for one database, over the
        /// concrete context that holds its table.
        /// </summary>
        /// <typeparam name="TDb">
        /// The database this outbox belongs to, identified by its application-layer
        /// context interface.
        /// </typeparam>
        /// <typeparam name="TDbContext">
        /// The concrete context. It must map <see cref="OutboxMessageRow"/>,
        /// which <see cref="OutboxMessageRowConfiguration"/> does.
        /// </typeparam>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        /// <remarks>
        /// <para>
        /// Pair with <c>AddD2Outbox&lt;TDb&gt;</c>, which registers the settings,
        /// and one <c>AddD2OutboxDispatcher&lt;TDb, TDispatcher&gt;</c> per
        /// kind; delivery also needs the provider's relay -
        /// <c>AddD2OutboxRelayPostgres&lt;TDb&gt;</c>.
        /// </para>
        /// <para>
        /// <b>The constraint rejects a mismatched pairing at compile time.</b> A
        /// context can only be bound under an interface it implements, so a
        /// pairing such as one module's seams registered over another module's
        /// context - which would make every staged row join a transaction that
        /// commits a different database - does not compile. Both seams register
        /// unkeyed, because <typeparamref name="TDb"/> already distinguishes two
        /// databases.
        /// </para>
        /// <para>
        /// <b>The pairing is recorded in both directions.</b> The constraint
        /// admits one context under every interface it implements and one
        /// interface for every context that implements it, and this call is the
        /// one place that sees both types, so it records the pair in both
        /// directions and rejects either collision: a second
        /// <typeparamref name="TDb"/> over a context already bound, and a second
        /// <typeparamref name="TDbContext"/> under a database already bound. The
        /// same pair twice is idempotent, and a rejected call records neither
        /// half. Without the first rejection one table would have two stores
        /// and, through them, two relays holding two advisory locks, each
        /// claiming rows the other has to dispatch. Without the second the seams
        /// are registered under the interface alone, so the second call's
        /// <c>TryAdd</c> would no-op and every handler taking
        /// <c>IMessageOutbox&lt;TDb&gt;</c> would stage onto the first context's
        /// change tracker while the second context is the one registered.
        /// </para>
        /// <para>
        /// <b>The check cannot see the table.</b> Both checks read the CLR
        /// types this call was given, so they enforce one context type per
        /// interface and one interface per context type. Two context classes
        /// mapping the one physical <c>outbox_messages</c> table are two
        /// registrations that DI cannot tell apart, and this package does not
        /// check that one context maps one outbox table.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <typeparamref name="TDbContext"/> is already registered
        /// under a different <typeparamref name="TDb"/>, or when
        /// <typeparamref name="TDb"/> is already registered with a different
        /// <typeparamref name="TDbContext"/>.
        /// </exception>
        public IServiceCollection AddD2OutboxEntityFrameworkCore<TDb, TDbContext>()
            where TDbContext : DbContext, TDb
        {
            ArgumentNullException.ThrowIfNull(services);

            BindContextAndDatabaseToEachOther<TDb, TDbContext>(services);

            services.TryAddScoped<IOutboxStore<TDb>, EfOutboxStore<TDb, TDbContext>>();
            services.TryAddScoped<IMessageOutbox<TDb>, EfMessageOutbox<TDb, TDbContext>>();

            return services;
        }
    }

    private static void BindContextAndDatabaseToEachOther<TDb, TDbContext>(
        IServiceCollection services)
    {
        var boundDatabase = services
            .FirstOrDefault(d => d.ServiceType == typeof(ContextDatabaseBinding<TDbContext>))
            ?.ImplementationInstance as ContextDatabaseBinding<TDbContext>;

        var boundContext = services
            .FirstOrDefault(d => d.ServiceType == typeof(DatabaseContextBinding<TDb>))
            ?.ImplementationInstance as DatabaseContextBinding<TDb>;

        // Both directions are read before either is recorded, so a refused call
        // leaves no binding behind to refuse the honest call that follows it.
        if (boundDatabase is not null && boundDatabase.Database != typeof(TDb))
        {
            throw new InvalidOperationException(
                $"The context '{typeof(TDbContext).FullName}' is already registered as "
                + $"the outbox for {boundDatabase.Database.Name}, so it cannot also be "
                + $"registered for {typeof(TDb).Name}. One database has one outbox: the "
                + "second would put a store, and through it a relay, over the same table "
                + "under its own advisory lock, and each relay would claim rows the other "
                + "has to dispatch. Register this context under one interface, or give "
                + "the other database its own context.");
        }

        if (boundContext is not null && boundContext.Context != typeof(TDbContext))
        {
            throw new InvalidOperationException(
                $"The database '{typeof(TDb).Name}' is already registered with the outbox "
                + $"context '{boundContext.Context.FullName}', so it cannot also be "
                + $"registered with '{typeof(TDbContext).FullName}'. One database has one "
                + "context: both seams are registered under the interface alone, so the "
                + "second registration is dropped and every handler taking "
                + $"IMessageOutbox<{typeof(TDb).Name}> stages onto the first context's "
                + "change tracker while this call named the second. Register one context "
                + "under this interface, or give the second context its own interface.");
        }

        if (boundDatabase is null)
        {
            services.Add(ServiceDescriptor.Singleton(
                new ContextDatabaseBinding<TDbContext>(typeof(TDb))));
        }

        if (boundContext is null)
        {
            services.Add(ServiceDescriptor.Singleton(
                new DatabaseContextBinding<TDb>(typeof(TDbContext))));
        }
    }

    /// <summary>
    /// Which database a concrete context was registered as the outbox for. It is
    /// a descriptor rather than a static table so it belongs to the one
    /// collection it was added to, and it is never resolved.
    /// </summary>
    /// <typeparam name="TDbContext">The concrete context.</typeparam>
    /// <param name="database">The interface the context was bound under.</param>
    private sealed class ContextDatabaseBinding<TDbContext>(Type database)
    {
        /// <summary>Gets the interface the context was bound under.</summary>
        public Type Database { get; } = database;
    }

    /// <summary>
    /// Which concrete context a database was registered with. The mirror of
    /// <see cref="ContextDatabaseBinding{TDbContext}"/>, and a separate closed
    /// type because neither key can find the other's collision.
    /// </summary>
    /// <typeparam name="TDb">The database's application-layer interface.</typeparam>
    /// <param name="context">The concrete context the database was bound with.</param>
    private sealed class DatabaseContextBinding<TDb>(Type context)
    {
        /// <summary>Gets the concrete context the database was bound with.</summary>
        public Type Context { get; } = context;
    }
}
