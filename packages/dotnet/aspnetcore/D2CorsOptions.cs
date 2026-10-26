// -----------------------------------------------------------------------
// <copyright file="D2CorsOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore;

using DcsvIo.D2.Headers.Http;

/// <summary>
/// Configuration for
/// <see cref="CorsServiceCollectionExtensions.AddD2Cors"/>. Init-only
/// properties; bind via the <c>configure</c> callback. Validated at host
/// build via <c>ValidateOnStart()</c> - fail-fast on
/// <see cref="Origins"/>=<c>[]</c> so an empty allowed-origins list never
/// silently degrades to "no policy applied" at runtime.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Origins"/> defaults to <c>[]</c> - empty list is intentionally
/// fail-closed (no service should auto-allow cross-origin requests without
/// an explicit decision). Services that need CORS configure the origins
/// list explicitly via the
/// <see cref="D2AspNetCoreConstants.CORS_ORIGINS_CONFIG_KEY"/> indexed
/// env-var convention; <c>AddD2Cors</c> is not called where NO CORS is
/// needed.
/// </para>
/// <para>
/// <see cref="AllowedHeaders"/> defaults enumerate the canonical D2
/// cross-cutting headers (<c>X-Correlation-Id</c> - spec-driven constant
/// from <c>HttpHeaders.CORRELATION_ID</c>; <c>Idempotency-Key</c> -
/// Stripe-style spec-driven constant from <c>HttpHeaders.IDEMPOTENCY_KEY</c>)
/// + the standard request header a SPA needs (<c>Content-Type</c>)
/// + IP-forwarding headers used by request enrichment
/// (<c>X-Forwarded-For</c>, <c>X-Real-IP</c>, <c>CF-Connecting-IP</c>). New
/// canonical headers added to any cross-cutting middleware MUST be added
/// here in the same change per the doc-parity discipline.
/// </para>
/// <para>
/// <c>Authorization</c> is not in the defaults, and
/// <see cref="CorsServiceCollectionExtensions.AddD2Cors"/> rejects at host
/// build a policy that allows it together with
/// <see cref="AllowCredentials"/>. A policy that allows both lets a page on
/// an allowlisted origin attach an arbitrary <c>Authorization</c> value to a
/// credentialed cross-origin mutation, which is the state the forgery check
/// in <c>UseD2ForgeryProtection</c> reads as exempt. To serve bearer-only
/// cross-origin callers, add the header and set
/// <see cref="AllowCredentials"/> to <c>false</c>.
/// </para>
/// </remarks>
public sealed record D2CorsOptions
{
    /// <summary>
    /// Default allowed-headers list - covers the canonical D2 cross-cutting
    /// headers + standard SPA request headers + IP-forwarding headers, and
    /// not <c>Authorization</c>.
    /// </summary>
    internal static readonly IReadOnlyList<string> SR_DefaultAllowedHeaders =
    [
        "Content-Type",
        HttpHeaders.CORRELATION_ID,
        HttpHeaders.IDEMPOTENCY_KEY,
        "X-Forwarded-For",
        "X-Real-IP",
        "CF-Connecting-IP",
    ];

    /// <summary>
    /// Default allowed-methods list - the HTTP methods a JSON API and a SPA
    /// need to read, to mutate and to preflight.
    /// </summary>
    internal static readonly IReadOnlyList<string> SR_DefaultAllowedMethods =
    [
        "GET",
        "HEAD",
        "POST",
        "PUT",
        "PATCH",
        "DELETE",
        "OPTIONS",
    ];

    /// <summary>
    /// Gets or sets the allowed origins - bound from
    /// <see cref="D2AspNetCoreConstants.CORS_ORIGINS_CONFIG_KEY"/> via the
    /// indexed env-var convention. Default <c>[]</c> = fail-closed (no
    /// origins allowed). Validated non-empty + per-entry non-empty /
    /// non-whitespace at host build. Settable so the
    /// <c>AddD2Cors(IConfiguration, Action&lt;D2CorsOptions&gt;)</c>
    /// configure pipeline can populate the env-derived default after the
    /// options instance is constructed by the DI container; the configure
    /// callback receives the live instance and mutates it.
    /// </summary>
    public IReadOnlyList<string> Origins { get; set; } = [];

    /// <summary>
    /// Gets or sets the allowed request headers - defaults to the canonical
    /// D2 cross-cutting headers + standard SPA request headers +
    /// IP-forwarding headers. Validated per-entry non-empty / non-whitespace
    /// at host build, and refused at host build when it contains
    /// <c>Authorization</c> while <see cref="AllowCredentials"/> is
    /// <c>true</c>.
    /// </summary>
    public IReadOnlyList<string> AllowedHeaders { get; set; } =
        SR_DefaultAllowedHeaders;

    /// <summary>
    /// Gets or sets the allowed HTTP methods - defaults to the standard set
    /// above.
    /// Validated per-entry non-empty / non-whitespace at host build.
    /// </summary>
    public IReadOnlyList<string> AllowedMethods { get; set; } =
        SR_DefaultAllowedMethods;

    /// <summary>
    /// Gets or sets a value indicating whether the policy supports
    /// cookie / Authorization-header credentials (CORS spec
    /// <c>Access-Control-Allow-Credentials: true</c>).
    /// Default <c>true</c>, because a browser client authenticated by a
    /// cookie needs it. Note: <c>true</c> is incompatible with
    /// <see cref="Origins"/> containing the wildcard <c>"*"</c> per the CORS
    /// spec, and with <c>Authorization</c> in
    /// <see cref="AllowedHeaders"/>; the validator catches both.
    /// </summary>
    public bool AllowCredentials { get; set; } = true;

    /// <summary>
    /// Gets or sets the browser preflight cache TTL in seconds. Default <c>600</c> (10 min)
    /// - balance of cache-hit rate vs. policy-staleness window.
    /// </summary>
    public int PreflightMaxAgeSeconds { get; set; } = 600;
}
