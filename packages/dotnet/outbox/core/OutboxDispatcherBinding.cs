// -----------------------------------------------------------------------
// <copyright file="OutboxDispatcherBinding.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox;

/// <summary>
/// The binding the registration produces. It is internal because a consumer
/// registers through <c>AddD2OutboxDispatcher</c> and reads through
/// <see cref="IOutboxDispatcherBinding{TDb}"/>, and never references this type.
/// </summary>
/// <typeparam name="TDb">The database the dispatcher is bound to.</typeparam>
/// <typeparam name="TDispatcher">The dispatcher implementation.</typeparam>
/// <param name="dispatcher">The dispatcher this binding carries.</param>
internal sealed class OutboxDispatcherBinding<TDb, TDispatcher>(TDispatcher dispatcher)
    : IOutboxDispatcherBinding<TDb>
    where TDispatcher : class, IOutboxDispatcher
{
    /// <inheritdoc/>
    public IOutboxDispatcher Dispatcher { get; } = dispatcher;
}
