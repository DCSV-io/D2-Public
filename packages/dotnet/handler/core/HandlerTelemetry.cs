// -----------------------------------------------------------------------
// <copyright file="HandlerTelemetry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Static OpenTelemetry primitives shared by every <c>BaseHandler</c>
/// invocation. One <see cref="System.Diagnostics.ActivitySource"/> for spans;
/// one <see cref="Meter"/> with four instruments (invoked / succeeded / failed
/// counters and a duration histogram). Static per OTel official guidance,
/// which avoids the generic-static-field issues that arise when each closed
/// <c>BaseHandler&lt;TSelf, ...&gt;</c> holds its own copy.
/// </summary>
/// <remarks>
/// Metric and span emission is observable through
/// <c>System.Diagnostics.MeterListener</c> +
/// <c>System.Diagnostics.ActivityListener</c>, so the telemetry surface needs
/// no mocking.
/// </remarks>
public static class HandlerTelemetry
{
    /// <summary>OTel source name - matches the assembly's well-known identifier.</summary>
    public const string SourceName = "DcsvIo.D2.Handler";

    /// <summary>
    /// Static <see cref="System.Diagnostics.ActivitySource"/> for handler spans.
    /// </summary>
    public static readonly ActivitySource SR_ActivitySource = new(SourceName);

    /// <summary>Static <see cref="Meter"/> for handler metrics.</summary>
    public static readonly Meter SR_Meter = new(SourceName);

    /// <summary>Counter - incremented at every <c>HandleAsync</c> entry.</summary>
    public static readonly Counter<long> SR_Invoked =
        SR_Meter.CreateCounter<long>(
            "d2.handler.invoked",
            unit: "{calls}",
            description: "Handler invocations attempted.");

    /// <summary>
    /// Counter - incremented when <c>HandleAsync</c> returns a successful
    /// result.
    /// </summary>
    public static readonly Counter<long> SR_Succeeded =
        SR_Meter.CreateCounter<long>(
            "d2.handler.succeeded",
            unit: "{calls}",
            description: "Handler invocations that returned a successful D2Result.");

    /// <summary>
    /// Counter - incremented when <c>HandleAsync</c> returns a failure result
    /// OR throws.
    /// </summary>
    public static readonly Counter<long> SR_Failed =
        SR_Meter.CreateCounter<long>(
            "d2.handler.failed",
            unit: "{calls}",
            description: "Handler invocations that returned a failed D2Result or threw.");

    /// <summary>
    /// Histogram - records each handler's wall-clock duration in milliseconds.
    /// </summary>
    public static readonly Histogram<double> SR_Duration =
        SR_Meter.CreateHistogram<double>(
            "d2.handler.duration",
            unit: "ms",
            description: "Handler invocation wall-clock duration in milliseconds.");

    /// <summary>
    /// The span-tag, metric-tag and log-scope keys the handler pipeline writes.
    /// BaseHandler writes most of these twice - once as a span tag and once as a
    /// log-scope field - and reads the key from here at both sites.
    /// </summary>
    internal static class Tags
    {
        /// <summary>Span tag, metric tag and log-scope key for the handler type name.</summary>
        public const string HANDLER_NAME = "d2.handler.name";

        /// <summary>Log-scope key for the request's trace id.</summary>
        public const string TRACE_ID = "d2.trace_id";

        /// <summary>Span tag and log-scope key for the acting user's id.</summary>
        public const string USER_ID = "d2.user_id";

        /// <summary>Span tag and log-scope key for the operating organization's id.</summary>
        public const string ORG_ID = "d2.org_id";

        /// <summary>Span tag and log-scope key for the operating organization's type.</summary>
        public const string ORG_TYPE = "d2.org_type";

        /// <summary>Span tag and log-scope key for the caller's role in that org.</summary>
        public const string ORG_ROLE = "d2.org_role";

        /// <summary>Span tag marking a call made under impersonation.</summary>
        public const string IMPERSONATING = "d2.impersonating";

        /// <summary>Span tag and log-scope key for the impersonation kind.</summary>
        public const string IMPERSONATION_KIND = "d2.impersonation_kind";

        /// <summary>Span tag for the impersonating agent's own user id.</summary>
        public const string IMPERSONATOR_ID = "d2.impersonator_id";

        /// <summary>Span tag for the impersonating agent's own organization id.</summary>
        public const string IMPERSONATOR_ORG_ID = "d2.impersonator_org_id";

        /// <summary>Span tag for the impersonating agent's own organization type.</summary>
        public const string IMPERSONATOR_ORG_TYPE = "d2.impersonator_org_type";

        /// <summary>Span tag for the impersonating agent's own organization role.</summary>
        public const string IMPERSONATOR_ORG_ROLE = "d2.impersonator_org_role";
    }
}
