// -----------------------------------------------------------------------
// <copyright file="SampleOutboxDispatcher.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using DcsvIo.D2.Outbox;
using DcsvIo.D2.Result;

/// <summary>
/// A dispatcher a consumer would write, for the registration tests. It has a
/// parameterless constructor, which the container needs to build it.
/// </summary>
internal sealed class SampleOutboxDispatcher : IOutboxDispatcher
{
    /// <inheritdoc/>
    public string Kind => nameof(FakeOutboxKind.ThingHappened);

    /// <inheritdoc/>
    public ValueTask<D2Result> DispatchAsync(
        OutboxMessage message, CancellationToken ct = default)
        => ValueTask.FromResult(D2Result.Ok());
}
