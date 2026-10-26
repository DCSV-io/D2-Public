// -----------------------------------------------------------------------
// <copyright file="IForwardedJwtAccessor.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// Request-scoped holder for the inbound forwarded JWT, structurally isolated
/// from the request context and its log / OpenTelemetry enrichment projection.
/// It holds a bearer token after the token has passed validation, so the token
/// can be replayed byte-for-byte on a cross-process hop.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not a property of the request context. The enriched request
/// context is a broadly projected log/telemetry surface (its enricher emits
/// many of its fields on every request); the forwarded JWT must be
/// structurally excluded from that projection so the guarantee that it is never
/// logged cannot regress through an unrelated enricher change. The holder is a
/// distinct type with a distinct DI registration, reachable only by code that
/// explicitly injects it - never enumerable from a heterogeneous bag and never
/// reached by the enricher, which reads only the request context. A
/// field-set-exclusion test pins that the enrichment projection contains no
/// forwarded-JWT field.
/// </para>
/// <para>
/// Registered request-scoped, so a fresh request scope starts with no captured
/// token (<see cref="Current"/> is <see langword="null"/>) and one request's
/// credential can never leak into another.
/// </para>
/// </remarks>
public interface IForwardedJwtAccessor
{
    /// <summary>
    /// Gets the captured forwarded JWT for the current request, or
    /// <see langword="null"/> when none has been captured - a harmless endpoint,
    /// a pre-auth resolution, or a host that does not forward.
    /// </summary>
    ForwardedJwt? Current { get; }

    /// <summary>
    /// Captures the validated raw bearer for the current request. Call it once
    /// per request, after the inbound JWT validator has accepted the token. A
    /// blank input is ignored (never stored); a second
    /// capture within the same request scope overwrites the first
    /// (last-write-wins) - in the real pipeline a request is validated once, so a
    /// second capture does not occur.
    /// </summary>
    /// <param name="rawBearer">The validated raw bearer string to retain.</param>
    void Capture(string rawBearer);

    /// <summary>
    /// Discards the token this request captured, so <see cref="Current"/> is
    /// <see langword="null"/> again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A token can be captured here and refused afterwards. Validation can be
    /// decided from the token alone and happens at every hop, so the capture
    /// comes at the end of the validating step. Whether the session behind the
    /// token was revoked needs an external lookup, so a later step decides it,
    /// one that the boundary that owns the session store adds to its own
    /// pipeline. A gate that refuses after the capture calls this method, and
    /// nothing can then forward the refused credential.
    /// </para>
    /// <para>
    /// Calling this when nothing was captured is not an error and does nothing.
    /// </para>
    /// </remarks>
    void Clear();
}
