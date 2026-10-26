// -----------------------------------------------------------------------
// <copyright file="D2ForgeryPostures.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Forgery;

/// <summary>
/// The closed set of values <see cref="ID2ForgeryPostureDeclaration.Posture"/>
/// takes.
/// </summary>
public static class D2ForgeryPostures
{
    /// <summary>
    /// Both halves apply: the fetch-metadata check and the signed token. The
    /// posture of a cookie-authenticated mutation.
    /// </summary>
    public const string REQUIRED = "required";

    /// <summary>
    /// The fetch-metadata check applies and the token does not. The posture
    /// of a mutation reachable without a session, where there is no session
    /// to bind a token to.
    /// </summary>
    public const string METADATA = "metadata";

    /// <summary>
    /// Neither half applies. The posture of a route a provider redirects a
    /// browser to, which carries its own nonce.
    /// </summary>
    public const string EXEMPT = "exempt";
}
