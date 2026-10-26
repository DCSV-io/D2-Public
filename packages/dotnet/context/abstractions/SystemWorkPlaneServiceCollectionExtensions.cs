// -----------------------------------------------------------------------
// <copyright file="SystemWorkPlaneServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.Abstractions;

using DcsvIo.D2.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Platform registration for the System work plane: <see cref="MutableRequestContext"/>,
/// the default scoped <see cref="IRequestContext"/> and
/// <see cref="ISystemWorkScopeFactory"/>. Hosted and background authority-bearing
/// work MUST enter through the factory, and modules must not re-register
/// <see cref="IRequestContext"/>.
/// </summary>
public static class SystemWorkPlaneServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the platform System work plane:
        /// <list type="bullet">
        /// <item><description>
        /// Scoped <see cref="MutableRequestContext"/> (per-scope establishment
        /// target for System workers and other non-HTTP planes).
        /// </description></item>
        /// <item><description>
        /// Default scoped <see cref="IRequestContext"/> -> the scope's
        /// <see cref="MutableRequestContext"/>. The transport package of an HTTP or
        /// gRPC host registers a dual-path resolver that replaces this default. The
        /// resolver returns the Items slot when it is established and otherwise the
        /// Mutable context; pre-auth HTTP stays Unestablished and fail-closed in
        /// authority rules.
        /// </description></item>
        /// <item><description>
        /// Singleton <see cref="ISystemWorkScopeFactory"/>, the only supported
        /// entry for hosted System-plane work.
        /// </description></item>
        /// <item><description>
        /// <see cref="IClock"/> as <see cref="SystemClock"/> when not already
        /// registered (<c>TryAdd</c>).
        /// </description></item>
        /// </list>
        /// </summary>
        /// <remarks>
        /// <para>
        /// Call once from the host composition root, typically from whatever
        /// aggregate extension that root uses for its service defaults.
        /// Idempotent via <c>TryAdd*</c> for the factory / Mutable / clock; the
        /// default <see cref="IRequestContext"/> registration is also
        /// <c>TryAdd</c> so an HTTP dual-path registered earlier is preserved,
        /// while the HTTP and gRPC transport registrations replace a plain
        /// Mutable default when they run after this extension.
        /// </para>
        /// <para>
        /// Requires the host to bind <c>D2WorkloadIdentityOptions.ServiceId</c>
        /// (establishment-boundary registration) before
        /// <see cref="ISystemWorkScopeFactory.BeginAsync"/> runs; an empty
        /// service id makes that call throw.
        /// </para>
        /// </remarks>
        /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> is <see langword="null"/>.
        /// </exception>
        public IServiceCollection AddD2SystemWorkPlane()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton<IClock, SystemClock>();
            services.TryAddScoped<MutableRequestContext>();

            // Platform default for System workers, messaging and non-HTTP hosts. The
            // HTTP and gRPC transport packages replace it with the dual-path
            // Items||Mutable resolver when the host wires inbound HTTP or gRPC.
            services.TryAddScoped<IRequestContext>(
                static sp => sp.GetRequiredService<MutableRequestContext>());

            services.TryAddSingleton<ISystemWorkScopeFactory, SystemWorkScopeFactory>();

            return services;
        }
    }
}
