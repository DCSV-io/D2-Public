// -----------------------------------------------------------------------
// <copyright file="Role.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// User role within an organization. Stored as a lowercase string in the
/// <c>d2_org_role</c> JWT claim and in the database.
/// </summary>
/// <remarks>
/// Capability and authority over another member are separate questions about a
/// role, and they have different answers.
/// <para>
/// <b>Capability - what a role may do - is not ordered.</b> It is assigned per
/// (role, org_type) pair, declared by each scope's <c>grantedTo</c> field in
/// <c>contracts/auth-scopes/scopes.spec.json</c>. The codegen-emitted
/// <c>Scopes.GrantedScopes</c> dictionary is the canonical
/// <c>(OrgType, Role) -&gt; scope-set</c> lookup used at token mint
/// time. An <see cref="Auditor"/> holds more read scopes than an
/// <see cref="Agent"/> and no write scopes, so neither is below the other in
/// capability.
/// </para>
/// <para>
/// <b>Authority over another member - who may act on whom - is a total
/// order.</b> It is declared once, in <see cref="RoleAuthority"/>, ranking
/// <see cref="Auditor"/> below <see cref="Agent"/> below <see cref="Officer"/>
/// below <see cref="Owner"/>. Reading that order as a capability ordering
/// would grant an auditor a write.
/// </para>
/// </remarks>
public enum Role
{
    /// <summary>
    /// Auditor - extensive read access across the organization (compliance and
    /// oversight purpose). No write capability. Distinct from low-privilege:
    /// auditors typically see more data than Agents, and cannot change it.
    /// </summary>
    Auditor,

    /// <summary>
    /// Agent - standard operational role. Day-to-day reads and writes within the
    /// agent's assigned scope of work.
    /// </summary>
    Agent,

    /// <summary>
    /// Officer - org-management capabilities (member management, configuration changes,
    /// organizational policy administration).
    /// </summary>
    Officer,

    /// <summary>
    /// Owner - full control over the organization, including org-level destructive
    /// operations.
    /// </summary>
    Owner,
}
