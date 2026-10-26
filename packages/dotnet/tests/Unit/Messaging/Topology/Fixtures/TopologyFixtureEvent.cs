// -----------------------------------------------------------------------
// <copyright file="TopologyFixtureEvent.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Topology.Fixtures;

using DcsvIo.D2.Messaging;

/// <summary>Message type the topology-declarer tests bind a subscriber to.
/// Its constant belongs to no shipped spec; the tests hand the declarer a
/// <see cref="MessagingCatalog"/> carrying its descriptor.</summary>
[MqPub(TopologyFixtureEvent.CONSTANT)]
internal sealed class TopologyFixtureEvent
{
    /// <summary>The constant the fixture catalog keys this type's descriptor
    /// under.</summary>
    public const string CONSTANT = "UnitTopologyFixture";
}
