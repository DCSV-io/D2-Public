// -----------------------------------------------------------------------
// <copyright file="MessagingCatalogFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Fixtures;

using System.Collections.Generic;
using DcsvIo.D2.Messaging;

/// <summary>
/// Message constants and catalogs for the messaging unit fixtures. The
/// generated registries hold no fixture entry, so a unit test of a catalog
/// lookup builds a catalog with this class and passes it to the type under
/// test.
/// </summary>
public static class MessagingCatalogFixture
{
    /// <summary>Constant on <c>SampleAuditEvent</c>.</summary>
    public const string SAMPLE_AUDIT = "FixtureSampleAudit";

    /// <summary>Constant on <c>SampleRotationEvent</c>.</summary>
    public const string SAMPLE_ROTATION = "FixtureSampleRotation";

    /// <summary>Constant on <see cref="SubscriberScanFixtureHandler"/>.</summary>
    public const string SCAN_SUBSCRIPTION = "FixtureScanSubscription";

    /// <summary>Builds a catalog over the supplied subscription descriptors,
    /// with no messages.</summary>
    /// <param name="descriptors">Descriptors, keyed by constant.</param>
    /// <returns>A catalog that holds exactly those subscriptions.</returns>
    public static MessagingCatalog ForSubscriptions(
        params (string Constant, MqSubscriptionDescriptor Descriptor)[] descriptors)
    {
        var subscriptions =
            new Dictionary<string, MqSubscriptionDescriptor>(StringComparer.Ordinal);

        foreach (var (constant, descriptor) in descriptors)
            subscriptions[constant] = descriptor;

        return new MessagingCatalog(
            new Dictionary<string, MqMessageDescriptor>(StringComparer.Ordinal),
            subscriptions);
    }

    /// <summary>Builds a competing-consumer subscription descriptor for a
    /// fixture handler's message type.</summary>
    /// <param name="constant">The constant that the handler's <c>[MqSub]</c>
    /// states.</param>
    /// <param name="messageType">The message CLR type the handler
    /// consumes.</param>
    /// <param name="queueName">The queue the subscription binds.</param>
    /// <returns>The descriptor.</returns>
    public static MqSubscriptionDescriptor Subscription(
        string constant, Type messageType, string queueName) =>
        new(
            Constant: constant,
            MessageTypeName: messageType.FullName!,
            QueueName: queueName,
            Pattern: QueuePattern.CompetingConsumer,
            RoutingKeyBinding: "#",
            Prefetch: 10,
            Idempotency: false,
            TieredRetry: null);

    /// <summary>Builds a catalog over the supplied message descriptors, with
    /// no subscriptions.</summary>
    /// <param name="descriptors">Descriptors, keyed by constant.</param>
    /// <returns>A catalog that holds exactly those messages.</returns>
    public static MessagingCatalog ForMessages(
        params (string Constant, MqMessageDescriptor Descriptor)[] descriptors)
    {
        var messages = new Dictionary<string, MqMessageDescriptor>(StringComparer.Ordinal);

        foreach (var (constant, descriptor) in descriptors)
            messages[constant] = descriptor;

        return new MessagingCatalog(
            messages,
            new Dictionary<string, MqSubscriptionDescriptor>(StringComparer.Ordinal));
    }

    /// <summary>Builds a plaintext descriptor for a fixture message type.</summary>
    /// <param name="constant">The constant that the type's <c>[MqPub]</c> states.</param>
    /// <param name="messageType">The fixture CLR type.</param>
    /// <param name="exchange">The exchange the descriptor routes to.</param>
    /// <param name="exchangeType">fanout, topic or direct.</param>
    /// <returns>The descriptor.</returns>
    public static MqMessageDescriptor Plaintext(
        string constant, Type messageType, string exchange, string exchangeType) =>
        new(
            Constant: constant,
            MessageTypeName: messageType.FullName!,
            Exchange: exchange,
            ExchangeType: exchangeType,
            Encryption: MqMessageDescriptor.PLAINTEXT,
            EncryptionReason: "Unit fixture exercising the plaintext code path.",
            DefaultRoutingKey: string.Empty);
}
