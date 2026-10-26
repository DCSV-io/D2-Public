// -----------------------------------------------------------------------
// <copyright file="ProductMeterServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry;

using DcsvIo.D2.Utilities.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI registration for a service's own metrics type. One call creates the
/// meter and adds its name to the meter provider.
/// </summary>
public static class ProductMeterServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <typeparamref name="TMetrics"/> as a singleton, registers the
        /// <c>IMeterFactory</c> it builds its instruments from, and adds
        /// <paramref name="meterName"/> to
        /// <see cref="D2TelemetryOptions.AdditionalMeters"/> - in one statement.
        /// This is the one way a service's meter is both created and listened to.
        /// A metrics type registered any other way emits into a process where
        /// nothing subscribes its meter, and no exporter ever sees its counters.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The write goes to D2's own options and never to the OpenTelemetry SDK,
        /// so a library still publishes its meter name as a constant rather than
        /// taking an SDK registration (ADR-0006, and item 2 of ADR-0010).
        /// <c>AddD2Telemetry</c> is the only place the SDK is told those names, and
        /// it reads them when the meter provider is built, so this call may come
        /// before or after it. Under <c>OTEL_SDK_DISABLED</c> no provider is
        /// registered at all and the name reaches nothing, which matches the
        /// behavior of the other registration methods in this package.
        /// </para>
        /// <para>
        /// The call is idempotent: the singleton is registered through
        /// <c>TryAdd</c> and the meter names are a set, so a second call for the
        /// same type registers nothing further. Make it from the composition that
        /// owns the module, and pass that type's own meter-name constant, so a
        /// rename of the constant is a compile error at this call site.
        /// </para>
        /// </remarks>
        /// <typeparam name="TMetrics">
        /// The service's metrics type. The container constructs it, so its
        /// dependencies - the <c>IMeterFactory</c> above among them - are resolved.
        /// </typeparam>
        /// <param name="meterName">
        /// The meter name <typeparamref name="TMetrics"/> creates its instruments
        /// under.
        /// </param>
        /// <returns>
        /// The same <paramref name="services"/> for fluent chaining.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="meterName"/> is null, empty or whitespace.
        /// </exception>
        public IServiceCollection AddD2Meter<TMetrics>(string meterName)
            where TMetrics : class
        {
            ArgumentNullException.ThrowIfNull(services);

            if (meterName.Falsey())
            {
                throw new ArgumentException(
                    "AddD2Meter requires a meter name. A registration with no name "
                    + "would build the metrics type and subscribe nothing, which is "
                    + "the split this seam exists to close.",
                    nameof(meterName));
            }

            services.AddMetrics();
            services.TryAddSingleton<TMetrics>();
            services.Configure<D2TelemetryOptions>(
                options => options.AdditionalMeters.Add(meterName));

            return services;
        }
    }
}
