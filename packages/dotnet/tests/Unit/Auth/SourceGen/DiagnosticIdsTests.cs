// -----------------------------------------------------------------------
// <copyright file="DiagnosticIdsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.SourceGen;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Scopes.SourceGen;
using Xunit;

/// <summary>
/// Pins every <see cref="DiagnosticIds"/> constant to its documented identifier
/// shape and confirms there are no duplicates. Diagnostic IDs are part of the
/// library's contract: a renamed ID breaks anything that searches build logs
/// for it.
/// </summary>
public sealed class DiagnosticIdsTests
{
    [Fact]
    public void IdConstants_FollowD2SCPPrefixWithThreeDigits()
    {
        var ids = CollectIds();

        ids.Should().NotBeEmpty();
        ids.Should().AllSatisfy(id =>
            id.Should().MatchRegex(
                @"^D2SCP\d{3}$",
                because: "diagnostic IDs follow the D2SCP### convention"));
    }

    [Fact]
    public void IdConstants_AreUnique()
    {
        // Adversarial: two constants with the same string value are a
        // copy-paste error that would silently merge two distinct diagnostics
        // into one.
        var ids = CollectIds();

        ids.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("D2SCP001", nameof(DiagnosticIds.MalformedSpec))]
    [InlineData("D2SCP002", nameof(DiagnosticIds.UnknownEnumValue))]
    [InlineData("D2SCP003", nameof(DiagnosticIds.InvalidScopeName))]
    [InlineData("D2SCP004", nameof(DiagnosticIds.DuplicateScope))]
    [InlineData("D2SCP005", nameof(DiagnosticIds.AnonImpersonationBlockedNoise))]
    [InlineData("D2SCP006", nameof(DiagnosticIds.EmptyRoleArray))]
    [InlineData("D2SCP007", nameof(DiagnosticIds.TreePositionCollision))]
    [InlineData("D2SCP008", nameof(DiagnosticIds.MissingGrantedTo))]
    [InlineData("D2SCP009", nameof(DiagnosticIds.MissingSpecFile))]
    [InlineData("D2SCP010", nameof(DiagnosticIds.InternalFlagPrefixDisagreement))]
    public void IdConstants_HaveStableValues(string expectedId, string constantName)
    {
        // Stability gate: changing any of these IDs is a breaking change to the
        // build-warning surface. A renumbering must update the inline test
        // data, which puts the change in front of PR review.
        var actual = typeof(DiagnosticIds)
            .GetField(constantName, BindingFlags.Public | BindingFlags.Static)
            !.GetRawConstantValue();

        actual.Should().Be(expectedId);
    }

    private static List<string> CollectIds() =>
        typeof(DiagnosticIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();
}
