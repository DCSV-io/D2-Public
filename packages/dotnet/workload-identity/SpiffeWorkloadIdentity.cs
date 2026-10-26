// -----------------------------------------------------------------------
// <copyright file="SpiffeWorkloadIdentity.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Spiffe;

using System.Text.RegularExpressions;
using DcsvIo.D2.ErrorCodes.Category;

/// <summary>
/// Strongly typed value object for a single workload's SPIFFE identity: the
/// subject-alternative-name that a leaf certificate contains and a peer validator
/// checks, written <c>spiffe://d2.internal/workload/&lt;service&gt;</c>.
/// </summary>
/// <remarks>
/// <b>The single SPIFFE grammar.</b> This is the one place the SPIFFE
/// workload-identity format lives. <see cref="Create"/> validates a bare service
/// identifier, and <see cref="Parse"/> validates the URI SAN of a presented
/// certificate. There is one parser for the format, never two.
///
/// <b>Not PII.</b> A workload identity is a service label such as <c>inventory</c>
/// or <c>reports</c>, which is not personally identifying. Do not apply
/// <c>[RedactData]</c> to this type.
///
/// <b>Wire-format constants.</b> The trust-domain, scheme, and path-prefix
/// literals are anchored to the wire format and the SPIFFE spec: the literal
/// value is the SPIFFE format. Use these constants instead of raw strings.
///
/// <b>Two construction paths.</b> <see cref="Create"/> validates a bare service
/// identifier (the issuance side); <see cref="Parse"/> validates a full SPIFFE
/// URI extracted from a presented certificate's SAN (the peer-validation side).
/// Both reject anything that is not a well-formed lowercase workload identity
/// inside the <c>d2.internal</c> trust domain with the same generic
/// <c>ValidationFailed</c> failure. This default-deny posture does not leak
/// which check failed.
///
/// Validation enforces on the service identifier:
/// <list type="bullet">
///   <item>Non-null, non-empty, non-whitespace.</item>
///   <item>Maximum length of <see cref="_SERVICE_ID_MAX"/> characters.</item>
///   <item>Lowercase DNS-label-safe charset: <c>[a-z0-9-]</c> only.</item>
/// </list>
/// </remarks>
public sealed partial record SpiffeWorkloadIdentity
{
    /// <summary>The SPIFFE URI scheme.</summary>
    public const string SCHEME = "spiffe";

    /// <summary>
    /// The internal SPIFFE trust domain, which equals the internal token audience.
    /// </summary>
    public const string TRUST_DOMAIN = "d2.internal";

    /// <summary>The SPIFFE path prefix that every D2 workload identity starts with.</summary>
    public const string WORKLOAD_PATH_PREFIX = "/workload/";

    private const int _SERVICE_ID_MAX = 64;

    // No-backtracking pattern: a single anchored character class with no
    // alternation or repetition that could backtrack. Input is length-capped to
    // _SERVICE_ID_MAX before the match, so no timeout is required.
    private static readonly Regex sr_serviceIdCharset = ServiceIdCharsetRegex();

    /// <summary>Gets the normalized lowercase service identifier (e.g. <c>inventory</c>).</summary>
    public required string ServiceId { get; init; }

    /// <summary>
    /// Gets the full SPIFFE subject-alternative-name URI emitted onto a leaf
    /// certificate (e.g. <c>spiffe://d2.internal/workload/inventory</c>).
    /// </summary>
    public string Uri =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{SCHEME}://{TRUST_DOMAIN}{WORKLOAD_PATH_PREFIX}{ServiceId}");

    /// <summary>
    /// Validates and constructs a <see cref="SpiffeWorkloadIdentity"/> from a raw
    /// service identifier (the issuance side).
    /// </summary>
    /// <param name="serviceId">Raw service identifier (may be null or whitespace).</param>
    /// <returns>
    /// <c>Ok</c> with the validated <see cref="SpiffeWorkloadIdentity"/> on success;
    /// generic <c>ValidationFailed</c> on failure.
    /// </returns>
    public static D2Result<SpiffeWorkloadIdentity> Create(string? serviceId)
    {
        var normalized = serviceId.ToNullIfEmpty()?.Trim().ToLowerInvariant();

        if (normalized is null)
            return Invalid();

        if (normalized.Length > _SERVICE_ID_MAX)
            return Invalid();

        if (!sr_serviceIdCharset.IsMatch(normalized))
            return Invalid();

        return D2Result<SpiffeWorkloadIdentity>.Ok(
            new SpiffeWorkloadIdentity { ServiceId = normalized });
    }

    /// <summary>
    /// Validates and constructs a <see cref="SpiffeWorkloadIdentity"/> from a full
    /// SPIFFE URI extracted from a presented certificate's subject-alternative-name
    /// (the peer-validation side). Checks that the scheme is <c>spiffe</c>, the host
    /// is the <c>d2.internal</c> trust domain, the path begins with <c>/workload/</c>,
    /// and the remaining service identifier passes <see cref="Create"/>.
    /// </summary>
    /// <param name="uri">The raw SAN URI (may be null, malformed, or foreign).</param>
    /// <returns>
    /// <c>Ok</c> with the parsed <see cref="SpiffeWorkloadIdentity"/> on success;
    /// generic <c>ValidationFailed</c> for any wrong-scheme, wrong-trust-domain,
    /// missing-path, or malformed input (default-deny).
    /// </returns>
    public static D2Result<SpiffeWorkloadIdentity> Parse(string? uri)
    {
        var normalized = uri.ToNullIfEmpty();

        if (normalized is null)
            return Invalid();

        if (!System.Uri.TryCreate(normalized, UriKind.Absolute, out var parsed))
            return Invalid();

        if (!string.Equals(parsed.Scheme, SCHEME, StringComparison.Ordinal))
            return Invalid();

        if (!string.Equals(parsed.Host, TRUST_DOMAIN, StringComparison.Ordinal))
            return Invalid();

        if (!parsed.AbsolutePath.StartsWith(WORKLOAD_PATH_PREFIX, StringComparison.Ordinal))
            return Invalid();

        var serviceId = parsed.AbsolutePath[WORKLOAD_PATH_PREFIX.Length..];

        return Create(serviceId);
    }

    /// <summary>
    /// Reconstructs a <see cref="SpiffeWorkloadIdentity"/> from a trusted,
    /// previously-validated service identifier without re-running validation. Use it
    /// for store-side rehydration only; use <see cref="Create"/> or <see cref="Parse"/>
    /// for all untrusted input.
    /// </summary>
    /// <param name="serviceId">The stored service identifier.</param>
    /// <returns>
    /// A <see cref="SpiffeWorkloadIdentity"/> whose <see cref="ServiceId"/> is set verbatim.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="serviceId"/> is <see langword="null"/>, empty, or whitespace.
    /// A corrupt stored value with an empty service id is a data-corruption error, not valid input.
    /// </exception>
    public static SpiffeWorkloadIdentity FromTrusted(string serviceId)
    {
        serviceId.ThrowIfFalsey();

        return new() { ServiceId = serviceId };
    }

    /// <summary>
    /// Builds the generic default-deny failure shared by every rejection path.
    /// The shared grammar defines no domain-specific error code: a malformed
    /// identity is a generic <c>ValidationFailed</c>.
    /// </summary>
    private static D2Result<SpiffeWorkloadIdentity> Invalid() =>
        D2Result<SpiffeWorkloadIdentity>.ValidationFailed(
            category: ErrorCategory.ValidationFailure);

    /// <summary>
    /// Lowercase DNS-label-safe charset: lowercase letters, digits, hyphens only.
    /// No backtracking: a single anchored character class with no
    /// alternation or repetition that could backtrack, so no timeout is required.
    /// </summary>
    [GeneratedRegex(@"^[a-z0-9-]+$", RegexOptions.None)]
    private static partial Regex ServiceIdCharsetRegex();
}
