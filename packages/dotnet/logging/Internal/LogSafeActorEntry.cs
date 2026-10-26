// -----------------------------------------------------------------------
// <copyright file="LogSafeActorEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Logging.Internal;

using DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// Log-shaped mirror of <see cref="ActorEntry"/> in which the session id is replaced by
/// a non-reversible correlation digest.
/// </summary>
/// <remarks>
/// Every other member is copied through verbatim - an operator reads the actor chain to
/// answer "who acted as whom", and blanking it would remove that answer. Only the
/// session id changes shape. A raw session id joins one session's records to each other
/// and to the person behind them.
/// </remarks>
/// <param name="Kind">Service vs impersonation actor - see <see cref="ActorEntry.Kind"/>.</param>
/// <param name="Subject">Actor identifier - see <see cref="ActorEntry.Subject"/>.</param>
/// <param name="ClientId">Service client id - see <see cref="ActorEntry.ClientId"/>.</param>
/// <param name="ImpersonationKind">
/// Consent vs force - see <see cref="ActorEntry.ImpersonationKind"/>.
/// </param>
/// <param name="SessionIdHash">
/// Correlation digest of <see cref="ActorEntry.SessionId"/>, or null when the entry has no
/// session. The raw id never appears here.
/// </param>
/// <param name="OrgId">Impersonator org id - see <see cref="ActorEntry.OrgId"/>.</param>
/// <param name="OrgName">Impersonator org name - see <see cref="ActorEntry.OrgName"/>.</param>
/// <param name="OrgType">Impersonator org type - see <see cref="ActorEntry.OrgType"/>.</param>
/// <param name="OrgRole">Impersonator org role - see <see cref="ActorEntry.OrgRole"/>.</param>
/// <param name="Act">Nested actor, projected the same way.</param>
/// <param name="ActTruncated">
/// True when the depth cap stopped the projection before a nested actor was projected, so a
/// chain that was cut can be told from a chain that ended.
/// </param>
internal sealed record LogSafeActorEntry(
    ActorKind Kind,
    string Subject,
    string? ClientId,
    ImpersonationKind? ImpersonationKind,
    string? SessionIdHash,
    Guid? OrgId,
    string? OrgName,
    OrgType? OrgType,
    Role? OrgRole,
    LogSafeActorEntry? Act,
    bool ActTruncated);
