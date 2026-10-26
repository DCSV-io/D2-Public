// -----------------------------------------------------------------------
// <copyright file="DiagnosticIdsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.I18n.SourceGen;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.I18n.SourceGen;
using Xunit;

/// <summary>
/// Pins every <see cref="DiagnosticIds"/> constant to its documented identifier
/// shape and checks that no two constants share a value. The diagnostic IDs
/// are part of the library contract, and a rename breaks operator tooling that
/// searches build logs for them.
/// </summary>
public sealed class DiagnosticIdsTests
{
    [Fact]
    public void IdConstants_FollowD2I18NPrefixWithThreeDigits()
    {
        var ids = CollectIds();

        ids.Should().NotBeEmpty();
        ids.Should().AllSatisfy(id =>
            id.Should().MatchRegex(
                @"^D2I18N\d{3}$",
                because: "diagnostic IDs follow the D2I18N### convention"));
    }

    [Fact]
    public void IdConstants_AreUnique()
    {
        // Two constants with the same string value are a copy-paste error that
        // merges two distinct diagnostics into one.
        var ids = CollectIds();

        ids.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("D2I18N001", nameof(DiagnosticIds.InvalidTranslationKey))]
    [InlineData("D2I18N002", nameof(DiagnosticIds.MissingKeyInLocale))]
    [InlineData("D2I18N003", nameof(DiagnosticIds.TranslationKeyCollision))]
    [InlineData("D2I18N004", nameof(DiagnosticIds.OrphanKeyInLocale))]
    [InlineData("D2I18N005", nameof(DiagnosticIds.MissingEnUsJson))]
    [InlineData("D2I18N006", nameof(DiagnosticIds.MalformedJsonCatalog))]
    public void IdConstants_HaveStableValues(string expectedId, string constantName)
    {
        // Changing any of these IDs is a breaking change to the build-warning
        // surface. A renumbering fails this test until the inline data
        // changes, which shows the change in the pull request.
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
