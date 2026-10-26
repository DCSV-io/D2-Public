// -----------------------------------------------------------------------
// <copyright file="OtherSampleOutboxDispatcher.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using DcsvIo.D2.Outbox;
using DcsvIo.D2.Result;

/// <summary>
/// A second dispatcher for a second kind, which is the ordinary shape - one
/// dispatcher per kind.
/// </summary>
internal sealed class OtherSampleOutboxDispatcher : IOutboxDispatcher
{
    /// <inheritdoc/>
    public string Kind => nameof(FakeOutboxKind.OtherThingHappened);

    /// <inheritdoc/>
    public ValueTask<D2Result> DispatchAsync(
        OutboxMessage message, CancellationToken ct = default)
        => ValueTask.FromResult(D2Result.Ok());
}
