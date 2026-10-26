// -----------------------------------------------------------------------
// <copyright file="PostalCodeValidatorPreWarmTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Location;

using System;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DcsvIo.D2.Location;
using Xunit;

/// <summary>
/// Pins that the regex pre-warm swallows a match timeout. The pre-warm runs inside the
/// static constructor, so an escaping exception fails the type initializer and every
/// later use of the validator throws for the life of the process.
/// </summary>
public sealed class PostalCodeValidatorPreWarmTests
{
    // Nested quantifiers over a subject with no match backtrack catastrophically.
    // The pattern is paired with a timeout that no machine can meet, so the match
    // times out on every run and not only on a loaded machine.
    private const string _CATASTROPHIC_PATTERN = "^(a+)+$";

    private static readonly TimeSpan sr_unmeetableTimeout = TimeSpan.FromTicks(1);

    [Fact]
    public void PreWarm_WhenTheMatchTimesOut_DoesNotThrow()
    {
        var willTimeOut = new Regex(
            _CATASTROPHIC_PATTERN, RegexOptions.None, sr_unmeetableTimeout);

        var preWarm = () => DefaultPostalCodeValidator.PreWarm(willTimeOut);

        preWarm.Should().NotThrow();
    }

    [Fact]
    public void PreWarm_WithNoRegex_Throws()
    {
        var preWarm = () => DefaultPostalCodeValidator.PreWarm(null!);

        preWarm.Should().Throw<ArgumentNullException>();
    }
}
