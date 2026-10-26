// -----------------------------------------------------------------------
// <copyright file="ID2Connection.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Connection;

using global::RabbitMQ.Client;
using JetBrains.Annotations;

/// <summary>
/// Internal wrapper over <see cref="IConnection"/>. It hides
/// connection-lifecycle handling and can be replaced with a test double in
/// unit tests.
/// </summary>
/// <remarks>
/// The connection may be unavailable without failing the host:
/// <see cref="StartReconnectLoop"/> starts a background task that retries
/// forever with exponential backoff. <see cref="IsOpen"/> is false and
/// <see cref="ReadyTask"/> is incomplete until the first connection opens, and
/// <see cref="CreateChannelAsync"/> throws
/// <see cref="BrokerUnavailableException"/> while the connection is not open.
/// </remarks>
internal interface ID2Connection : IAsyncDisposable
{
    /// <summary>Gets a value indicating whether the underlying connection
    /// is currently open.</summary>
    bool IsOpen { get; }

    /// <summary>Gets a task that completes the first time the connection
    /// successfully opens. Later reconnects are handled automatically by
    /// RabbitMQ.Client's topology recovery and do not complete it again.</summary>
    Task ReadyTask { get; }

    /// <summary>
    /// Starts the background reconnect loop and returns immediately; the
    /// connection opens asynchronously. Calling it twice has no effect.
    /// </summary>
    void StartReconnectLoop();

    /// <summary>
    /// Creates a new AMQP channel against the underlying connection.
    /// </summary>
    /// <param name="options">
    /// Channel options (publisher confirms enabled, etc.). Pass null for
    /// defaults.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An open <see cref="IChannel"/>; caller owns disposal.</returns>
    /// <exception cref="BrokerUnavailableException">
    /// The connection is not currently open. A publish converts it to
    /// <c>D2Result.ServiceUnavailable</c>.
    /// </exception>
    [MustDisposeResource]
    ValueTask<IChannel> CreateChannelAsync(
        CreateChannelOptions? options = null, CancellationToken ct = default);
}
