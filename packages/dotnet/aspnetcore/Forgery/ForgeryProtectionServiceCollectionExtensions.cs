// -----------------------------------------------------------------------
// <copyright file="ForgeryProtectionServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Forgery;

using DcsvIo.D2.Utilities.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI registration entry point for the forgery check.
/// </summary>
public static class ForgeryProtectionServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="D2ForgeryProtectionOptions"/>, bound from the
        /// <see cref="D2ForgeryProtectionOptions.SECTION"/> configuration
        /// section and validated at host build, plus the request-scoped
        /// <see cref="D2ForgeryTokenState"/> the middleware writes and the
        /// host's token issuer reads.
        /// </summary>
        /// <remarks>
        /// The token half of the check runs only where the host also registers
        /// an <see cref="ID2ForgeryTokenVerifier"/>. This extension registers
        /// none: a host with no session cookie has no session to bind a token
        /// to, and the fetch-metadata half needs nothing from the host.
        /// </remarks>
        /// <param name="configuration">The host's configuration.</param>
        /// <param name="configure">
        /// Optional customizer applied after the configuration binding.
        /// </param>
        /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> or
        /// <paramref name="configuration"/> is null.
        /// </exception>
        public IServiceCollection AddD2ForgeryProtection(
            IConfiguration configuration,
            Action<D2ForgeryProtectionOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddOptions<D2ForgeryProtectionOptions>()
                .Bind(configuration.GetSection(D2ForgeryProtectionOptions.SECTION))
                .Configure(opts => configure?.Invoke(opts))
                .Validate(
                    o => o.TokenCookieName.Truthy(),
                    "D2ForgeryProtectionOptions.TokenCookieName must not be empty / "
                    + "whitespace.")
                .Validate(
                    o => o.TokenHeaderName.Truthy(),
                    "D2ForgeryProtectionOptions.TokenHeaderName must not be empty / "
                    + "whitespace.")
                .Validate(
                    o => o.TokenCookieName.StartsWith(
                        D2ForgeryProtectionOptions.REQUIRED_COOKIE_NAME_PREFIX,
                        StringComparison.Ordinal),
                    "D2ForgeryProtectionOptions.TokenCookieName must start with "
                    + "\"__Host-\". The prefix binds the cookie to this exact origin "
                    + "and stops a sibling subdomain writing it.")
                .ValidateOnStart();

            services.AddScoped<D2ForgeryTokenState>();

            return services;
        }
    }
}
