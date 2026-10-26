// -----------------------------------------------------------------------
// <copyright file="D2ForgeryRefusalReasons.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Forgery;

/// <summary>
/// The closed set of reasons the forgery check refuses a request. Emitted as
/// the value of the <see cref="ForgeryProtectionTelemetry.TAG_OUTCOME"/> metric
/// tag and as the reason field of the refusal log line.
/// </summary>
/// <remarks>
/// The refusal returns the caller one error code whatever the reason, so an
/// attacker learns nothing about which check failed. The reason is for the
/// operator and reaches only the metric and the log.
/// </remarks>
public static class D2ForgeryRefusalReasons
{
    /// <summary>
    /// <c>Sec-Fetch-Site</c> had the value <c>same-site</c> or
    /// <c>cross-site</c>.
    /// </summary>
    public const string METADATA_CROSS_SITE = "metadata-cross-site";

    /// <summary>
    /// <c>Sec-Fetch-Site</c> had a value outside the closed set, or more
    /// than one value.
    /// </summary>
    public const string METADATA_UNKNOWN_VALUE = "metadata-unknown-value";

    /// <summary>
    /// <c>Sec-Fetch-Site</c> was absent and <c>Origin</c> was another
    /// origin, or the literal <c>null</c>.
    /// </summary>
    public const string ORIGIN_MISMATCH = "origin-mismatch";

    /// <summary>Neither <c>Sec-Fetch-Site</c> nor <c>Origin</c> was present.</summary>
    public const string HEADERS_ABSENT = "headers-absent";

    /// <summary>The request presented no forgery token.</summary>
    public const string TOKEN_ABSENT = "token-absent";

    /// <summary>
    /// The token opened but belonged to another session, or was sealed under a
    /// retired key.
    /// </summary>
    public const string TOKEN_STALE = "token-stale";

    /// <summary>The token did not open.</summary>
    public const string TOKEN_FORGED = "token-forged";

    /// <summary>The key needed to open the token could not be reached.</summary>
    public const string TOKEN_UNVERIFIABLE = "token-unverifiable";

    /// <summary>
    /// The host registered no <see cref="ID2ForgeryTokenVerifier"/>, so the
    /// token half could not run on a request that calls for it.
    /// </summary>
    public const string VERIFIER_ABSENT = "verifier-absent";
}
