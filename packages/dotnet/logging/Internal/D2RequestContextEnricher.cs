// -----------------------------------------------------------------------
// <copyright file="D2RequestContextEnricher.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Logging.Internal;

using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

/// <summary>
/// Enriches the Serilog request-completion log line with the
/// PII-reviewed LOG-OK projection of <see cref="IRequestContext"/> when the
/// per-request DI scope has one registered.
/// </summary>
/// <remarks>
/// <para>
/// Emits the following fields when populated, organized into clusters:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>Tracing</b>: <c>TraceId</c>, <c>RequestId</c>,
///     <c>RequestPath</c>.
///   </description></item>
///   <item><description>
///     <b>Auth/Identity</b>: <c>IsAuthenticated</c>, <c>Subject</c>,
///     <c>UserId</c>, <c>Username</c>, <c>RequestedByClientId</c>,
///     <c>ImmediateCallerClientId</c>, <c>OriginatingClientId</c>,
///     <c>IsServiceIdentity</c>.
///   </description></item>
///   <item><description>
///     <b>Auth/Token+Trust</b>: <c>Audience</c>, <c>SessionIdHash</c>,
///     <c>TokenIssuedAt</c>, <c>TokenExpiresAt</c>, <c>ActorChain</c>
///     (destructured).
///   </description></item>
///   <item><description>
///     <b>Auth/Org</b>: <c>OrgId</c>, <c>OrgName</c>, <c>OrgType</c>,
///     <c>OrgRole</c>.
///   </description></item>
///   <item><description>
///     <b>Auth/Impersonation</b>: <c>IsImpersonating</c>,
///     <c>ImpersonationKind</c>, <c>ImpersonatedBy</c>,
///     <c>ImpersonationSessionIdHash</c>, <c>ImpersonatorOrgId</c>,
///     <c>ImpersonatorOrgName</c>, <c>ImpersonatorOrgType</c>,
///     <c>ImpersonatorOrgRole</c>.
///   </description></item>
///   <item><description>
///     <b>Scopes</b>: <c>Scopes</c> (destructured).
///   </description></item>
///   <item><description>
///     <b>Trust/Risk</b>: <c>RiskScore</c>.
///   </description></item>
///   <item><description>
///     <b>Fingerprints</b>: <c>SessionFingerprint</c>,
///     <c>CurrentFingerprint</c>.
///   </description></item>
///   <item><description>
///     <b>WhoIs/Geo</b>: <c>WhoIsHashId</c>,
///     <c>AdminLocationHashId</c>, <c>CountryIso31661Alpha2Code</c>.
///   </description></item>
///   <item><description>
///     <b>WhoIs/Network-Privacy</b>: <c>IsVpn</c>, <c>IsProxy</c>,
///     <c>IsTor</c>, <c>IsHosting</c>.
///   </description></item>
///   <item><description>
///     <b>WhoIs/ASN</b>: <c>Asn</c>, <c>AsnName</c>, <c>AsnType</c>.
///   </description></item>
/// </list>
/// <para>
/// See the "LOG-OK fields" section of docs/field-policy.md in this package for
/// the full per-field table including types and rationale.
/// </para>
/// <para>
/// PII fields are NEVER emitted: <c>ClientIp</c>, <c>City</c>,
/// <c>SubdivisionIso31662Code</c>, <c>PostalCode</c>, <c>Latitude</c>,
/// <c>Longitude</c>, <c>Geohash</c>. Sub-country geographic precision
/// is available only through <c>WhoIsHashId</c> lookups against the WhoIs store.
/// </para>
/// <para>
/// <b>Session identifiers are never emitted raw.</b> A session id does not authenticate
/// a request - the session cookie carries an encrypted frame rather than the id - but a
/// log line carrying it joins that session across every log and ties it to one person,
/// and identifies that person's session to anything able to end it. The
/// session-bearing scalars leave as non-reversible correlation digests under distinct
/// names - <c>SessionIdHash</c> and <c>ImpersonationSessionIdHash</c> (see
/// <see cref="LogSafeIdentifiers"/>). The actor chain is projected to
/// <see cref="LogSafeActorEntry"/> before it reaches the diagnostic context, with each
/// entry's session id replaced by the same correlation digest under the member name
/// <c>SessionIdHash</c>; this class passes no <c>ActorEntry</c> instance to a sink.
/// <c>[RedactData(SecretInformation)]</c> on <c>ActorEntry.SessionId</c> is a second
/// layer of protection for other capture sites, not the protection on this path - it
/// fires only on structural destructuring, and a sequence element bound as a scalar
/// renders the record's <c>ToString()</c>, which prints the raw id. This is the SECRET
/// policy, deliberately distinct from the PII policy above.
/// </para>
/// <para>
/// Each per-field <c>Set</c> call is gated by a null check on the field
/// value; null fields are not emitted at all (vs emitting a null structured
/// property). For the collection fields (<c>Audience</c>,
/// <c>ActorChain</c>, <c>Scopes</c>), an empty-collection gate via
/// <c>Truthy()</c> applies - empty collections are not emitted, which keeps
/// empty arrays out of the logs on every request.
/// </para>
/// <para>
/// Where nothing populates the request context, the enricher is a structural
/// no-op - every value is null on every request and the enricher adds no
/// property to the log line.
/// </para>
/// <para>
/// <b>Precedence note</b>: behavior of the Tracing-axis fields
/// differs by HOW the upstream value reaches the log event.
/// <see cref="IRequestContext.TraceId"/> reaches the diagnostic context via
/// the same <see cref="IDiagnosticContext"/> mechanism the request-logging
/// middleware uses, so this enricher (running LAST in the
/// <c>EnrichDiagnosticContext</c> callback) WINS via last-writer-wins on
/// the diag-ctx dictionary. <see cref="IRequestContext.RequestId"/> and
/// <see cref="IRequestContext.RequestPath"/> are pre-bound by Serilog's
/// own <c>ForContext</c> mechanism on the HTTP path BEFORE the callback
/// runs; once Serilog has populated those properties on the
/// <c>LogEvent</c>, subsequent <c>IDiagnosticContext.Set</c> calls are
/// silently dropped by Serilog's <c>AddPropertyIfAbsent</c> semantics.
/// The enricher emits each of them for spec-contract completeness - on
/// non-HTTP transports without Serilog's pre-binding, every emission
/// reaches the log. See the "Precedence note" section of
/// docs/field-policy.md in this package for the full operator-facing
/// description.
/// </para>
/// <para>
/// Degrades to a no-op when <see cref="IRequestContext"/> is not registered
/// in the per-request DI scope. Never throws.
/// </para>
/// </remarks>
internal static class D2RequestContextEnricher
{
    /// <summary>
    /// Resolves <see cref="IRequestContext"/> from the current
    /// <see cref="HttpContext.RequestServices"/> scope and emits the LOG-OK
    /// fields onto <paramref name="diagnosticContext"/>. No-ops when the
    /// context is unregistered or every field is null.
    /// </summary>
    /// <param name="diagnosticContext">
    /// Serilog request-logging diagnostic context - receives one
    /// <see cref="IDiagnosticContext.Set(string, object?, bool)"/> call per
    /// non-null LOG-OK field.
    /// </param>
    /// <param name="httpContext">
    /// Current request's <see cref="HttpContext"/>. Must be non-null; the
    /// null check is defensive only, because the
    /// <c>UseSerilogRequestLogging</c> middleware always supplies it.
    /// </param>
    internal static void Enrich(
        IDiagnosticContext diagnosticContext,
        HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(diagnosticContext);
        ArgumentNullException.ThrowIfNull(httpContext);

        var ctx = httpContext.RequestServices.GetService<IRequestContext>();
        if (ctx is null)
            return;

        // Tracing - TraceId OVERRIDES the request-logging middleware's
        // locally-set value when populated; RequestId and RequestPath are
        // pre-bound by Serilog on the HTTP path, so Serilog drops these
        // calls there. See the precedence note in the type-level remarks
        // above.
        if (ctx.TraceId is { } traceId)
            diagnosticContext.Set(nameof(IRequestContext.TraceId), traceId);

        if (ctx.RequestId is { } requestId)
            diagnosticContext.Set(nameof(IRequestContext.RequestId), requestId);

        if (ctx.RequestPath is { } requestPath)
            diagnosticContext.Set(nameof(IRequestContext.RequestPath), requestPath);

        // Auth/Identity.
        if (ctx.IsAuthenticated is { } isAuthenticated)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.IsAuthenticated),
                isAuthenticated);
        }

        if (ctx.Subject is { } subject)
            diagnosticContext.Set(nameof(IRequestContext.Subject), subject);

        if (ctx.UserId is { } userId)
            diagnosticContext.Set(nameof(IRequestContext.UserId), userId);

        if (ctx.Username is { } username)
            diagnosticContext.Set(nameof(IRequestContext.Username), username);

        if (ctx.RequestedByClientId is { } requestedByClientId)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.RequestedByClientId),
                requestedByClientId);
        }

        if (ctx.ImmediateCallerClientId is { } immediateCallerClientId)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.ImmediateCallerClientId),
                immediateCallerClientId);
        }

        if (ctx.OriginatingClientId is { } originatingClientId)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.OriginatingClientId),
                originatingClientId);
        }

        if (ctx.IsServiceIdentity is { } isServiceIdentity)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.IsServiceIdentity),
                isServiceIdentity);
        }

        // Auth/Token+Trust. Audience + ActorChain are non-null
        // collections per spec; gate on Truthy() to suppress empty arrays.
        if (ctx.Audience.Truthy())
        {
            diagnosticContext.Set(
                nameof(IRequestContext.Audience),
                ctx.Audience,
                destructureObjects: true);
        }

        // A session id is the handle every record about one session shares, not an opaque
        // audit number. Only its non-reversible correlation digest may reach a sink.
        if (ctx.SessionId is { } sessionId)
        {
            diagnosticContext.Set(
                LogSafeIdentifiers.SESSION_ID_HASH,
                LogSafeIdentifiers.SessionDigest(sessionId));
        }

        if (ctx.TokenIssuedAt is { } tokenIssuedAt)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.TokenIssuedAt),
                tokenIssuedAt);
        }

        if (ctx.TokenExpiresAt is { } tokenExpiresAt)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.TokenExpiresAt),
                tokenExpiresAt);
        }

        // Projected before it is passed on: each entry's SessionId is a session id of the
        // same class as the scalar above, and a record's ToString() would print it verbatim
        // if Serilog binds the sequence elements as scalars rather than structures.
        if (ctx.ActorChain.Truthy())
        {
            diagnosticContext.Set(
                nameof(IRequestContext.ActorChain),
                LogSafeIdentifiers.ToLogSafe(ctx.ActorChain),
                destructureObjects: true);
        }

        // Auth/Org.
        if (ctx.OrgId is { } orgId)
            diagnosticContext.Set(nameof(IRequestContext.OrgId), orgId);

        if (ctx.OrgName is { } orgName)
            diagnosticContext.Set(nameof(IRequestContext.OrgName), orgName);

        if (ctx.OrgType is { } orgType)
            diagnosticContext.Set(nameof(IRequestContext.OrgType), orgType);

        if (ctx.OrgRole is { } orgRole)
            diagnosticContext.Set(nameof(IRequestContext.OrgRole), orgRole);

        // Auth/Impersonation.
        if (ctx.IsImpersonating is { } isImpersonating)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.IsImpersonating),
                isImpersonating);
        }

        if (ctx.ImpersonationKind is { } impersonationKind)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.ImpersonationKind),
                impersonationKind);
        }

        if (ctx.ImpersonatedBy is { } impersonatedBy)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.ImpersonatedBy),
                impersonatedBy);
        }

        // Same class as SessionId above - digest only.
        if (ctx.ImpersonationSessionId is { } impersonationSessionId)
        {
            diagnosticContext.Set(
                LogSafeIdentifiers.IMPERSONATION_SESSION_ID_HASH,
                LogSafeIdentifiers.SessionDigest(impersonationSessionId));
        }

        if (ctx.ImpersonatorOrgId is { } impersonatorOrgId)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.ImpersonatorOrgId),
                impersonatorOrgId);
        }

        if (ctx.ImpersonatorOrgName is { } impersonatorOrgName)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.ImpersonatorOrgName),
                impersonatorOrgName);
        }

        if (ctx.ImpersonatorOrgType is { } impersonatorOrgType)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.ImpersonatorOrgType),
                impersonatorOrgType);
        }

        if (ctx.ImpersonatorOrgRole is { } impersonatorOrgRole)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.ImpersonatorOrgRole),
                impersonatorOrgRole);
        }

        // Scopes. Non-null set per spec; Truthy() gate suppresses
        // empty sets so we don't emit "Scopes":[] on every unauthenticated
        // request.
        if (ctx.Scopes.Truthy())
        {
            diagnosticContext.Set(
                nameof(IRequestContext.Scopes),
                ctx.Scopes,
                destructureObjects: true);
        }

        // Trust/Risk.
        if (ctx.RiskScore is { } riskScore)
            diagnosticContext.Set(nameof(IRequestContext.RiskScore), riskScore);

        // Fingerprints.
        if (ctx.SessionFingerprint is { } sessionFingerprint)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.SessionFingerprint),
                sessionFingerprint);
        }

        if (ctx.CurrentFingerprint is { } currentFingerprint)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.CurrentFingerprint),
                currentFingerprint);
        }

        // WhoIs/Geo.
        if (ctx.WhoIsHashId is { } whoIsHashId)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.WhoIsHashId),
                whoIsHashId);
        }

        if (ctx.AdminLocationHashId is { } adminLocationHashId)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.AdminLocationHashId),
                adminLocationHashId);
        }

        if (ctx.CountryIso31661Alpha2Code is { } countryIso31661Alpha2Code)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.CountryIso31661Alpha2Code),
                countryIso31661Alpha2Code);
        }

        // WhoIs/Network-Privacy.
        if (ctx.IsVpn is { } isVpn)
            diagnosticContext.Set(nameof(IRequestContext.IsVpn), isVpn);

        if (ctx.IsProxy is { } isProxy)
            diagnosticContext.Set(nameof(IRequestContext.IsProxy), isProxy);

        if (ctx.IsTor is { } isTor)
            diagnosticContext.Set(nameof(IRequestContext.IsTor), isTor);

        if (ctx.IsHosting is { } isHosting)
            diagnosticContext.Set(nameof(IRequestContext.IsHosting), isHosting);

        // WhoIs/ASN.
        if (ctx.Asn is { } asn)
            diagnosticContext.Set(nameof(IRequestContext.Asn), asn);

        if (ctx.AsnName is { } asnName)
            diagnosticContext.Set(nameof(IRequestContext.AsnName), asnName);

        if (ctx.AsnType is { } asnType)
            diagnosticContext.Set(nameof(IRequestContext.AsnType), asnType);
    }
}
