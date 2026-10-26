// -----------------------------------------------------------------------
// <copyright file="DependencyInjection.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.Default;

using DcsvIo.D2.Geo.Abstractions.NameResolution;
using DcsvIo.D2.Geo.Default.NameResolution;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI registration entry point for <c>DcsvIo.D2.Geo.Default</c>.
/// </summary>
public static class DependencyInjection
{
    /// <param name="services">The DI service collection to register into.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="DefaultGeoNameResolver"/> as the singleton
        /// implementation of <see cref="IGeoNameResolver"/>. The resolver is
        /// stateless and thread-safe, and the singleton lifetime spreads the
        /// catalog-cache build cost over the whole process. The DI factory
        /// does not build the cache eagerly - the first
        /// <see cref="IGeoNameResolver"/> call performs the O(n) build.
        /// </summary>
        /// <returns>The same <c>services</c> instance, so calls can be chained.</returns>
        public IServiceCollection AddD2GeoDefault()
        {
            services.AddSingleton<IGeoNameResolver, DefaultGeoNameResolver>();
            return services;
        }
    }
}
