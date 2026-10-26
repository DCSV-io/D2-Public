// -----------------------------------------------------------------------
// <copyright file="RoleAuthorityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth;

using System;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using Xunit;

public sealed class RoleAuthorityTests
{
    [Theory]
    [InlineData(Role.Auditor, 0)]
    [InlineData(Role.Agent, 1)]
    [InlineData(Role.Officer, 2)]
    [InlineData(Role.Owner, 3)]
    public void RankOf_DeclaredRole_ReturnsItsRank(Role role, int expected)
    {
        RoleAuthority.RankOf(role).Should().Be(expected);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void RankOf_ValueOutsideTheEnumeration_Throws(int raw)
    {
        var act = () => RoleAuthority.RankOf((Role)raw);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(Role.Owner, Role.Officer)]
    [InlineData(Role.Owner, Role.Agent)]
    [InlineData(Role.Owner, Role.Auditor)]
    [InlineData(Role.Officer, Role.Agent)]
    [InlineData(Role.Officer, Role.Auditor)]
    [InlineData(Role.Agent, Role.Auditor)]
    public void Outranks_HigherOverLower_IsTrue(Role actor, Role target)
    {
        RoleAuthority.Outranks(actor, target).Should().BeTrue();
    }

    [Theory]
    [InlineData(Role.Officer, Role.Owner)]
    [InlineData(Role.Agent, Role.Owner)]
    [InlineData(Role.Auditor, Role.Owner)]
    [InlineData(Role.Agent, Role.Officer)]
    [InlineData(Role.Auditor, Role.Officer)]
    [InlineData(Role.Auditor, Role.Agent)]
    public void Outranks_LowerOverHigher_IsFalse(Role actor, Role target)
    {
        RoleAuthority.Outranks(actor, target).Should().BeFalse();
    }

    [Theory]
    [InlineData(Role.Auditor)]
    [InlineData(Role.Agent)]
    [InlineData(Role.Officer)]
    [InlineData(Role.Owner)]
    public void Outranks_IsIrreflexive_ARoleDoesNotOutrankItself(Role role)
    {
        RoleAuthority.Outranks(role, role).Should().BeFalse();
    }

    [Fact]
    public void Outranks_IsTotal_EveryDistinctPairIsComparableInExactlyOneDirection()
    {
        var roles = Enum.GetValues<Role>();

        foreach (var a in roles)
        {
            foreach (var b in roles.Where(r => r != a))
            {
                (RoleAuthority.Outranks(a, b) ^ RoleAuthority.Outranks(b, a))
                    .Should().BeTrue(because: $"{a} and {b} must be comparable one way");
            }
        }
    }

    [Fact]
    public void RankOf_IsInjective_NoTwoRolesShareARank()
    {
        var ranks = Enum.GetValues<Role>().Select(RoleAuthority.RankOf).ToArray();

        ranks.Should().OnlyHaveUniqueItems();
    }

    // Outranks returns false for a value outside the enumeration and does not throw.
    [Theory]
    [InlineData(-1, Role.Owner)]
    [InlineData(4, Role.Owner)]
    public void Outranks_ActorOutsideTheEnumeration_IsFalse(int rawActor, Role target)
    {
        RoleAuthority.Outranks((Role)rawActor, target).Should().BeFalse();
    }

    [Fact]
    public void Outranks_TargetOutsideTheEnumeration_IsFalse()
    {
        RoleAuthority.Outranks(Role.Owner, (Role)9).Should().BeFalse();
    }

    [Theory]
    [InlineData(Role.Auditor, 0)]
    [InlineData(Role.Agent, 1)]
    [InlineData(Role.Officer, 2)]
    [InlineData(Role.Owner, 3)]
    public void TryRankOf_DeclaredRole_WritesItsRank(Role role, int expected)
    {
        RoleAuthority.TryRankOf(role, out var rank).Should().BeTrue();
        rank.Should().Be(expected);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void TryRankOf_ValueOutsideTheEnumeration_IsFalse(int raw)
    {
        RoleAuthority.TryRankOf((Role)raw, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(Role.Auditor)]
    [InlineData(Role.Agent)]
    [InlineData(Role.Officer)]
    [InlineData(Role.Owner)]
    public void TryRankOf_AgreesWithRankOf_OverEveryDeclaredMember(Role role)
    {
        RoleAuthority.TryRankOf(role, out var rank).Should().BeTrue();
        rank.Should().Be(RoleAuthority.RankOf(role));
    }
}
