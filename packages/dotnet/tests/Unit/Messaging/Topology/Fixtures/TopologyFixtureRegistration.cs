// -----------------------------------------------------------------------
// <copyright file="TopologyFixtureRegistration.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Topology.Fixtures;

using DcsvIo.D2.Messaging;

/// <summary>Subscriber registration the topology-declarer tests hand to a
/// <c>SubscriberRegistry</c>.</summary>
/// <param name="HandlerType">Handler CLR type.</param>
/// <param name="MessageType">Message CLR type.</param>
/// <param name="Descriptor">Subscription descriptor.</param>
/// <param name="ResolvedQueueName">Queue name after placeholder resolution.</param>
internal sealed record TopologyFixtureRegistration(
    Type HandlerType,
    Type MessageType,
    MqSubscriptionDescriptor Descriptor,
    string ResolvedQueueName) : ISubscriberRegistration;
