// -----------------------------------------------------------------------
// <copyright file="ScopeEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Scopes.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;

/// <summary>
/// One scope entry parsed from the spec.
/// </summary>
/// <param name="Name">
/// Dot-separated lowercase name (for example <c>"orders.refund.approve"</c>).
/// Each segment must be a valid C# identifier when PascalCased. Anonymous
/// scopes live under the <c>anon.*</c> namespace.
/// </param>
/// <param name="Description">
/// Free-form description used in XML doc comments on the emitted constants.
/// </param>
/// <param name="ImpersonationBlocked">
/// When true, the entry service strips the scope from impersonated tokens at mint time.
/// This is defense in depth: with the scope absent, the RequiredScopes check
/// already fails for blocked actions. The flag has no meaning on anonymous
/// scopes, and the D2SCP005 warning reports it there.
/// </param>
/// <param name="GrantedTo">
/// Per-(OrgType, Role) grant matrix. Keys are OrgType names (for example <c>"Admin"</c>)
/// or <c>"*"</c> for all org types; values are arrays of Role names (for example
/// <c>["Owner", "Officer"]</c>) or <c>["*"]</c> for all roles. Wildcards expand
/// at codegen time against the OrgType / Role enum values. Null on anonymous
/// scopes, which the namespace convention grants to every caller before
/// authentication. D2SCP008 reports a non-anonymous scope that omits it.
/// </param>
/// <param name="Deprecated">
/// When true, the emitted constant has <c>[Obsolete]</c>. A scope entry is
/// never deleted, because deleting one is a wire break. Retiring an entry marks
/// the constant, so the compiler reports each use of it.
/// </param>
/// <param name="DeprecatedReason">
/// Developer-facing English rendered into the <c>[Obsolete]</c> message. Null
/// where the entry gives no reason.
/// </param>
/// <param name="ReplacedBy">
/// The successor entry's scope name, appended to the <c>[Obsolete]</c> message
/// so the message states the replacement and not only that the constant is obsolete.
/// </param>
/// <param name="Internal">
/// When true, the scope is workload-only: granted by the internal
/// transaction-token mint rather than the per-(OrgType, Role) grant matrix, so
/// it may omit <paramref name="GrantedTo"/>, and an untrusted caller can never
/// present it. The declaration must agree with the <c>internal.</c>
/// name prefix (D2SCP010 reports a disagreement in either direction).
/// </param>
internal sealed record ScopeEntry(
    string Name,
    string? Description,
    bool ImpersonationBlocked,
    IReadOnlyDictionary<string, ImmutableArray<string>>? GrantedTo,
    bool Deprecated = false,
    string? DeprecatedReason = null,
    string? ReplacedBy = null,
    bool Internal = false);
