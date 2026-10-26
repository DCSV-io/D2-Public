// -----------------------------------------------------------------------
// <copyright file="FriendlyUsernameGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Usernames;

using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DcsvIo.D2.Usernames;
using DcsvIo.D2.Usernames.Data;
using Xunit;

/// <summary>
/// Unit tests for <see cref="FriendlyUsernameGenerator"/> and default word lists.
/// </summary>
public sealed partial class FriendlyUsernameGeneratorTests
{
    [Fact]
    public void Username_ListCounts_4096()
    {
        DefaultUsernameAdjectives.All.Count.Should().Be(4096);
        DefaultUsernameNouns.All.Count.Should().Be(4096);
    }

    [Fact]
    public void DefaultAdjectives_AmericanSpelling_HoldsGrayed()
    {
        DefaultUsernameAdjectives.All.Should().Contain("Grayed");
        DefaultUsernameAdjectives.All.Should().NotContain("Greyed"); // british-spelling:exempt-line
    }

    [Fact]
    public void Username_Generate_FormatAndSuffixRange()
    {
        for (var i = 0; i < 32; i++)
        {
            var result = FriendlyUsernameGenerator.GenerateFriendlyUsername();
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();

            var friendly = result.Data;
            UsernameShape().IsMatch(friendly.Username).Should().BeTrue();
            DisplayUsernameShape().IsMatch(friendly.DisplayUsername).Should().BeTrue();

            var suffixText = SuffixCapture().Match(friendly.Username).Groups[1].Value;
            var suffix = int.Parse(suffixText, System.Globalization.CultureInfo.InvariantCulture);
            suffix.Should().BeInRange(1, 999);
        }
    }

    [Fact]
    public void Username_Generate_LowercaseCanonical_DisplayPascal()
    {
        var result = FriendlyUsernameGenerator.GenerateFriendlyUsername();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();

        var friendly = result.Data;
        friendly.Username.Should().Be(friendly.DisplayUsername.ToLowerInvariant());
        char.IsUpper(friendly.DisplayUsername[0]).Should().BeTrue();
        friendly.Username.Any(char.IsUpper).Should().BeFalse();
    }

    [Fact]
    public void Username_MaxWordLength_Respected()
    {
        var options = new FriendlyUsernameGeneratorOptions
        {
            Adjectives = ["TooLongWordHere"],
            Nouns = ["Ok"],
            MaxWordLength = 12,
        };

        var result = FriendlyUsernameGenerator.GenerateFriendlyUsername(options);
        result.Failed.Should().BeTrue();
        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public void Username_EmptyListsConfig_ValidationOrDefault()
    {
        var emptyAdjectives = new FriendlyUsernameGeneratorOptions
        {
            Adjectives = [],
            Nouns = ["Noun"],
        };

        var emptyNouns = new FriendlyUsernameGeneratorOptions
        {
            Adjectives = ["Adj"],
            Nouns = [],
        };

        var a = FriendlyUsernameGenerator.GenerateFriendlyUsername(emptyAdjectives);
        var n = FriendlyUsernameGenerator.GenerateFriendlyUsername(emptyNouns);

        a.Failed.Should().BeTrue();
        a.IsValidationFailed.Should().BeTrue();
        n.Failed.Should().BeTrue();
        n.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public void Username_InvalidConfig_ReturnsD2ResultNotThrow()
    {
        var invertedSuffix = new FriendlyUsernameGeneratorOptions
        {
            Adjectives = ["Adj"],
            Nouns = ["Noun"],
            MinSuffix = 10,
            MaxSuffix = 1,
        };

        var result = FriendlyUsernameGenerator.GenerateFriendlyUsername(invertedSuffix);
        result.Failed.Should().BeTrue();
        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public void Username_FalseyWordInOverrideList_ValidationFailed()
    {
        var options = new FriendlyUsernameGeneratorOptions
        {
            Adjectives = [" "],
            Nouns = ["Noun"],
        };

        var result = FriendlyUsernameGenerator.GenerateFriendlyUsername(options);
        result.Failed.Should().BeTrue();
        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public void Username_CustomLists_UsesOverrideAndSuffixBounds()
    {
        var options = new FriendlyUsernameGeneratorOptions
        {
            Adjectives = ["Red"],
            Nouns = ["Fox"],
            MinSuffix = 7,
            MaxSuffix = 7,
        };

        var result = FriendlyUsernameGenerator.GenerateFriendlyUsername(options);
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Username.Should().Be("redfox7");
        result.Data.DisplayUsername.Should().Be("RedFox7");
    }

    [Fact]
    public void Username_MaxSuffixAtIntMaxValue_ValidationFailed()
    {
        var options = new FriendlyUsernameGeneratorOptions
        {
            Adjectives = ["Red"],
            Nouns = ["Fox"],
            MinSuffix = 1,
            MaxSuffix = int.MaxValue,
        };

        // The exclusive bound is MaxSuffix + 1, which overflows here. The
        // generator returns ValidationFailed instead of letting
        // RandomNumberGenerator.GetInt32 throw.
        var result = FriendlyUsernameGenerator.GenerateFriendlyUsername(options);

        result.Failed.Should().BeTrue();
        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public void Username_MaxSuffixOneBelowIntMaxValue_Succeeds()
    {
        var options = new FriendlyUsernameGeneratorOptions
        {
            Adjectives = ["Red"],
            Nouns = ["Fox"],
            MinSuffix = int.MaxValue - 1,
            MaxSuffix = int.MaxValue - 1,
        };

        var result = FriendlyUsernameGenerator.GenerateFriendlyUsername(options);

        result.Success.Should().BeTrue();
        result.Data!.DisplayUsername.Should().Be("RedFox2147483646");
    }

    /// <summary>
    /// Anchored pattern of linear character classes. The generator bounds the input, so
    /// the pattern sets no timeout.
    /// </summary>
    [GeneratedRegex("^[a-z]+[1-9][0-9]{0,2}$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernameShape();

    /// <summary>
    /// Anchored pattern of linear character classes. The generator bounds the input, so
    /// the pattern sets no timeout.
    /// </summary>
    [GeneratedRegex("^[A-Z][a-zA-Z]+[1-9][0-9]{0,2}$", RegexOptions.CultureInvariant)]
    private static partial Regex DisplayUsernameShape();

    /// <summary>
    /// End-anchored pattern of linear character classes. The generator bounds the input,
    /// so the pattern sets no timeout.
    /// </summary>
    [GeneratedRegex("([1-9][0-9]{0,2})$", RegexOptions.CultureInvariant)]
    private static partial Regex SuffixCapture();
}
