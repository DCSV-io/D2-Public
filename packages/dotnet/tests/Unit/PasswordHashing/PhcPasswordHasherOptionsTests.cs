// -----------------------------------------------------------------------
// <copyright file="PhcPasswordHasherOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.PasswordHashing;

using AwesomeAssertions;
using DcsvIo.D2.PasswordHashing;
using Xunit;

/// <summary>
/// Pins every constant on <see cref="PhcPasswordHasherOptions"/> to its exact
/// value. These are the Argon2id cost floors and the bounds the verify path
/// admits a stored PHC string within, so a value silently lowered would weaken
/// every hash the platform writes.
/// </summary>
public sealed class PhcPasswordHasherOptionsTests
{
    [Fact]
    public void DefaultMemoryKib_IsSixtyFourMebibytes()
    {
        PhcPasswordHasherOptions.DEFAULT_MEMORY_KIB.Should().Be(65536);
    }

    [Fact]
    public void DefaultIterations_IsThree()
    {
        PhcPasswordHasherOptions.DEFAULT_ITERATIONS.Should().Be(3);
    }

    [Fact]
    public void DefaultParallelism_IsOne()
    {
        PhcPasswordHasherOptions.DEFAULT_PARALLELISM.Should().Be(1);
    }

    [Fact]
    public void DefaultSaltLength_IsSixteenBytes()
    {
        PhcPasswordHasherOptions.DEFAULT_SALT_LENGTH.Should().Be(16);
    }

    [Fact]
    public void DefaultHashLength_IsThirtyTwoBytes()
    {
        PhcPasswordHasherOptions.DEFAULT_HASH_LENGTH.Should().Be(32);
    }

    [Fact]
    public void MaxPasswordLength_IsOneHundredTwentyEight()
    {
        PhcPasswordHasherOptions.MAX_PASSWORD_LENGTH.Should().Be(128);
    }

    [Fact]
    public void AcceptedMemoryBounds_AreEightKibibytesToOneGibibyte()
    {
        PhcPasswordHasherOptions.MIN_ACCEPTED_MEMORY_KIB.Should().Be(8);
        PhcPasswordHasherOptions.MAX_ACCEPTED_MEMORY_KIB.Should().Be(1048576);
    }

    [Fact]
    public void AcceptedIterationBounds_AreOneToSixteen()
    {
        PhcPasswordHasherOptions.MIN_ACCEPTED_ITERATIONS.Should().Be(1);
        PhcPasswordHasherOptions.MAX_ACCEPTED_ITERATIONS.Should().Be(16);
    }

    [Fact]
    public void AcceptedParallelismBounds_AreOneToSixteen()
    {
        PhcPasswordHasherOptions.MIN_ACCEPTED_PARALLELISM.Should().Be(1);
        PhcPasswordHasherOptions.MAX_ACCEPTED_PARALLELISM.Should().Be(16);
    }

    [Fact]
    public void AcceptedSaltLengthBounds_AreEightToSixtyFourBytes()
    {
        PhcPasswordHasherOptions.MIN_ACCEPTED_SALT_LENGTH.Should().Be(8);
        PhcPasswordHasherOptions.MAX_ACCEPTED_SALT_LENGTH.Should().Be(64);
    }

    [Fact]
    public void AcceptedHashLengthBounds_AreSixteenToSixtyFourBytes()
    {
        PhcPasswordHasherOptions.MIN_ACCEPTED_HASH_LENGTH.Should().Be(16);
        PhcPasswordHasherOptions.MAX_ACCEPTED_HASH_LENGTH.Should().Be(64);
    }

    [Fact]
    public void AcceptedMemoryPerLane_IsEightKibibytes()
    {
        PhcPasswordHasherOptions.MIN_ACCEPTED_MEMORY_KIB_PER_LANE.Should().Be(8);
    }

    [Fact]
    public void PropertyDefaults_MatchTheDefaultConstants()
    {
        var options = new PhcPasswordHasherOptions();

        options.MemoryKib.Should().Be(PhcPasswordHasherOptions.DEFAULT_MEMORY_KIB);
        options.Iterations.Should().Be(PhcPasswordHasherOptions.DEFAULT_ITERATIONS);
        options.Parallelism.Should().Be(PhcPasswordHasherOptions.DEFAULT_PARALLELISM);
        options.SaltLength.Should().Be(PhcPasswordHasherOptions.DEFAULT_SALT_LENGTH);
        options.HashLength.Should().Be(PhcPasswordHasherOptions.DEFAULT_HASH_LENGTH);
    }
}
