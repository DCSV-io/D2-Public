// -----------------------------------------------------------------------
// <copyright file="EmailValidatorPreWarmTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Validation;

using System;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DcsvIo.D2.Validation;
using Xunit;

/// <summary>
/// Pins that the regex pre-warm swallows a match timeout. The pre-warm runs inside the
/// static constructor, so an escaping exception fails the type initializer and every
/// later use of the validator throws for the life of the process.
/// </summary>
public sealed class EmailValidatorPreWarmTests
{
    // Nested quantifiers over a subject with no match - the classic catastrophic
    // backtracking shape - paired with a timeout no machine can meet, so the match
    // times out on every run rather than on a loaded one.
    private const string _CATASTROPHIC_PATTERN = "^(a+)+$";

    private static readonly TimeSpan sr_unmeetableTimeout = TimeSpan.FromTicks(1);

    [Fact]
    public void PreWarm_WhenTheMatchTimesOut_DoesNotThrow()
    {
        var willTimeOut = new Regex(
            _CATASTROPHIC_PATTERN, RegexOptions.None, sr_unmeetableTimeout);

        var preWarm = () => DefaultEmailValidator.PreWarm(willTimeOut);

        preWarm.Should().NotThrow();
    }

    [Fact]
    public void PreWarm_WithNoRegex_Throws()
    {
        var preWarm = () => DefaultEmailValidator.PreWarm(null!);

        preWarm.Should().Throw<ArgumentNullException>();
    }
}
