// -----------------------------------------------------------------------
// <copyright file="OrgTypeTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth;

using System;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using Xunit;

public sealed class OrgTypeTests
{
    [Fact]
    public void Enum_HasExactlyFiveMembers()
    {
        // Adversarial: every new OrgType makes the generator re-emit
        // Scopes.g.cs (wildcard expansion) and may require new IsStaff and
        // IsAdmin handling.
        const int expected_count = 5;

        Enum.GetNames<OrgType>().Should().HaveCount(expected_count);
    }

    [Fact]
    public void Enum_NamesAreCanonicalSet()
    {
        Enum.GetNames<OrgType>()
            .Should().BeEquivalentTo("Admin", "Support", "Customer", "ThirdParty", "Affiliate");
    }

    [Theory]
    [InlineData(OrgType.Admin, 0)]
    [InlineData(OrgType.Support, 1)]
    [InlineData(OrgType.Customer, 2)]
    [InlineData(OrgType.ThirdParty, 3)]
    [InlineData(OrgType.Affiliate, 4)]
    public void Enum_UnderlyingIntValuesAreStable(OrgType orgType, int expected)
    {
        // Adversarial: the int values are stored in DB columns and in activity
        // tags. Reordering the members would reclassify orgs across the
        // platform with no compiler error.
        ((int)orgType).Should().Be(expected);
    }

    [Theory]
    [InlineData("Admin", OrgType.Admin)]
    [InlineData("admin", OrgType.Admin)]
    [InlineData("ADMIN", OrgType.Admin)]
    [InlineData("ThirdParty", OrgType.ThirdParty)]
    [InlineData("thirdparty", OrgType.ThirdParty)]
    [InlineData("THIRDPARTY", OrgType.ThirdParty)]
    [InlineData("Affiliate", OrgType.Affiliate)]
    [InlineData("affiliate", OrgType.Affiliate)]
    public void Parse_CaseInsensitive_RoundTrips(string input, OrgType expected)
    {
        Enum.TryParse<OrgType>(input, ignoreCase: true, out var parsed)
            .Should().BeTrue();
        parsed.Should().Be(expected);
    }

    [Fact]
    public void Parse_GarbageString_ReturnsFalse()
    {
        Enum.TryParse<OrgType>("Third Party", ignoreCase: true, out _).Should().BeFalse();
        Enum.TryParse<OrgType>("third_party", ignoreCase: true, out _).Should().BeFalse();
        Enum.TryParse<OrgType>("god", ignoreCase: true, out _).Should().BeFalse();
        Enum.TryParse<OrgType>(string.Empty, ignoreCase: true, out _).Should().BeFalse();
    }
}
