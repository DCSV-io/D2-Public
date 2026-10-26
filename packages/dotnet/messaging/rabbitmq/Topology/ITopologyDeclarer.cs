// -----------------------------------------------------------------------
// <copyright file="ITopologyDeclarer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Topology;

/// <summary>
/// Declares every exchange / queue / DLX binding required by the registered
/// subscribers. Several callers can invoke it once the connection is ready, so
/// an implementation must declare once and let every later caller join that
/// declaration rather than send a second set of declare and bind commands.
/// </summary>
/// <remarks>
/// A second concurrent <c>queue.bind</c> for a binding another caller is
/// already creating is not harmless, which is why declaring twice is not an
/// option here. RabbitMQ answers the second bind from its route table as soon
/// as the first bind's route record is committed, and the first bind's
/// exchange-index update runs after that commit, so <c>bind-ok</c> can arrive
/// while the exchange still routes nothing to the queue. A publish issued in
/// that window matches no queue. The publisher publishes with the mandatory
/// flag, so such a publish is returned by the broker and retried instead of
/// being confirmed and dropped. Declaring once still keeps a single host from
/// opening the window, but two separate processes that declare the same
/// binding at the same time can still open it.
/// </remarks>
internal interface ITopologyDeclarer
{
    /// <summary>Declares all topology required by the subscriber registry. The
    /// returned task completes only once the declaration has taken effect at
    /// the broker, so a caller may publish as soon as it has awaited this.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when declaration finishes.</returns>
    ValueTask DeclareAsync(CancellationToken ct);
}
