// -----------------------------------------------------------------------
// <copyright file="IOutboxDispatcherBinding.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox;

/// <summary>
/// One dispatcher, bound to the database whose relay may hand it messages.
/// </summary>
/// <typeparam name="TDb">
/// The database this dispatcher was registered for, identified by its
/// application-layer context interface.
/// </typeparam>
/// <remarks>
/// <para>
/// The binding is a wrapper rather than a second interface a consumer
/// implements, so <see cref="IOutboxDispatcher"/> stays the only thing a
/// dispatcher class has to know about. Registration produces the binding; the
/// relay for a database resolves the bindings whose type argument is its own
/// <typeparamref name="TDb"/> and reaches no other module's transports.
/// </para>
/// <para>
/// The wrapper's implementation type closes over both the database and the
/// dispatcher, which is what lets the container deduplicate a repeated
/// registration of one dispatcher while keeping two different dispatchers
/// apart.
/// </para>
/// </remarks>
public interface IOutboxDispatcherBinding<TDb>
{
    /// <summary>Gets the dispatcher this binding carries.</summary>
    IOutboxDispatcher Dispatcher { get; }
}
