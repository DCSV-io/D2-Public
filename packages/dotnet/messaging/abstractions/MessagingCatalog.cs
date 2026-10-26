// -----------------------------------------------------------------------
// <copyright file="MessagingCatalog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

/// <summary>
/// The message and subscription descriptors one host publishes and consumes
/// against. A host registers exactly one; the publisher, the consumer, the
/// topology declarer and every outbox dispatcher read the same instance.
/// </summary>
/// <remarks>
/// <para>
/// Default-deny: a message type without <see cref="MqPubAttribute"/>, or one
/// whose constant this catalog does not carry, throws from
/// <see cref="RequireDescriptor"/> rather than routing to an unintended
/// exchange.
/// </para>
/// <para>
/// The per-type resolution cache is per instance, not static. A static cache
/// keyed by CLR type alone is only safe while one immutable registry exists in
/// the process: with two catalogs the first one to touch a type would pin its
/// descriptor for the other.
/// </para>
/// </remarks>
/// <param name="Messages">Publisher contracts by message constant.</param>
/// <param name="Subscriptions">Subscription contracts by subscription
/// constant.</param>
public sealed record MessagingCatalog(
    IReadOnlyDictionary<string, MqMessageDescriptor> Messages,
    IReadOnlyDictionary<string, MqSubscriptionDescriptor> Subscriptions)
{
    private readonly ConcurrentDictionary<Type, MqMessageDescriptor> r_resolved = new();

    // The synthesized record copy constructor would copy the source's cache
    // to a catalog with different dictionaries, so a `with` expression would
    // return entries resolved against the dictionaries it replaced.
    private MessagingCatalog(MessagingCatalog original)
    {
        Messages = original.Messages;
        Subscriptions = original.Subscriptions;
        r_resolved = new ConcurrentDictionary<Type, MqMessageDescriptor>();
    }

    /// <summary>Gets the catalog built from the generated registries this
    /// assembly contains. A host that registers no catalog of its own gets this
    /// one, and a message type declared in a host catalog is then absent from it.</summary>
    public static MessagingCatalog Default { get; } = new(
        MqMessagesRegistry.ByConstant,
        MqSubscriptionsRegistry.ByConstant);

    /// <summary>Compares two catalogs by the dictionaries they were built from.
    /// Written out because the synthesized record equality would compare the
    /// resolution cache too, which no two instances ever share.</summary>
    /// <param name="other">The catalog to compare against.</param>
    /// <returns><see langword="true"/> when both were built from the same
    /// <c>Messages</c> and <c>Subscriptions</c> dictionary instances.</returns>
    public bool Equals(MessagingCatalog? other) =>
        other is not null
        && ReferenceEquals(Messages, other.Messages)
        && ReferenceEquals(Subscriptions, other.Subscriptions);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(
            RuntimeHelpers.GetHashCode(Messages),
            RuntimeHelpers.GetHashCode(Subscriptions));

    /// <summary>Resolves the publisher contract for a message type from its
    /// <see cref="MqPubAttribute"/>. Cached per type on this instance.</summary>
    /// <param name="messageType">The message CLR type.</param>
    /// <returns>The descriptor the catalog contains for that type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="messageType"/>
    /// is null.</exception>
    /// <exception cref="InvalidOperationException">The type carries no
    /// <see cref="MqPubAttribute"/>, the attribute's constant is absent from
    /// this catalog, or the entry names a different CLR type.</exception>
    public MqMessageDescriptor RequireDescriptor(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        return r_resolved.GetOrAdd(messageType, ResolveCore);
    }

    private MqMessageDescriptor ResolveCore(Type messageType)
    {
        var attr = messageType.GetCustomAttribute<MqPubAttribute>(inherit: false)
            ?? throw new InvalidOperationException(
                $"Message type '{messageType.FullName}' has no [MqPub] attribute. "
                + $"Apply [MqPub(MqMessages.YourConstant)] on the type. Add a "
                + $"corresponding entry to an mq-messages.spec.json the host's "
                + $"catalog is generated from. The publisher's default-deny "
                + $"posture means a forgotten attribute fails loud rather than "
                + $"silently routing to an unintended exchange.");

        if (!Messages.TryGetValue(attr.Constant, out var descriptor))
        {
            throw new InvalidOperationException(
                $"Message type '{messageType.FullName}' carries "
                + $"[MqPub(\"{attr.Constant}\")] but '{attr.Constant}' is not in "
                + $"the registered MessagingCatalog. Likely causes: the host "
                + $"registered no catalog of its own and got MessagingCatalog.Default, "
                + $"a stale build after a spec edit, a typo in the constant "
                + $"reference, or the spec entry was renamed without updating the "
                + $"attribute. Register the host's own catalog, rebuild, "
                + $"and verify the constant name in the mq-messages spec.");
        }

        if (!string.Equals(
            descriptor.MessageTypeName, messageType.FullName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Message type '{messageType.FullName}' carries "
                + $"[MqPub(\"{attr.Constant}\")] but the spec entry's messageType "
                + $"is '{descriptor.MessageTypeName}'. The CLR type name and the "
                + $"spec messageType must match exactly. Either rename the type or "
                + $"update the spec entry.");
        }

        return descriptor;
    }
}
