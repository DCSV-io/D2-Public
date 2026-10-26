// -----------------------------------------------------------------------
// <copyright file="ForgeryProtectionLog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Forgery;

using Microsoft.Extensions.Logging;

/// <summary>
/// Source-generated log delegates for the forgery check. EventIds 9570+,
/// distinct from the mTLS 9560+ range.
/// </summary>
/// <remarks>
/// No delegate accepts an <see cref="System.Exception"/>, a header value, a
/// cookie value or a token. The log line contains the route pattern and the
/// closed-set reason, which are enough to tell a misconfigured deployment
/// from an attack.
/// </remarks>
internal static partial class ForgeryProtectionLog
{
    /// <summary>
    /// Logs that the forgery check refused a request.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="route">The route pattern, or a sentinel when unrouted.</param>
    /// <param name="reason">A value from <see cref="D2ForgeryRefusalReasons"/>.</param>
    [LoggerMessage(
        EventId = 9570,
        Level = LogLevel.Warning,
        Message = "Forgery check refused a request (route {route}, reason {reason}).")]
    public static partial void Refused(ILogger logger, string route, string reason);

    /// <summary>
    /// Logs that a refused response could not carry a fresh token cookie
    /// because the sealing key could not be reached.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="route">The route pattern, or a sentinel when unrouted.</param>
    [LoggerMessage(
        EventId = 9571,
        Level = LogLevel.Warning,
        Message = "Forgery token could not be issued on a refusal (route {route}).")]
    public static partial void TokenNotIssuedOnRefusal(ILogger logger, string route);

    /// <summary>
    /// Logs that the token half was skipped because no session had resolved
    /// when the check ran.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="route">The route pattern, or a sentinel when unrouted.</param>
    [LoggerMessage(
        EventId = 9572,
        Level = LogLevel.Debug,
        Message = "Forgery token half skipped, no session resolved (route {route}).")]
    public static partial void TokenHalfSkippedNoSession(ILogger logger, string route);
}
