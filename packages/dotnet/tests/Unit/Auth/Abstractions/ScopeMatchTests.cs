// -----------------------------------------------------------------------
// <copyright file="ScopeMatchTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Abstractions;

using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using Xunit;

/// <summary>
/// Reflection-based name and count pins for <see cref="ScopeMatch"/>.
/// Mirrors the <c>HandlerScopeMatch</c> pins in
/// <c>DcsvIo.D2.Tests.Unit.Handler.ScopeRequirementTests</c>. Code that
/// branches on <see cref="ScopeMatch"/> depends on the member names and the
/// count, so a rename must fail a test visibly.
/// </summary>
/// <remarks>
/// These tests deliberately do not use <c>nameof()</c> in the expected-value
/// arguments: <c>nameof</c> is resolved at compile time and would follow any
/// rename, which defeats the pin. The literal strings here are the
/// contract.
/// </remarks>
public sealed class ScopeMatchTests
{
    [Fact]
    public void ScopeMatch_HasAnyMember_WithExpectedName()
    {
        var member = typeof(ScopeMatch).GetField("Any");

        member.Should().NotBeNull();
        member.Name.Should().Be("Any");
    }

    [Fact]
    public void ScopeMatch_HasAllMember_WithExpectedName()
    {
        var member = typeof(ScopeMatch).GetField("All");

        member.Should().NotBeNull();
        member.Name.Should().Be("All");
    }

    [Fact]
    public void ScopeMatch_EnumHasExactlyTwoMembers()
    {
        // Adversarial: a member added to ScopeMatch requires changes wherever
        // code switches on ScopeMatch. Pin the count so that an addition fails
        // this test and forces a deliberate review.
        var members = typeof(ScopeMatch).GetFields(BindingFlags.Public | BindingFlags.Static);

        members.Should().HaveCount(2);
    }
}
