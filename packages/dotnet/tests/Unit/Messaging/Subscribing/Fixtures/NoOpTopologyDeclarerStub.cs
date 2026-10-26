// -----------------------------------------------------------------------
// <copyright file="NoOpTopologyDeclarerStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Subscribing.Fixtures;

using DcsvIo.D2.Messaging.RabbitMq.Topology;

/// <summary>Topology declarer stub that succeeds without touching a broker.</summary>
internal sealed class NoOpTopologyDeclarerStub : ITopologyDeclarer
{
    public ValueTask DeclareAsync(CancellationToken ct) => default;
}
