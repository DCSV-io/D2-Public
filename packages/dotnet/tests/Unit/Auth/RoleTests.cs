// -----------------------------------------------------------------------
// <copyright file="RoleTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth;

using System;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using Xunit;

public sealed class RoleTests
{
    [Fact]
    public void Enum_HasExactlyFourMembers()
    {
        // Adversarial: the role set is closed. A new role requires the
        // generator to re-emit Scopes.g.cs and a rank in RoleAuthority.
        const int expected_count = 4;

        Enum.GetNames<Role>().Should().HaveCount(expected_count);
    }

    [Fact]
    public void Enum_NamesAreCanonicalSet()
    {
        Enum.GetNames<Role>()
            .Should().BeEquivalentTo("Auditor", "Agent", "Officer", "Owner");
    }

    [Theory]
    [InlineData(Role.Auditor, 0)]
    [InlineData(Role.Agent, 1)]
    [InlineData(Role.Officer, 2)]
    [InlineData(Role.Owner, 3)]
    public void Enum_UnderlyingIntValuesAreStable(Role role, int expected)
    {
        ((int)role).Should().Be(expected);
    }

    [Theory]
    [InlineData("Auditor", Role.Auditor)]
    [InlineData("auditor", Role.Auditor)]
    [InlineData("AUDITOR", Role.Auditor)]
    [InlineData("Agent", Role.Agent)]
    [InlineData("Officer", Role.Officer)]
    [InlineData("Owner", Role.Owner)]
    public void Parse_CaseInsensitive_RoundTrips(string input, Role expected)
    {
        Enum.TryParse<Role>(input, ignoreCase: true, out var parsed)
            .Should().BeTrue();
        parsed.Should().Be(expected);
    }

    [Fact]
    public void Parse_GarbageString_ReturnsFalse()
    {
        Enum.TryParse<Role>("Admin", ignoreCase: true, out _).Should().BeFalse();
        Enum.TryParse<Role>("Manager", ignoreCase: true, out _).Should().BeFalse();
        Enum.TryParse<Role>(string.Empty, ignoreCase: true, out _).Should().BeFalse();
    }

    [Fact]
    public void Roles_UnderlyingValuesAreDeclarationOrder_NotTheAuthorityOrder()
    {
        // The underlying integers are declaration order and nothing else.
        // Capability, what a role may do, is defined per (role, org type) in
        // scopes.spec.json. Authority over another member is the total order
        // declared in RoleAuthority. Neither is derived from arithmetic on
        // these values, so code that reads (int)role as a privilege rank
        // misreads them.
        var roleInts = Enum.GetValues<Role>().Select(r => (int)r).ToArray();

        roleInts.Should().BeEquivalentTo([0, 1, 2, 3]);
        ((int)Role.Auditor).Should().BeLessThan((int)Role.Agent);
    }
}
