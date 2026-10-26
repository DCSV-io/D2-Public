// -----------------------------------------------------------------------
// <copyright file="D2ForgeryTokenState.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Forgery;

/// <summary>
/// What the forgery-protection middleware decided about this request, for the
/// host's token issuer to read on the response path.
/// </summary>
/// <remarks>
/// Registered scoped by
/// <see cref="ForgeryProtectionServiceCollectionExtensions.AddD2ForgeryProtection"/>,
/// so one instance serves one request and needs no lock. Both members are
/// written by the middleware and read by the host.
/// </remarks>
public sealed class D2ForgeryTokenState
{
    /// <summary>
    /// Gets a value indicating whether the check has already run on this
    /// request. A later registration of the middleware in one pipeline sees it
    /// set and passes the request through, so a pipeline runs the check at its
    /// earliest registration.
    /// </summary>
    public bool Checked { get; internal set; }

    /// <summary>
    /// Gets the host verifier's verdict on the token this request presented.
    /// Stays <see cref="D2ForgeryTokenVerdict.NoSession"/> when no verifier is
    /// registered.
    /// </summary>
    public D2ForgeryTokenVerdict Verdict { get; internal set; }
        = D2ForgeryTokenVerdict.NoSession;
}
