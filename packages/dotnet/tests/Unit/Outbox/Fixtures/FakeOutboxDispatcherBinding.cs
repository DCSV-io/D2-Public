// -----------------------------------------------------------------------
// <copyright file="FakeOutboxDispatcherBinding.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using DcsvIo.D2.Outbox;

/// <summary>
/// Binds a dispatcher the test built to one database, so a test that constructs
/// a relay directly can hand it dispatchers without going through a container.
/// </summary>
/// <typeparam name="TDb">The database the dispatcher is bound to.</typeparam>
/// <param name="dispatcher">The dispatcher this binding holds.</param>
internal sealed class FakeOutboxDispatcherBinding<TDb>(IOutboxDispatcher dispatcher)
    : IOutboxDispatcherBinding<TDb>
{
    /// <inheritdoc/>
    public IOutboxDispatcher Dispatcher { get; } = dispatcher;

    /// <summary>Binds each dispatcher to <typeparamref name="TDb"/>.</summary>
    /// <param name="dispatchers">The dispatchers to bind.</param>
    /// <returns>The bindings, in the order given.</returns>
    public static IOutboxDispatcherBinding<TDb>[] Bind(params IOutboxDispatcher[] dispatchers)
        => [.. dispatchers.Select(d => new FakeOutboxDispatcherBinding<TDb>(d))];
}
