// -----------------------------------------------------------------------
// <copyright file="D2WorkloadIdentityOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// The host's own workload identity: the service id that is appended to the
/// propagated call-path on receipt. It is the same value the host's mutual-TLS
/// leaf certificate SPIFFE SAN encodes - a single config-bound identity, so every
/// boundary that appends a self-id appends the same one (the inbound HTTP
/// middleware, the cross-process gRPC interceptor and the system worker bootstrap
/// all do).
/// </summary>
public sealed class D2WorkloadIdentityOptions
{
    /// <summary>
    /// Gets or sets the host's own workload service id - a lowercase DNS-label-safe
    /// service label. Required: an establishment boundary appends this id to the
    /// call-path, so an unset value is a misconfiguration the registration
    /// extensions reject at host startup.
    /// </summary>
    public string ServiceId { get; set; } = string.Empty;
}
