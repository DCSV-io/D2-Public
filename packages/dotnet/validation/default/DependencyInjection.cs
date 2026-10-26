// -----------------------------------------------------------------------
// <copyright file="DependencyInjection.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Validation;

using DcsvIo.D2.Validation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI registration entry point for <c>DcsvIo.D2.Validation</c>.
/// </summary>
public static class DependencyInjection
{
    /// <param name="services">The DI service collection to register into.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the default validator implementations as singletons:
        /// <see cref="DefaultEmailValidator"/> for <see cref="IEmailValidator"/>,
        /// <see cref="DefaultPhoneValidator"/> for <see cref="IPhoneValidator"/>,
        /// and <see cref="DefaultPostalCodeValidator"/> for
        /// <see cref="IPostalCodeValidator"/> (country-aware, from
        /// <c>DcsvIo.D2.Validation.Abstractions</c>).
        /// </summary>
        /// <remarks>
        /// The validators are stateless (regex patterns and phone-number
        /// metadata are <c>static readonly</c> with JIT pre-warm), so singleton
        /// lifetime is safe. To use a custom implementation, register
        /// an override with <c>services.Replace(...)</c> after calling this method.
        /// </remarks>
        /// <returns>The same <c>services</c> instance for call chaining.</returns>
        public IServiceCollection AddValidation()
        {
            services.TryAddSingleton<IEmailValidator, DefaultEmailValidator>();
            services.TryAddSingleton<IPhoneValidator, DefaultPhoneValidator>();
            services.TryAddSingleton<IPostalCodeValidator, DefaultPostalCodeValidator>();
            return services;
        }
    }
}
