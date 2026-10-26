// -----------------------------------------------------------------------
// <copyright file="D2MutualTlsOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Mtls;

using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Configuration for <see cref="MutualTlsHostExtensions.AddD2MutualTls"/>. When
/// <see cref="Enabled"/> the host's Kestrel HTTPS endpoint is configured to
/// require a client certificate and validate it with the default-deny
/// <see cref="SpiffeSanPeerValidator"/>. Validated at host build: an enabled host
/// with no trust anchors, or with a non-canonical allowed-workload entry, throws at
/// startup rather than silently accepting any certificate.
/// </summary>
/// <remarks>
/// <para>
/// <b>Off by default.</b> mTLS is off unless a host explicitly opts in, such as a
/// local development setup or a host that other processes call. A host that has
/// not wired it must not start requiring client certificates and lock itself out.
/// </para>
/// <para>
/// <b>The host owns trust-anchor sourcing.</b> <see cref="TrustAnchorsProvider"/>
/// supplies the public internal root certificates the validator chains a
/// presented certificate to, never private keys. This keeps
/// <c>DcsvIo.D2.AspNetCore</c> free of any file, secret or key-management-service
/// dependency: the host loads its anchor however it sources it, for example from
/// a local certificate authority in development or from a certificate-authority
/// provider in production.
/// </para>
/// </remarks>
public sealed class D2MutualTlsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the host's Kestrel HTTPS endpoint
    /// requires and validates a client certificate (mutual TLS).
    /// Default <c>false</c> - mTLS is opt-in. When <c>true</c>,
    /// <see cref="TrustAnchorsProvider"/> must be set and every entry of
    /// <see cref="AllowedWorkloads"/> must be canonical, or the host throws at build.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the receiver's allowed-workload set - the lowercase service
    /// identifiers permitted to call this host (e.g. <c>["orders", "inventory"]</c>).
    /// This set is the workload-membership condition of the default-deny peer
    /// check: a presented certificate whose SPIFFE workload identifier is not a
    /// member is rejected. Default <c>[]</c>, which admits no peer while the port
    /// keeps requiring a client certificate. Each entry is compared ordinally
    /// against a SPIFFE service id that is already lowercase, so an entry with an
    /// upper-case letter or any whitespace matches nothing; the host rejects such
    /// an entry at build and the failure message identifies it.
    /// </summary>
    public IReadOnlyList<string> AllowedWorkloads { get; set; } = [];

    /// <summary>
    /// Gets or sets the provider that supplies the public internal certificate-
    /// authority trust anchors - the roots a presented certificate must chain
    /// to (the chain condition of the peer check). The host supplies the loaded
    /// public roots however it sourced them; this provider is invoked once per
    /// validation to build a fresh chain. Returns public certificates only,
    /// never private keys. Required (non-null) when <see cref="Enabled"/>.
    /// </summary>
    public Func<X509Certificate2Collection>? TrustAnchorsProvider { get; set; }
}
