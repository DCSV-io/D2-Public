// -----------------------------------------------------------------------
// <copyright file="OutboxTelemetry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox.EntityFrameworkCore;

using System.Diagnostics.Metrics;

/// <summary>
/// The relay's OpenTelemetry instruments. Hosts add
/// <see cref="METER_NAME"/> via
/// <c>.WithMetrics(m =&gt; m.AddMeter(OutboxTelemetry.METER_NAME))</c>.
/// </summary>
public static class OutboxTelemetry
{
    /// <summary>The OpenTelemetry meter name for the outbox relay.</summary>
    public const string METER_NAME = "DcsvIo.D2.Outbox";

    /// <summary>The tag key for the message kind on every instrument here.</summary>
    public const string TAG_KIND = "kind";

    /// <summary>
    /// The tag key that identifies the failure class on each error counter.
    /// Without it, an abandonment alert shows that a message was abandoned but
    /// not why, and a rolling deploy that is missing a dispatcher looks the same
    /// in metrics as a broker outage, although the two need different operator
    /// responses.
    /// </summary>
    public const string TAG_OUTCOME = "outcome";

    /// <summary>The shared meter for the relay.</summary>
    public static readonly Meter SR_Meter = new(METER_NAME);

    /// <summary>
    /// Counter - messages a dispatcher confirmed. Tagged with
    /// <see cref="TAG_KIND"/>.
    /// </summary>
    public static readonly Counter<long> SR_PublishedTotal =
        SR_Meter.CreateCounter<long>(
            name: "d2.outbox.published",
            unit: "{message}",
            description: "Total staged messages a dispatcher confirmed. Tag: kind.");

    /// <summary>
    /// Counter - delivery attempts that failed and re-staged the message.
    /// Tagged with <see cref="TAG_KIND"/> and <see cref="TAG_OUTCOME"/>.
    /// </summary>
    public static readonly Counter<long> SR_DispatchFailuresTotal =
        SR_Meter.CreateCounter<long>(
            name: "d2.outbox.dispatch_failures",
            unit: "{failure}",
            description:
                "Total failed delivery attempts that re-staged a message. Tags: kind, outcome.");

    /// <summary>
    /// Counter - messages that ran out of attempts. <b>The abandonment alert is
    /// built on this counter</b>; the alerting rule over it is deployment
    /// configuration rather than something this package ships. Tagged with
    /// <see cref="TAG_KIND"/> and <see cref="TAG_OUTCOME"/>, because an alert
    /// that does not show why a message was abandoned cannot be acted on.
    /// </summary>
    public static readonly Counter<long> SR_AbandonedTotal =
        SR_Meter.CreateCounter<long>(
            name: "d2.outbox.abandoned",
            unit: "{message}",
            description:
                "Total messages abandoned after exhausting the retry limit. Tags: kind, outcome.");

    /// <summary>
    /// Counter - settled rows the retention sweep deleted. It has no tag: the
    /// sweep deletes by age and status rather than by kind, and an operator
    /// reads from it whether the table is being drained at all.
    /// </summary>
    public static readonly Counter<long> SR_PrunedTotal =
        SR_Meter.CreateCounter<long>(
            name: "d2.outbox.pruned",
            unit: "{row}",
            description: "Total published and abandoned rows deleted past their retention window.");

    /// <summary>
    /// Histogram - how long one dispatcher call took, in seconds. Tagged with
    /// <see cref="TAG_KIND"/>.
    /// </summary>
    /// <remarks>
    /// <b>The counters do not show why the published rate fell.</b> One tick
    /// claims up to <see cref="OutboxOptions.BatchSize"/> messages and awaits a
    /// dispatcher per message, each a broker publish or equivalent, so a
    /// dispatcher that slows from ten milliseconds to eight hundred turns a
    /// full batch from under a second into more than a minute. The tick is a
    /// fixed cadence rather than a queue, so the backlog grows silently until
    /// the abandonment alert fires an hour later; this histogram shows the
    /// slowdown while it is still only a slowdown. It is tagged with the kind
    /// alone: the failure class already appears on the error counters, and
    /// splitting the latency by outcome would leave every bucket too thin to
    /// read.
    /// </remarks>
    public static readonly Histogram<double> SR_DispatchDurationSeconds =
        SR_Meter.CreateHistogram<double>(
            name: "d2.outbox.dispatch_duration",
            unit: "s",
            description: "How long one dispatcher call took, in seconds. Tag: kind.");

    /// <summary>
    /// The closed value set <see cref="TAG_OUTCOME"/> takes. Every failing
    /// delivery has exactly one of these values, and each calls for a different
    /// operator action.
    /// </summary>
    public static class Outcome
    {
        /// <summary>
        /// No dispatcher is registered for the staged kind: a deployment is
        /// missing a component. Adding it delivers the waiting messages.
        /// </summary>
        public const string NO_DISPATCHER = "no_dispatcher";

        /// <summary>
        /// A dispatcher threw instead of returning a result. This is a transport
        /// fault that surfaces as an exception.
        /// </summary>
        public const string DISPATCHER_THREW = "dispatcher_threw";

        /// <summary>
        /// A dispatcher returned a failed result. The reason is the dispatcher's
        /// own error code, which is recorded on the message and not used as a
        /// tag, because that set is open and a metric dimension must be closed.
        /// </summary>
        public const string DISPATCHER_REFUSED = "dispatcher_refused";
    }
}
