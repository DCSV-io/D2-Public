// -----------------------------------------------------------------------
// <copyright file="RoleAuthority.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// Authority of one role over another - who may act on whom.
/// </summary>
/// <remarks>
/// This is not a capability ordering. Capability is per role and per
/// organization kind and is declared by each scope's granted-to set: an
/// <see cref="Role.Auditor"/> reads more than an <see cref="Role.Agent"/> and
/// writes nothing, and that is unchanged. This type ranks authority
/// over another member: whether one member may change, remove or invite
/// another.
/// <para>
/// There is no TypeScript counterpart, by decision. Every decision this order
/// makes is taken where the membership write happens, which is .NET; the
/// TypeScript <c>Role</c> catalog is a generated name list that nothing
/// compares. If TypeScript code needs to rank two roles, a decision has moved
/// to the browser tier, and the ordering should then be emitted from the same
/// specification the catalog comes from rather than hand-written a second time.
/// </para>
/// </remarks>
public static class RoleAuthority
{
    /// <summary>
    /// Rank within the total order. Higher outranks lower.
    /// </summary>
    /// <param name="role">The role to rank.</param>
    /// <returns>
    /// 0 for <see cref="Role.Auditor"/>, 1 for <see cref="Role.Agent"/>,
    /// 2 for <see cref="Role.Officer"/>, 3 for <see cref="Role.Owner"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="role"/> is not a declared
    /// <see cref="Role"/> member. A value outside the enumeration has no
    /// authority rank, and returning a rank for it would invent one.
    /// </exception>
    public static int RankOf(Role role) => role switch
    {
        Role.Auditor => 0,
        Role.Agent => 1,
        Role.Officer => 2,
        Role.Owner => 3,
        _ => throw new ArgumentOutOfRangeException(
            nameof(role),
            role,
            "Role carries no authority rank."),
    };

    /// <summary>
    /// Rank within the total order, for a role value that may be outside the
    /// enumeration.
    /// </summary>
    /// <param name="role">The role to rank.</param>
    /// <param name="rank">The rank when the role is a declared member.</param>
    /// <returns>
    /// True when <paramref name="role"/> is a declared member and
    /// <paramref name="rank"/> was written; false otherwise.
    /// </returns>
    public static bool TryRankOf(Role role, out int rank)
    {
        switch (role)
        {
            case Role.Auditor:
                rank = 0;
                return true;
            case Role.Agent:
                rank = 1;
                return true;
            case Role.Officer:
                rank = 2;
                return true;
            case Role.Owner:
                rank = 3;
                return true;
            default:
                rank = default;
                return false;
        }
    }

    /// <summary>
    /// True when <paramref name="actor"/> strictly outranks
    /// <paramref name="target"/>.
    /// </summary>
    /// <param name="actor">The role acting.</param>
    /// <param name="target">The role being acted on.</param>
    /// <returns>
    /// True when the actor's rank is strictly greater than the target's. The
    /// order is total over the declared members, so every declared pair is
    /// comparable, and the relation is irreflexive: a role does not outrank
    /// itself. A value outside the enumeration has no authority, so a
    /// comparison that includes one returns false whichever side it is on - this
    /// is an authority decision, and an authority decision denies rather than
    /// throws.
    /// </returns>
    public static bool Outranks(Role actor, Role target)
        => TryRankOf(actor, out var actorRank)
            && TryRankOf(target, out var targetRank)
            && actorRank > targetRank;
}
