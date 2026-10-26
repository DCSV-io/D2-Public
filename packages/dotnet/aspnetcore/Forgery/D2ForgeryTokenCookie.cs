// -----------------------------------------------------------------------
// <copyright file="D2ForgeryTokenCookie.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Forgery;

using Microsoft.AspNetCore.Http;

/// <summary>
/// The attributes of the forgery-token cookie, in one place, so the
/// middleware and the host's issuer write the same cookie.
/// </summary>
/// <remarks>
/// The cookie is readable by page script, which is how the token is
/// delivered: the page reads it and echoes it in a request header, and a
/// cross-site page cannot read a cookie set on this origin. The value opens
/// no session, so a script that reads it gains no session access. The name is
/// configurable through
/// <see cref="D2ForgeryProtectionOptions.TokenCookieName"/>.
/// </remarks>
public static class D2ForgeryTokenCookie
{
    /// <summary>Cookie path (root), as the <c>__Host-</c> prefix requires.</summary>
    public const string PATH = "/";

    /// <summary>Whether the cookie is Secure, as the <c>__Host-</c> prefix requires.</summary>
    public const bool SECURE = true;

    /// <summary>Whether the cookie is HttpOnly. It is not: page script reads it.</summary>
    public const bool HTTP_ONLY = false;

    /// <summary>The SameSite mode of the cookie.</summary>
    public const SameSiteMode SAME_SITE = SameSiteMode.Lax;

    /// <summary>
    /// The cookie attributes, with the given expiry. A delete repeats the
    /// same attributes, so it passes <c>null</c> here and lets
    /// <see cref="IResponseCookies.Delete(string, CookieOptions)"/> write the
    /// expiry.
    /// </summary>
    /// <param name="expires">
    /// The absolute expiry of the session the token is bound to, or
    /// <c>null</c> for a session cookie.
    /// </param>
    /// <returns>The cookie attributes.</returns>
    public static CookieOptions Options(DateTimeOffset? expires) =>
        new()
        {
            Path = PATH,
            Secure = SECURE,
            HttpOnly = HTTP_ONLY,
            SameSite = SAME_SITE,
            Expires = expires,
        };
}
