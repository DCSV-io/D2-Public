// -----------------------------------------------------------------------
// <copyright file="PeerWorkloadIdentityAccessor.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Mtls;

using Microsoft.AspNetCore.Http;

/// <summary>
/// The single accessor for the validated mutual-TLS peer workload identity,
/// independent of any capability such as signing or sealing. It returns which
/// workload is calling, taken from the certificate Kestrel already validated at
/// the TLS handshake.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fails closed.</b> The identity is derived only from
/// <see cref="ConnectionInfo.ClientCertificate"/> - the authenticated artifact
/// Kestrel populates per request only after the connection presented a client
/// certificate that the default-deny <see cref="SpiffeSanPeerValidator"/> accepted
/// (it chains to the internal CA, the SPIFFE trust domain matches, and the workload
/// is in the allowed set). No certificate returns <see langword="null"/>, which
/// means deny. There is no free-standing string slot the accessor would trust: whether
/// the connection is mTLS-authenticated and what the peer id is both come from the
/// same validated certificate.
/// </para>
/// <para>
/// <b>Set by the transport, never by the request.</b> The value comes from the TLS
/// client certificate, which Kestrel sets from the validated handshake - never from
/// a request header or body field a client controls. A remote client cannot set or
/// spoof it.
/// </para>
/// <para>
/// <b>Re-derived on every call.</b> The SAN service id is re-extracted via
/// <see cref="SpiffeSanPeerValidator.TryExtractWorkloadId"/> (the same validated
/// SAN walk and grammar parse the handshake used) on each read - no memoization, no
/// items slot. The parse is a cheap ASN.1 walk on an already-validated certificate.
/// </para>
/// <para>
/// The gRPC-transport overload (a gRPC service reaching the same validated
/// certificate via <c>ServerCallContext.GetHttpContext()</c>) delegates to this
/// <see cref="HttpContext"/> overload, so identity is derived in exactly one place
/// across both transports.
/// </para>
/// </remarks>
public static class PeerWorkloadIdentityAccessor
{
    /// <param name="httpContext">The current HTTP context.</param>
    extension(HttpContext httpContext)
    {
        /// <summary>
        /// Gets the validated mutual-TLS peer workload service id for this request,
        /// or <see langword="null"/> when the connection presented no client
        /// certificate (fail-closed) or the certificate's SAN is not a well-formed
        /// SPIFFE workload identity. The returned value is an authenticated, non-PII
        /// service label (e.g. <c>orders</c>) - never request-controlled.
        /// </summary>
        /// <returns>
        /// The peer workload service id, or <see langword="null"/> when no validated
        /// peer certificate is present (deny).
        /// </returns>
        public string? GetD2PeerWorkloadIdentity()
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            var clientCertificate = httpContext.Connection.ClientCertificate;

            // Fail-closed: no client certificate means no peer identity. Kestrel
            // populates Connection.ClientCertificate only after the default-deny
            // validator accepted the peer, so a present cert is an already-validated
            // one.
            if (clientCertificate is null)
                return null;

            return SpiffeSanPeerValidator.TryExtractWorkloadId(clientCertificate)?.ServiceId;
        }
    }
}
