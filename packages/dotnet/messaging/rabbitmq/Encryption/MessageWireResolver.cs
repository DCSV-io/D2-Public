// -----------------------------------------------------------------------
// <copyright file="MessageWireResolver.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Encryption;

using System;
using DcsvIo.D2.Messaging;

/// <summary>
/// Resolves a message <see cref="Type"/> to its <see cref="MqMessageDescriptor"/>
/// against the catalog the host registered. The lookup itself is
/// <see cref="MessagingCatalog.RequireDescriptor"/>, in the abstractions;
/// this class delegates to it.
/// </summary>
internal static class MessageWireResolver
{
    /// <summary>Resolves the <see cref="MqMessageDescriptor"/> for
    /// <paramref name="messageType"/> against <paramref name="catalog"/>.
    /// Throws if the type is missing <see cref="MqPubAttribute"/>, references
    /// a constant the catalog does not carry, or its FQN does not match the
    /// registered descriptor.</summary>
    /// <param name="catalog">The catalog the host registered.</param>
    /// <param name="messageType">The message CLR type.</param>
    /// <returns>The descriptor for that type.</returns>
    public static MqMessageDescriptor Resolve(MessagingCatalog catalog, Type messageType)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return catalog.RequireDescriptor(messageType);
    }
}
