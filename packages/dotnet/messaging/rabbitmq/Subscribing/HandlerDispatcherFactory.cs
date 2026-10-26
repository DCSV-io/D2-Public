// -----------------------------------------------------------------------
// <copyright file="HandlerDispatcherFactory.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Subscribing;

using System.Collections.Concurrent;
using System.Diagnostics;

/// <summary>
/// Pre-builds typed <see cref="IHandlerDispatcher"/> instances per
/// <see cref="ISubscriberRegistration"/>. Reflection runs once per
/// registration at startup; per-message dispatch is then a dictionary
/// lookup and a virtual call.
/// </summary>
internal sealed class HandlerDispatcherFactory
{
    private readonly ConcurrentDictionary<string, IHandlerDispatcher> r_byQueue =
        new(StringComparer.Ordinal);

    private readonly SubscriberRegistry r_registry;

    private readonly MessagingCatalog r_catalog;

    /// <summary>Initializes the factory.</summary>
    /// <param name="registry">Source of subscriber registrations.</param>
    /// <param name="catalog">The message descriptors each built dispatcher
    /// decodes against.</param>
    public HandlerDispatcherFactory(SubscriberRegistry registry, MessagingCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(catalog);
        r_registry = registry;
        r_catalog = catalog;
    }

    /// <summary>Returns a dispatcher for the named queue (built on demand,
    /// cached).</summary>
    /// <param name="queueName">Queue name (matches a registered subscriber).</param>
    /// <returns>The dispatcher for <paramref name="queueName"/>.</returns>
    /// <exception cref="InvalidOperationException">No subscriber registered
    /// for that queue.</exception>
    /// <exception cref="ArgumentException">The registration's types are
    /// incompatible with <c>BaseHandler&lt;TSelf, TIn, Unit&gt;</c>, which
    /// <see cref="Type.MakeGenericType"/> rejects.</exception>
    public IHandlerDispatcher GetForQueue(string queueName)
    {
        return r_byQueue.GetOrAdd(queueName, BuildDispatcher);
    }

    private IHandlerDispatcher BuildDispatcher(string queueName)
    {
        var reg = r_registry.All.FirstOrDefault(
            r => string.Equals(r.ResolvedQueueName, queueName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"No subscriber registered for queue '{queueName}'.");

        // Compose the closed generic. The handler must derive from
        // BaseHandler<TSelf, TMessage, Unit> so the dispatcher's where-clauses
        // are satisfied. MakeGenericType throws ArgumentException otherwise.
        // Activator.CreateInstance returns null only for a Nullable<T>, which
        // the closed dispatcher type is not.
        var openGeneric = typeof(TypedHandlerDispatcher<,>);
        var closedGeneric = openGeneric.MakeGenericType(reg.HandlerType, reg.MessageType);
        var instance = Activator.CreateInstance(closedGeneric, r_catalog);

        Debug.Assert(
            instance is not null,
            $"Activator.CreateInstance returned null for {closedGeneric.FullName}.");

        return (IHandlerDispatcher)instance;
    }
}
