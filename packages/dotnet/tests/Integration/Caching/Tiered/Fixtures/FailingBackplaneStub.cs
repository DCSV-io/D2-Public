// -----------------------------------------------------------------------
// <copyright file="FailingBackplaneStub.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Caching.Tiered.Fixtures;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DcsvIo.D2.Caching;
using DcsvIo.D2.Result;

/// <summary>
/// An invalidation backplane whose every publish returns <c>ServiceUnavailable</c>, as a
/// backplane does when the broker is unreachable.
/// </summary>
public sealed class FailingBackplaneStub : ICacheInvalidationBackplane
{
    /// <inheritdoc />
    /// <remarks>The subscription is the stub itself, whose disposal does nothing.</remarks>
    public IAsyncDisposable Subscribe(Func<string, CancellationToken, ValueTask> handler)
        => this;

    /// <inheritdoc />
    public ValueTask<D2Result> PublishInvalidationAsync(
        string key, CancellationToken ct = default)
    {
        return ValueTask.FromResult(D2Result.ServiceUnavailable());
    }

    /// <inheritdoc />
    public ValueTask<D2Result> PublishInvalidationManyAsync(
        IReadOnlyCollection<string> keys, CancellationToken ct = default)
    {
        return ValueTask.FromResult(D2Result.ServiceUnavailable());
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
