// -----------------------------------------------------------------------
// <copyright file="D2ForgeryTokenVerdict.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Forgery;

/// <summary>
/// The result of a host's verifier checking the token a request presented.
/// </summary>
public enum D2ForgeryTokenVerdict
{
    /// <summary>
    /// The request resolved no session, so there is nothing to bind a token
    /// to. The zero member, so an unchecked request reads as this.
    /// </summary>
    NoSession = 0,

    /// <summary>The token opened and is bound to the session this request resolved.</summary>
    Bound = 1,

    /// <summary>The request presented no token.</summary>
    Absent = 2,

    /// <summary>
    /// The token opened but belongs to another session, or was sealed under a key
    /// the rotation has since retired.
    /// </summary>
    Stale = 3,

    /// <summary>
    /// The token did not open: malformed, sealed under a foreign key, or the
    /// authentication tag did not match.
    /// </summary>
    Forged = 4,

    /// <summary>
    /// The key the verifier needs could not be reached, so the token could be
    /// neither accepted nor rejected. The middleware refuses the request.
    /// </summary>
    Unverifiable = 5,
}
