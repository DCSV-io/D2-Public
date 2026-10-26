// -----------------------------------------------------------------------
// <copyright file="ID2ForgeryTokenVerifier.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Forgery;

using Microsoft.AspNetCore.Http;

/// <summary>
/// The token half of the forgery check, implemented by a host that serves a
/// session cookie.
/// </summary>
/// <remarks>
/// <para>
/// The fetch-metadata half needs nothing from the host and runs in every
/// pipeline. The token half needs the session the request resolved, which
/// only the host knows about, so the host registers this seam. A host that
/// registers none refuses every non-safe request that has no
/// <c>Authorization</c> header and whose endpoint declares
/// <see cref="D2ForgeryPostures.REQUIRED"/> or declares nothing. This seam
/// reports whether a session resolved, so a host without it cannot tell a
/// request that resolved one from a request that did not, and refuses both.
/// </para>
/// <para>
/// Register the implementation scoped. The middleware calls
/// <see cref="Verify"/> at most once per request, and
/// <see cref="IssueForResolvedSession"/> at most once per refused response.
/// </para>
/// </remarks>
public interface ID2ForgeryTokenVerifier
{
    /// <summary>
    /// Returns the verdict on the presented token against the session this
    /// request resolved. Performs at most one keyed operation. No value a
    /// request can present makes it throw.
    /// </summary>
    /// <param name="context">The current request.</param>
    /// <param name="presentedToken">
    /// The token the request presented, or <c>null</c> when it presented
    /// none.
    /// </param>
    /// <returns>The verdict.</returns>
    D2ForgeryTokenVerdict Verify(HttpContext context, string? presentedToken);

    /// <summary>
    /// Seals a token for the session this request resolved, with that
    /// session's absolute expiry.
    /// </summary>
    /// <param name="context">The current request.</param>
    /// <returns>
    /// The sealed token and its expiry, or <c>null</c> when no session
    /// resolved or the sealing key could not be reached.
    /// </returns>
    D2ForgeryTokenIssue? IssueForResolvedSession(HttpContext context);
}
