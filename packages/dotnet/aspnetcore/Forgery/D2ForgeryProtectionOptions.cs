// -----------------------------------------------------------------------
// <copyright file="D2ForgeryProtectionOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Forgery;

/// <summary>
/// Configuration for
/// <see cref="ForgeryProtectionServiceCollectionExtensions.AddD2ForgeryProtection"/>.
/// </summary>
/// <remarks>
/// Both names are read by the middleware and by the host's token issuer, so
/// one configured name is written and read alike. The cookie name is
/// host-prefixed: a <c>__Host-</c> cookie is bound to the exact origin that
/// set it, carries no <c>Domain</c> attribute, and cannot be written by a
/// sibling subdomain. The validator rejects a name without that prefix.
/// </remarks>
public sealed record D2ForgeryProtectionOptions
{
    /// <summary>The configuration section name this options type binds from.</summary>
    public const string SECTION = "LIB_ASPNETCORE_FORGERY";

    /// <summary>Default name of the script-readable forgery-token cookie.</summary>
    public const string DEFAULT_TOKEN_COOKIE_NAME = "__Host-d2_csrf";

    /// <summary>Default name of the request header the token is echoed in.</summary>
    public const string DEFAULT_TOKEN_HEADER_NAME = "X-CSRF-Token";

    /// <summary>
    /// The prefix a token cookie name must start with. Validated at host build.
    /// </summary>
    public const string REQUIRED_COOKIE_NAME_PREFIX = "__Host-";

    /// <summary>
    /// Initializes a new <see cref="D2ForgeryProtectionOptions"/> with all
    /// documented defaults. Equivalent to the parameterized constructor
    /// invoked with all <c>null</c> arguments.
    /// </summary>
    public D2ForgeryProtectionOptions()
        : this(null, null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="D2ForgeryProtectionOptions"/>. Each
    /// parameter is nullable; passing <c>null</c> (or omitting the argument)
    /// yields the documented default for that property.
    /// </summary>
    /// <param name="tokenCookieName">
    /// Override for <see cref="TokenCookieName"/>; <c>null</c> = default
    /// <c>__Host-d2_csrf</c>.
    /// </param>
    /// <param name="tokenHeaderName">
    /// Override for <see cref="TokenHeaderName"/>; <c>null</c> = default
    /// <c>X-CSRF-Token</c>.
    /// </param>
    public D2ForgeryProtectionOptions(
        string? tokenCookieName = null,
        string? tokenHeaderName = null)
    {
        TokenCookieName = tokenCookieName ?? DEFAULT_TOKEN_COOKIE_NAME;
        TokenHeaderName = tokenHeaderName ?? DEFAULT_TOKEN_HEADER_NAME;
    }

    /// <summary>
    /// Gets or sets the name of the script-readable forgery-token cookie.
    /// Default <c>__Host-d2_csrf</c>. Settable so the configure callback and
    /// configuration binding can populate it after the DI container has
    /// constructed the instance.
    /// </summary>
    public string TokenCookieName { get; set; }

    /// <summary>
    /// Gets or sets the name of the request header a client echoes the token
    /// in. Default <c>X-CSRF-Token</c>.
    /// </summary>
    public string TokenHeaderName { get; set; }
}
