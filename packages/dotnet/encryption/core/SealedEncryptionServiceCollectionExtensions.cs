// -----------------------------------------------------------------------
// <copyright file="SealedEncryptionServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Registration helpers for the sealed-encryption startup self-check
/// machinery - the sealed sibling of
/// <see cref="EncryptionSourceServiceCollectionExtensions"/>. This is a
/// building-block seam that a registration source composes (a
/// managed single call, or the in-process twin). A service does not
/// call it directly; it composes one spec-driven registration call. The keyed
/// <see cref="IPayloadSealer"/> and/or <see cref="IPayloadOpener"/> are
/// registered separately under the same recipient service id, and this
/// method records the recipient so the hosted self-check verifies whichever
/// sides exist. It is public so that cross-assembly registration sources can
/// call it.
/// </summary>
public static class SealedEncryptionServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Records a sealed recipient for startup verification and ensures
        /// the hosted <see cref="SealedEncryptionStartupCheck"/> is
        /// registered. The keyed <see cref="IPayloadSealer"/> and/or
        /// <see cref="IPayloadOpener"/> are registered separately under
        /// <paramref name="recipientServiceId"/> - the check resolves
        /// whichever sides exist.
        /// </summary>
        /// <param name="recipientServiceId">
        /// The recipient service id the sealer/opener registrations are
        /// keyed by (lowercase <c>[a-z0-9-]</c>, at most 64 characters).
        /// </param>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2SealedEncryptionRecipient(string recipientServiceId)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton(new SealedEncryptionRegistration(recipientServiceId));
            services.AddD2SealedEncryptionStartupCheck();

            return services;
        }

        /// <summary>
        /// Registers <see cref="SealedEncryptionStartupCheck"/>, the hosted
        /// service that verifies every registered sealed recipient at host
        /// startup. Idempotent - the hosted service and the registry are
        /// each registered exactly once.
        /// </summary>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2SealedEncryptionStartupCheck()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton<SealedEncryptionRegistry>();
            services.AddHostedService<SealedEncryptionStartupCheck>();

            return services;
        }

        /// <summary>
        /// Registers <see cref="SealedEncryptionSourceStartupCheck"/>, the hosted service that
        /// enforces sealed-keyring provenance deny-by-default at host startup (a static /
        /// unmarked sealed recipient is rejected outside a Development host). A recipient passes
        /// when it has at least one marker and every marker under its id is <see cref="EncryptionKeyringSource.Managed"/>.
        /// Idempotent - the hosted service is registered exactly once.
        /// </summary>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2SealedEncryptionSourceCheck()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton<SealedEncryptionRegistry>();
            services.AddHostedService<SealedEncryptionSourceStartupCheck>();

            return services;
        }
    }
}
