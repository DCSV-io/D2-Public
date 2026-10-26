// -----------------------------------------------------------------------
// <copyright file="OutboxRelayPostgresServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres;

using DcsvIo.D2.Outbox.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI registration for the PostgreSQL relay.
/// </summary>
public static class OutboxRelayPostgresServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the relay for one database. Pair this with
        /// <c>AddD2Outbox&lt;TDb&gt;</c>, which registers the settings,
        /// <c>AddD2OutboxEntityFrameworkCore&lt;TDb, TDbContext&gt;</c>, which
        /// registers the store the relay reads through, and one
        /// <c>AddD2OutboxDispatcher&lt;TDb, TDispatcher&gt;</c> per kind.
        /// </summary>
        /// <typeparam name="TDb">
        /// The database this relay drains, identified by its application-layer
        /// context interface.
        /// </typeparam>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        /// <remarks>
        /// <para>
        /// <b>Staging messages does not require a relay in the same process.</b> The
        /// store and the staging seam are provider-neutral and register through
        /// <c>AddD2OutboxEntityFrameworkCore</c>; the relay takes a PostgreSQL
        /// advisory lock, so it registers here and reaches the provider only in
        /// the composition that registers it. A relay registered for a database
        /// with no store fails at startup.
        /// </para>
        /// <para>
        /// <b>One relay per context interface; the closed generic relay type enforces it.</b>
        /// The hosted service deduplicates on its implementation type, which is
        /// <c>OutboxRelay&lt;TDb&gt;</c> - one closed type per interface - so a
        /// second call for one interface adds nothing and a call for another
        /// interface adds its own relay. <c>AddD2OutboxEntityFrameworkCore</c>
        /// records which context that interface stands for; which physical
        /// table the context maps is decided outside this package.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown at host start, when the hosted services are resolved, if no
        /// <see cref="IOutboxStore{TDb}"/> is registered.
        /// </exception>
        public IServiceCollection AddD2OutboxRelayPostgres<TDb>()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddHostedService<OutboxRelay<TDb>>();

            return services;
        }
    }
}
