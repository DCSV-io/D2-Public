// -----------------------------------------------------------------------
// <copyright file="AdvisoryLocksDiagnosticIdsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AdvisoryLocks.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.AdvisoryLocks.SourceGen;
using Xunit;

/// <summary>
/// Pins the <c>D2LCK</c> diagnostic-ID string values and the prefix convention.
/// A renamed constant causes a compiler error, and a changed value fails these
/// tests, so either change is caught before it ships.
/// </summary>
public sealed class AdvisoryLocksDiagnosticIdsTests
{
    [Theory]
    [InlineData(DiagnosticIds.MalformedSpec, "D2LCK001")]
    [InlineData(DiagnosticIds.DuplicateConstNameInDatabase, "D2LCK002")]
    [InlineData(DiagnosticIds.DuplicateKeyInDatabase, "D2LCK003")]
    [InlineData(DiagnosticIds.InvalidConstName, "D2LCK004")]
    [InlineData(DiagnosticIds.KeyOutOfRange, "D2LCK005")]
    [InlineData(DiagnosticIds.IncompleteEmitConfig, "D2LCK006")]
    [InlineData(DiagnosticIds.UnknownDatabaseFilter, "D2LCK007")]
    public void DiagnosticId_HasExpectedValue(string actual, string expected)
    {
        actual.Should().Be(expected);
    }

    [Fact]
    public void DiagnosticIds_PinD2LCK001Through007_Including006And007()
    {
        DiagnosticIds.MalformedSpec.Should().Be("D2LCK001");
        DiagnosticIds.DuplicateConstNameInDatabase.Should().Be("D2LCK002");
        DiagnosticIds.DuplicateKeyInDatabase.Should().Be("D2LCK003");
        DiagnosticIds.InvalidConstName.Should().Be("D2LCK004");
        DiagnosticIds.KeyOutOfRange.Should().Be("D2LCK005");
        DiagnosticIds.IncompleteEmitConfig.Should().Be("D2LCK006");
        DiagnosticIds.UnknownDatabaseFilter.Should().Be("D2LCK007");
    }

    [Theory]
    [InlineData(DiagnosticIds.MalformedSpec)]
    [InlineData(DiagnosticIds.DuplicateConstNameInDatabase)]
    [InlineData(DiagnosticIds.DuplicateKeyInDatabase)]
    [InlineData(DiagnosticIds.InvalidConstName)]
    [InlineData(DiagnosticIds.KeyOutOfRange)]
    [InlineData(DiagnosticIds.IncompleteEmitConfig)]
    [InlineData(DiagnosticIds.UnknownDatabaseFilter)]
    public void DiagnosticId_StartsWithD2LCKPrefix(string id)
    {
        id.Should().StartWith("D2LCK", "all advisory-locks diagnostic IDs use the D2LCK prefix");
    }

    [Theory]
    [InlineData(DiagnosticIds.MalformedSpec)]
    [InlineData(DiagnosticIds.DuplicateConstNameInDatabase)]
    [InlineData(DiagnosticIds.DuplicateKeyInDatabase)]
    [InlineData(DiagnosticIds.InvalidConstName)]
    [InlineData(DiagnosticIds.KeyOutOfRange)]
    [InlineData(DiagnosticIds.IncompleteEmitConfig)]
    [InlineData(DiagnosticIds.UnknownDatabaseFilter)]
    public void DiagnosticId_HasThreeDigitNumericSuffix(string id)
    {
        var suffix = id[5..]; // after "D2LCK"
        suffix.Should().HaveLength(3);
        int.TryParse(suffix, out _).Should().BeTrue(
            "ID suffix must be a three-digit numeric string");
    }
}
