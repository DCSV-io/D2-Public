// -----------------------------------------------------------------------
// <copyright file="MessagingTelemetry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Telemetry;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Static OTel <see cref="ActivitySource"/> + <see cref="Meter"/> used
/// across the messaging-rabbitmq lib. They are static, not per-handler
/// instances, per OTel guidance, because generic-static-field issues and
/// listener attachment cost dominate with per-handler instances.
/// </summary>
/// <remarks>
/// The class is <c>public</c> so the source / meter name constant
/// (<see cref="SOURCE_NAME"/>) is reachable from other assemblies, which
/// register the source and meter so messaging publish / consume spans +
/// metrics reach the OTLP / Prometheus exporters without per-host opt-in. The
/// activity source / meter / counter / histogram fields are <c>public</c>
/// statics too, and the lib's own publish + consume hot-path code is the only
/// code that emits to them.
/// </remarks>
public static class MessagingTelemetry
{
    /// <summary>OTel source / meter name for the messaging-rabbitmq lib.</summary>
    public const string SOURCE_NAME = "DcsvIo.D2.Messaging.RabbitMq";

    /// <summary>Activity source for publish + consume spans.</summary>
    public static readonly ActivitySource SR_ActivitySource = new(SOURCE_NAME);

    /// <summary>Meter that owns the publish counters / histogram.</summary>
    public static readonly Meter SR_Meter = new(SOURCE_NAME);

    /// <summary>Total publish attempts (including retries).</summary>
    public static readonly Counter<long> SR_PublishesCounter = SR_Meter.CreateCounter<long>(
        "d2.messaging.rabbitmq.publishes",
        unit: "{publish}",
        description: "Total publish attempts (including retries).");

    /// <summary>Publishes that fail after the retry loop, retried or not.</summary>
    public static readonly Counter<long> SR_PublishFailuresCounter = SR_Meter.CreateCounter<long>(
        "d2.messaging.rabbitmq.publish_failures",
        unit: "{publish}",
        description: "Publishes that fail after the retry loop, retried or not.");

    /// <summary>Wall-clock duration of a publish operation, end-to-end.</summary>
    public static readonly Histogram<double> SR_PublishDurationHistogram =
        SR_Meter.CreateHistogram<double>(
            "d2.messaging.rabbitmq.publish_duration",
            unit: "ms",
            description:
                "Wall-clock duration of a publish operation, end-to-end (including retries).");

    /// <summary>Publish retry attempts (transient failure -> backoff -> re-attempt).</summary>
    public static readonly Counter<long> SR_PublishRetriesCounter = SR_Meter.CreateCounter<long>(
        "d2.messaging.rabbitmq.publish_retries",
        unit: "{retry}",
        description: "Publish retry attempts (transient failure -> backoff -> re-attempt).");

    /// <summary>Publish attempts the broker returned as unroutable - the
    /// message matched no queue. Counted per returned attempt, so an attempt
    /// that was returned and a later attempt that succeeded both show.</summary>
    public static readonly Counter<long> SR_PublishReturnsCounter =
        SR_Meter.CreateCounter<long>(
            "d2.messaging.rabbitmq.publish_returns",
            unit: "{publish}",
            description:
                "Publish attempts the broker returned because they matched no queue.");

    /// <summary>DLQ republish failures - consumer fell back from
    /// republish-with-failure-header to plain BasicNack, so the DLQ
    /// message lacks the <c>x-d2-failure-reason</c> diagnostic header.</summary>
    public static readonly Counter<long> SR_DlqRepublishFailuresCounter =
        SR_Meter.CreateCounter<long>(
            "d2.messaging.rabbitmq.dlq_republish_failures",
            unit: "{republish}",
            description:
                "Consumer-side failures to republish to the dead-letter exchange: "
                + "the failure header was lost and the consumer fell back to a nack "
                + "without requeue.");

    /// <summary>Failures after a successful handler run: the idempotency mark
    /// write failed (the message is nacked without requeue to the DLQ) or
    /// BasicAck failed (a narrowed catch around the ack call). Spikes here
    /// indicate broker / channel instability between handler completion and
    /// ack, or a degraded idempotency store; after a failed ack the broker
    /// redelivers on reconnect, and the idempotency mark prevents duplicate
    /// work.</summary>
    public static readonly Counter<long> SR_AckFailuresCounter =
        SR_Meter.CreateCounter<long>(
            "d2.messaging.rabbitmq.ack_failures",
            unit: "{ack}",
            description:
                "Consumer-side failures after a successful handler run: the "
                + "idempotency mark could not be written (the message is nacked to "
                + "the dead-letter queue) or the ack could not be sent (the broker "
                + "redelivers the message).");
}
