// -----------------------------------------------------------------------
// <copyright file="ActorKindTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth;

using System;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using Xunit;

public sealed class ActorKindTests
{
    [Fact]
    public void Enum_HasExactlyTwoMembers()
    {
        // Adversarial: adding an ActorKind value breaks the
        // Service-versus-Impersonation discriminator across the codebase with
        // no compiler error. This test fails on any addition, which forces a
        // deliberate update.
        const int expected_count = 2;

        Enum.GetNames<ActorKind>().Should().HaveCount(expected_count);
    }

    [Fact]
    public void Enum_NamesAreServiceAndImpersonation()
    {
        Enum.GetNames<ActorKind>()
            .Should().BeEquivalentTo("Service", "Impersonation");
    }

    [Theory]
    [InlineData(ActorKind.Service, 0)]
    [InlineData(ActorKind.Impersonation, 1)]
    public void Enum_UnderlyingIntValuesAreStable(ActorKind kind, int expected)
    {
        // Adversarial: the enum int values appear as numbers in JWT activity
        // tags and AMQP headers. Reordering the members would change what each
        // number means with no compiler error.
        ((int)kind).Should().Be(expected);
    }

    [Theory]
    [InlineData("Service", ActorKind.Service)]
    [InlineData("service", ActorKind.Service)]
    [InlineData("SERVICE", ActorKind.Service)]
    [InlineData("Impersonation", ActorKind.Impersonation)]
    [InlineData("impersonation", ActorKind.Impersonation)]
    [InlineData("IMPERSONATION", ActorKind.Impersonation)]
    public void Parse_CaseInsensitive_RoundTrips(string input, ActorKind expected)
    {
        Enum.TryParse<ActorKind>(input, ignoreCase: true, out var parsed)
            .Should().BeTrue();
        parsed.Should().Be(expected);
    }

    [Fact]
    public void Parse_GarbageString_ReturnsFalse()
    {
        // Adversarial: case-insensitive parsing must not match arbitrary
        // substrings.
        Enum.TryParse<ActorKind>("Serv", ignoreCase: true, out _).Should().BeFalse();
        Enum.TryParse<ActorKind>("xyz", ignoreCase: true, out _).Should().BeFalse();
        Enum.TryParse<ActorKind>(string.Empty, ignoreCase: true, out _).Should().BeFalse();
    }
}
