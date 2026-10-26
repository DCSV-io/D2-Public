// -----------------------------------------------------------------------
// <copyright file="ThrowingTopologyDeclarerStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Subscribing.Fixtures;

using DcsvIo.D2.Messaging.RabbitMq.Topology;

/// <summary>Topology declarer stub that faults the background start.</summary>
internal sealed class ThrowingTopologyDeclarerStub : ITopologyDeclarer
{
    public ValueTask DeclareAsync(CancellationToken ct)
        => throw new InvalidOperationException("declarer-fixture-blow-up");
}
