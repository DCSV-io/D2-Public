// -----------------------------------------------------------------------
// <copyright file="ForgeryProtectionMiddleware.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Forgery;

using System.Text.Json;
using DcsvIo.D2.AspNetCore.Internal;
using DcsvIo.D2.ProblemDetails;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Internal middleware behind
/// <see cref="ForgeryProtectionApplicationBuilderExtensions.UseD2ForgeryProtection"/>.
/// </summary>
/// <remarks>
/// <para>
/// Classifies each request, then runs the checks that class calls for. No
/// check runs on a safe method, on a request presenting a non-empty
/// <c>Authorization</c> header, or on an endpoint declaring
/// <see cref="D2ForgeryPostures.EXEMPT"/>. The fetch-metadata check runs on
/// an endpoint declaring <see cref="D2ForgeryPostures.METADATA"/>, and on an
/// endpoint declaring <see cref="D2ForgeryPostures.REQUIRED"/> or declaring
/// nothing at all. A required or undeclared endpoint also runs the token
/// check once a session resolves; when none did, the fetch-metadata half still
/// decides and only the token half is skipped, because there is no session to
/// bind against.
/// </para>
/// <para>
/// An endpoint that declares nothing is treated as
/// <see cref="D2ForgeryPostures.REQUIRED"/>, so a route added without a
/// declaration is closed rather than open.
/// </para>
/// <para>
/// The <c>Authorization</c> exemption holds only while the host's CORS
/// policy never allows that header from a foreign origin. A browser cannot
/// attach it cross-site on its own; a policy that permits it can.
/// <c>AddD2Cors</c> on <see cref="CorsServiceCollectionExtensions"/> keeps
/// that precondition true for a host built through it: the header is not in
/// <see cref="D2CorsOptions.SR_DefaultAllowedHeaders"/>, and a policy that
/// allows it together with <see cref="D2CorsOptions.AllowCredentials"/> is
/// rejected at host build. A host that builds its CORS policy some other way
/// must hold that precondition itself.
/// </para>
/// </remarks>
internal sealed class ForgeryProtectionMiddleware
{
    /// <summary>The route field of a log line for a request that matched no endpoint.</summary>
    private const string _UNROUTED = "(unrouted)";

    private static readonly JsonSerializerOptions sr_jsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate r_next;
    private readonly D2ForgeryProtectionOptions r_options;
    private readonly ILogger<ForgeryProtectionMiddleware> r_logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForgeryProtectionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="options">The configured forgery-protection options snapshot.</param>
    /// <param name="logger">The host logger.</param>
    public ForgeryProtectionMiddleware(
        RequestDelegate next,
        IOptions<D2ForgeryProtectionOptions> options,
        ILogger<ForgeryProtectionMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        r_next = next;
        r_options = options.Value;
        r_logger = logger;
    }

    /// <summary>Pipeline entry point.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the async pipeline continuation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>AddD2ForgeryProtection</c> did not run, so the
    /// request-scoped <see cref="D2ForgeryTokenState"/> is not registered.
    /// </exception>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var state = context.RequestServices.GetService<D2ForgeryTokenState>();

        if (state is null)
        {
            throw new InvalidOperationException(
                "UseD2ForgeryProtection requires AddD2ForgeryProtection on the service "
                + "collection: D2ForgeryTokenState resolved to null. Passing the request "
                + "through instead would leave the check open by default.");
        }

        if (state.Checked)
        {
            await r_next(context);

            return;
        }

        state.Checked = true;

        var posture = context.GetEndpoint()?.Metadata
            .GetMetadata<ID2ForgeryPostureDeclaration>()?.Posture;

        var isSafeMethod = IsSafeMethod(context.Request.Method);
        var bearerPresented = context.Request.Headers.Authorization
            .ToString().ToNullIfEmpty() is not null;

        var postureIsRequired = posture is not D2ForgeryPostures.EXEMPT
            and not D2ForgeryPostures.METADATA;

        // A required or undeclared endpoint reads the token from the request
        // header. A token read from the cookie would be satisfied by any
        // cross-site request, because the browser attaches the cookie itself.
        var readsTokenHeader = !isSafeMethod && postureIsRequired;
        var tokenClass = readsTokenHeader && !bearerPresented;
        var metadataClass = !isSafeMethod
            && !bearerPresented
            && posture is D2ForgeryPostures.METADATA;

        var verifier = context.RequestServices.GetService<ID2ForgeryTokenVerifier>();

        if (verifier is not null)
        {
            // One keyed operation per request. Every class records a verdict,
            // because the host's issuer reads it on the response path to decide
            // whether this response needs a fresh token.
            var presented = readsTokenHeader
                ? context.Request.Headers[r_options.TokenHeaderName].ToString().ToNullIfEmpty()
                : context.Request.Cookies[r_options.TokenCookieName].ToNullIfEmpty();

            state.Verdict = verifier.Verify(context, presented);
        }
        else if (tokenClass)
        {
            // The verifier reports whether a session resolved. A host that
            // registers none cannot tell a request with a session from one
            // without a session, so both are refused.
            await RefuseAsync(context, state, verifier, D2ForgeryRefusalReasons.VERIFIER_ABSENT);

            return;
        }

        if (!metadataClass && !tokenClass)
        {
            await r_next(context);

            return;
        }

        // The fetch-metadata half needs no session, so it runs before the
        // no-session arm below and a session-less mutation on a required or
        // undeclared route is refused on the same header states a metadata
        // route is.
        var metadataReason = EvaluateFetchMetadata(context);

        if (metadataReason is not null)
        {
            await RefuseAsync(context, state, verifier, metadataReason);

            return;
        }

        if (tokenClass)
        {
            if (state.Verdict is D2ForgeryTokenVerdict.NoSession)
            {
                // No session resolved, so no token can be bound to one. The
                // endpoint's own auth gate decides what such a request gets.
                // The pass-through is recorded: a host whose pipeline places
                // this stage ahead of session resolution reads NoSession for
                // every request, and nothing else in metrics or logs tells that
                // deployment apart from a healthy one.
                ForgeryProtectionTelemetry.SR_TokenHalfSkipped.Add(1);
                ForgeryProtectionLog.TokenHalfSkippedNoSession(r_logger, RouteOf(context));

                await r_next(context);

                return;
            }

            var tokenReason = TokenRefusalReason(state.Verdict);

            if (tokenReason is not null)
            {
                await RefuseAsync(context, state, verifier, tokenReason);

                return;
            }
        }

        await r_next(context);
    }

    /// <summary>GET, HEAD and OPTIONS requests mutate nothing on this surface.</summary>
    private static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method)
        || HttpMethods.IsHead(method)
        || HttpMethods.IsOptions(method);

    /// <summary>
    /// The fetch-metadata half. Returns the refusal reason, or null when the
    /// headers show the request came from this origin.
    /// </summary>
    private static string? EvaluateFetchMetadata(HttpContext context)
    {
        var request = context.Request;

        if (request.Headers.TryGetValue(D2ForgeryHeaderNames.SEC_FETCH_SITE, out var siteValues)
            && siteValues.Count > 0)
        {
            return siteValues.ToString() switch
            {
                D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN => null,
                D2ForgeryHeaderNames.SEC_FETCH_SITE_NONE => null,
                D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_SITE =>
                    D2ForgeryRefusalReasons.METADATA_CROSS_SITE,
                D2ForgeryHeaderNames.SEC_FETCH_SITE_CROSS_SITE =>
                    D2ForgeryRefusalReasons.METADATA_CROSS_SITE,
                _ => D2ForgeryRefusalReasons.METADATA_UNKNOWN_VALUE,
            };
        }

        if (!request.Headers.TryGetValue(D2ForgeryHeaderNames.ORIGIN, out var originValues)
            || originValues.Count == 0)
        {
            return D2ForgeryRefusalReasons.HEADERS_ABSENT;
        }

        // Compared against the request's own scheme and host, so a deployment
        // that terminates TLS ahead of this host without forwarding the scheme
        // refuses a legacy-browser request rather than admitting one.
        return OriginMatchesRequest(originValues.ToString(), request)
            ? null
            : D2ForgeryRefusalReasons.ORIGIN_MISMATCH;
    }

    /// <summary>
    /// Whether the presented origin has the same scheme, host and port as
    /// the request. The scheme and host compare case-insensitively and the
    /// scheme's default port normalizes away on both sides; anything with
    /// a path, query or fragment is not an origin and does not match.
    /// </summary>
    private static bool OriginMatchesRequest(string origin, HttpRequest request)
    {
        var schemeEnd = origin.IndexOf("://", StringComparison.Ordinal);

        if (schemeEnd <= 0)
            return false;

        var scheme = origin.AsSpan(0, schemeEnd);
        var authority = origin.AsSpan(schemeEnd + 3);

        if (authority.IsEmpty || authority.IndexOfAny('/', '?', '#') >= 0)
            return false;

        if (!scheme.Equals(request.Scheme, StringComparison.OrdinalIgnoreCase))
            return false;

        var ownAuthority = (request.Host.Value ?? string.Empty).AsSpan();

        SplitAuthority(authority, out var host, out var port);
        SplitAuthority(ownAuthority, out var ownHost, out var ownPort);

        return host.Equals(ownHost, StringComparison.OrdinalIgnoreCase)
            && NormalizePort(port, scheme).Equals(
                NormalizePort(ownPort, scheme),
                StringComparison.Ordinal);
    }

    /// <summary>Splits an authority into its host and its port, if any.</summary>
    private static void SplitAuthority(
        ReadOnlySpan<char> authority,
        out ReadOnlySpan<char> host,
        out ReadOnlySpan<char> port)
    {
        host = authority;
        port = default;

        var colon = authority.LastIndexOf(':');

        if (colon < 0 || colon < authority.LastIndexOf(']'))
            return;

        var candidate = authority[(colon + 1)..];

        if (candidate.IsEmpty)
            return;

        foreach (var c in candidate)
        {
            if (c is < '0' or > '9')
                return;
        }

        host = authority[..colon];
        port = candidate;
    }

    /// <summary>The port, with the scheme's default rendered as no port.</summary>
    private static ReadOnlySpan<char> NormalizePort(
        ReadOnlySpan<char> port,
        ReadOnlySpan<char> scheme)
    {
        if (port.IsEmpty)
            return default;

        if (scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && port.Equals("443", StringComparison.Ordinal))
        {
            return default;
        }

        if (scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && port.Equals("80", StringComparison.Ordinal))
        {
            return default;
        }

        return port;
    }

    /// <summary>
    /// The token half. Returns the refusal reason, or null when the token is
    /// bound to the session this request resolved.
    /// </summary>
    private static string? TokenRefusalReason(D2ForgeryTokenVerdict verdict) =>
        verdict switch
        {
            D2ForgeryTokenVerdict.Bound => null,
            D2ForgeryTokenVerdict.Absent => D2ForgeryRefusalReasons.TOKEN_ABSENT,
            D2ForgeryTokenVerdict.Stale => D2ForgeryRefusalReasons.TOKEN_STALE,
            D2ForgeryTokenVerdict.Forged => D2ForgeryRefusalReasons.TOKEN_FORGED,

            // NoSession never reaches here: a request with that verdict passed
            // at the no-session arm above. Every other verdict is refused,
            // Unverifiable included: a key the verifier could not reach leaves
            // the token unproven, and admitting an unproven token would open
            // the check on the host's own outage. The caller gets the one
            // refusal code whatever the reason; the outage is distinguishable
            // in the log line and the metric tag an operator reads.
            _ => D2ForgeryRefusalReasons.TOKEN_UNVERIFIABLE,
        };

    /// <summary>The route pattern for the log line, never the request path.</summary>
    private static string RouteOf(HttpContext context) =>
        context.GetEndpoint() is RouteEndpoint routeEndpoint
            ? routeEndpoint.RoutePattern.RawText ?? _UNROUTED
            : _UNROUTED;

    /// <summary>
    /// Writes the 403 refusal with the forgery code, re-issues the token
    /// cookie when a session resolved, and does not call the next middleware.
    /// </summary>
    private async Task RefuseAsync(
        HttpContext context,
        D2ForgeryTokenState state,
        ID2ForgeryTokenVerifier? verifier,
        string reason)
    {
        var route = RouteOf(context);

        ForgeryProtectionTelemetry.SR_Refusals.Add(
            1,
            new KeyValuePair<string, object?>(ForgeryProtectionTelemetry.TAG_OUTCOME, reason));
        ForgeryProtectionLog.Refused(r_logger, route, reason);

        if (verifier is not null && state.Verdict is not D2ForgeryTokenVerdict.NoSession)
        {
            var issue = verifier.IssueForResolvedSession(context);

            if (issue is null)
            {
                ForgeryProtectionLog.TokenNotIssuedOnRefusal(r_logger, route);
            }
            else
            {
                context.Response.Cookies.Append(
                    r_options.TokenCookieName,
                    issue.Value,
                    D2ForgeryTokenCookie.Options(issue.ExpiresAt));
            }
        }

        context.SetD2Result(D2Result.ForgeryRefused());
        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
        };

        var problemContext = new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
        };

        var problemDetailsService = context.RequestServices
            .GetService<IProblemDetailsService>();

        if (problemDetailsService is not null
            && await problemDetailsService.TryWriteAsync(problemContext))
        {
            return;
        }

        // No problem-details pipeline wrote the body, so write the same shape
        // here rather than respond with a 403 and no body.
        var problemOptions = context.RequestServices
            .GetService<IOptions<D2ProblemDetailsOptions>>()?.Value
            ?? new D2ProblemDetailsOptions();

        D2ProblemDetailsCustomizer.Apply(problemContext, problemOptions);

        context.Response.ContentType = D2ProblemDetailsKeys.CONTENT_TYPE;

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            problem,
            sr_jsonOptions,
            context.RequestAborted);
    }
}
