// -----------------------------------------------------------------------
// <copyright file="IOutboxDispatcher.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox;

using DcsvIo.D2.Result;

/// <summary>
/// Sends one kind of staged message. The transport is implemented in the
/// dispatcher and not in the relay, so the relay has no dependency on message
/// buses, notification ports or any other channel a message is sent through.
/// </summary>
public interface IOutboxDispatcher
{
    /// <summary>
    /// Gets the stored name of the consumer's own kind that this dispatcher
    /// sends. Each kind has one dispatcher: two dispatchers that claim one kind
    /// make the relay's dispatcher index fail to build.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Sends one message and reports whether the transport confirmed it.
    /// </summary>
    /// <param name="message">The message the relay claimed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok</c> on a confirmed send. Any failure re-stages the message with a
    /// longer backoff until the retry limit, and the failure's error code is
    /// recorded on the abandoned row.
    /// </returns>
    ValueTask<D2Result> DispatchAsync(OutboxMessage message, CancellationToken ct = default);
}
