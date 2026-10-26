// -----------------------------------------------------------------------
// <copyright file="TransientPublishClassifier.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Publishing;

using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Resilience.Retry;
using global::RabbitMQ.Client.Exceptions;

/// <summary>
/// Decides whether an exception thrown during a publish attempt is
/// transient (worth retrying) or terminal (a caller-side bug, which
/// surfaces immediately).
/// </summary>
internal static class TransientPublishClassifier
{
    /// <summary>True when <paramref name="ex"/> is worth retrying.</summary>
    /// <param name="ex">The exception.</param>
    public static bool IsTransient(Exception ex) => ex switch
    {
        // The connection was not open at acquire time. It most likely reopens
        // within a retry cycle; if it does not, the publish returns
        // ServiceUnavailable after the attempts run out.
        BrokerUnavailableException => true,

        // Channel-level: the channel went down mid-call. The pool discards
        // it, and the next attempt creates a fresh one.
        AlreadyClosedException => true,

        // RabbitMQ.Client wraps various AMQP / network failures here.
        OperationInterruptedException => true,

        // The broker was not reachable at all.
        BrokerUnreachableException => true,

        // Pool acquire timed out - usually because every channel is stuck
        // waiting on a confirm during broker degradation. Worth retrying.
        TimeoutException => true,

        // Broker NACK, confirm failure, or a basic.return for a mandatory
        // publish that matched no queue: all of them retry. A return is
        // transient in the case that loses messages here. When two hosts bind
        // at once, each gets bind-ok before the exchange index is written, and
        // that window closes on its own. A binding that is genuinely absent
        // still fails once the attempts run out.
        PublishException => true,

        // Defer everything else to the standard classifier (HTTP / sockets / etc.).
        _ => RetryHelper.IsTransientException(ex),
    };
}
