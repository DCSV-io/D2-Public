// -----------------------------------------------------------------------
// <copyright file="D2ForgeryHeaderNames.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Forgery;

/// <summary>
/// The request headers the fetch-metadata half of the forgery check reads.
/// </summary>
/// <remarks>
/// The browser sets both headers, and neither name is configurable, because the
/// check reads them to tell a request that this origin's own page issued from a
/// request that another site issued. The token header name is configurable
/// through <see cref="D2ForgeryProtectionOptions.TokenHeaderName"/>.
/// </remarks>
public static class D2ForgeryHeaderNames
{
    /// <summary>
    /// The fetch-metadata header that states the relationship between the page
    /// that issued the request and the origin serving it.
    /// </summary>
    public const string SEC_FETCH_SITE = "Sec-Fetch-Site";

    /// <summary>
    /// The origin header, read only when
    /// <see cref="SEC_FETCH_SITE"/> is absent.
    /// </summary>
    public const string ORIGIN = "Origin";

    /// <summary>
    /// The <see cref="SEC_FETCH_SITE"/> value for a request from the same
    /// origin. Admitted.
    /// </summary>
    public const string SEC_FETCH_SITE_SAME_ORIGIN = "same-origin";

    /// <summary>
    /// The <see cref="SEC_FETCH_SITE"/> value for a request with no initiating
    /// page, such as an address-bar navigation or a script the user ran.
    /// Admitted.
    /// </summary>
    public const string SEC_FETCH_SITE_NONE = "none";

    /// <summary>
    /// The <see cref="SEC_FETCH_SITE"/> value for a request from a different
    /// origin under the same registrable domain. Refused: under a one-origin
    /// topology no legitimate request is same-site, and a sibling subdomain
    /// with an injection is the documented SameSite bypass.
    /// </summary>
    public const string SEC_FETCH_SITE_SAME_SITE = "same-site";

    /// <summary>
    /// The <see cref="SEC_FETCH_SITE"/> value for a request from an unrelated
    /// origin. Refused.
    /// </summary>
    public const string SEC_FETCH_SITE_CROSS_SITE = "cross-site";
}
