// -----------------------------------------------------------------------
// <copyright file="FriendlyUsernameGeneratorOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Usernames;

using AwesomeAssertions;
using DcsvIo.D2.Usernames;
using Xunit;

/// <summary>
/// Pins every constant on <see cref="FriendlyUsernameGeneratorOptions"/> to its
/// exact value. The suffix bounds decide the size of the generated name space,
/// so a value silently lowered narrows it with the whole suite green.
/// </summary>
public sealed class FriendlyUsernameGeneratorOptionsTests
{
    [Fact]
    public void DefaultMinSuffix_IsOne()
    {
        FriendlyUsernameGeneratorOptions.DEFAULT_MIN_SUFFIX.Should().Be(1);
    }

    [Fact]
    public void DefaultMaxSuffix_IsNineHundredNinetyNine()
    {
        FriendlyUsernameGeneratorOptions.DEFAULT_MAX_SUFFIX.Should().Be(999);
    }

    [Fact]
    public void DefaultMaxWordLength_IsTwelve()
    {
        FriendlyUsernameGeneratorOptions.DEFAULT_MAX_WORD_LENGTH.Should().Be(12);
    }

    [Fact]
    public void PropertyDefaults_MatchTheDefaultConstants()
    {
        var options = new FriendlyUsernameGeneratorOptions();

        options.MinSuffix.Should().Be(FriendlyUsernameGeneratorOptions.DEFAULT_MIN_SUFFIX);
        options.MaxSuffix.Should().Be(FriendlyUsernameGeneratorOptions.DEFAULT_MAX_SUFFIX);
        options.MaxWordLength.Should()
            .Be(FriendlyUsernameGeneratorOptions.DEFAULT_MAX_WORD_LENGTH);
        options.Adjectives.Should().BeNull();
        options.Nouns.Should().BeNull();
    }
}
