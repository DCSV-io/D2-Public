// -----------------------------------------------------------------------
// <copyright file="FakeOutboxDispatcher.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using DcsvIo.D2.Outbox;
using DcsvIo.D2.Result;

/// <summary>
/// A dispatcher whose transport behavior the test sets. It records every
/// message it was handed so a test can assert the relay passed the whole
/// message rather than a truncated projection of it.
/// </summary>
/// <param name="kind">The stored kind name this dispatcher claims.</param>
internal sealed class FakeOutboxDispatcher(string kind) : IOutboxDispatcher
{
    private readonly List<OutboxMessage> r_received = [];

    /// <inheritdoc/>
    public string Kind { get; } = kind;

    /// <summary>Gets the messages this dispatcher was handed, in order.</summary>
    public IReadOnlyList<OutboxMessage> Received => r_received;

    /// <summary>Gets how many times the transport was called.</summary>
    public int Calls => r_received.Count;

    /// <summary>
    /// Gets or sets the result the transport returns. The default is a confirmed send.
    /// </summary>
    public Func<OutboxMessage, D2Result> Answer { get; set; } = _ => D2Result.Ok();

    /// <summary>
    /// Gets or sets an exception the transport throws instead of returning, for
    /// the arm where a client library faults rather than returning a failure.
    /// </summary>
    public Exception? Throws { get; set; }

    /// <summary>Gets the cancellation token the last call received.</summary>
    public CancellationToken LastToken { get; private set; }

    /// <inheritdoc/>
    public ValueTask<D2Result> DispatchAsync(
        OutboxMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        r_received.Add(message);
        LastToken = ct;

        if (Throws is { } ex)
            throw ex;

        return ValueTask.FromResult(Answer(message));
    }
}
