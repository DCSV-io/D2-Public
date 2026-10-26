// -----------------------------------------------------------------------
// <copyright file="ImpersonationKind.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// Sub-discriminator for <see cref="ActorKind.Impersonation"/> entries - which
/// impersonation flavor (<see cref="Consent"/> or <see cref="Force"/>) this actor
/// used. Sourced from the <c>act.d2_kind</c> JWT claim.
/// </summary>
/// <remarks>
/// Which callers may start each flavor is decided by the deploying product's scope
/// catalog, which grants <see cref="Consent"/> and <see cref="Force"/> separately.
/// This type records only which flavor was used.
/// </remarks>
public enum ImpersonationKind
{
    /// <summary>
    /// Consent-based impersonation - the impersonated user authorized the session with
    /// a one-time code sent to them.
    /// </summary>
    Consent,

    /// <summary>
    /// Force impersonation - no user consent, and not surfaced to the impersonated
    /// user. A durable record is kept for internal audit.
    /// </summary>
    Force,
}
