// -----------------------------------------------------------------------
// <copyright file="ForgeryProtectionTelemetry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Forgery;

using System.Diagnostics.Metrics;

/// <summary>
/// The metric surface of the forgery check. Hosts add the meter via
/// <c>.WithMetrics(m =&gt; m.AddMeter(ForgeryProtectionTelemetry.METER_NAME))</c>.
/// </summary>
public static class ForgeryProtectionTelemetry
{
    /// <summary>The OpenTelemetry meter name.</summary>
    public const string METER_NAME = "DcsvIo.D2.AspNetCore.Forgery";

    /// <summary>
    /// The tag key for the failure class of a refusal. Values come from
    /// <see cref="D2ForgeryRefusalReasons"/>.
    /// </summary>
    public const string TAG_OUTCOME = "outcome";

    /// <summary>The shared meter for the forgery check.</summary>
    public static readonly Meter SR_Meter = new(METER_NAME);

    /// <summary>
    /// Counter - cookie-borne requests the forgery check refused. Tagged with
    /// <see cref="TAG_OUTCOME"/>. No request identifier, no header value and no
    /// token reaches a tag.
    /// </summary>
    public static readonly Counter<long> SR_Refusals =
        SR_Meter.CreateCounter<long>(
            name: "d2.aspnetcore.forgery.refusals",
            unit: "{refusal}",
            description: "Cookie-borne requests refused by the forgery check.");

    /// <summary>
    /// Counter - cookie-borne mutations admitted with the token half skipped
    /// because no session had resolved when the check ran. On a healthy host
    /// this counts sign-in and other session-less mutations. A host whose
    /// pipeline puts this stage ahead of session resolution reads NoSession for
    /// every request, so this counter climbs with the whole mutation traffic and
    /// indicates the misconfiguration; nothing else distinguishes that deployment
    /// from a healthy one. Has no tag - no route, no header value, no token.
    /// </summary>
    public static readonly Counter<long> SR_TokenHalfSkipped =
        SR_Meter.CreateCounter<long>(
            name: "d2.aspnetcore.forgery.token_half_skipped",
            unit: "{request}",
            description:
                "Cookie-borne mutations admitted with the token half skipped for "
                + "want of a resolved session.");
}
