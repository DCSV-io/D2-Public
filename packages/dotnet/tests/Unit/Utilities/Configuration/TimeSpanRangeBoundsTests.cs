// -----------------------------------------------------------------------
// <copyright file="TimeSpanRangeBoundsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Utilities.Configuration;

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using AwesomeAssertions;
using DcsvIo.D2.Tests.Unit.Utilities.Configuration.Fixtures;
using DcsvIo.D2.Utilities.Configuration;
using Xunit;

public sealed class TimeSpanRangeBoundsTests
{
    [Fact]
    public void NoCeiling_ParsesToTimeSpanMaxValue()
    {
        // NO_CEILING must parse to TimeSpan.MaxValue. A typo in it lowers the upper
        // bound of every floor-only range that uses it, in every assembly, and raises
        // no error.
        var parsed = TimeSpan.Parse(TimeSpanRangeBounds.NO_CEILING, CultureInfo.InvariantCulture);

        parsed.Should().Be(TimeSpan.MaxValue);
    }

    [Fact]
    public void NoCeiling_HasExpectedLiteralValue()
    {
        TimeSpanRangeBounds.NO_CEILING.Should().Be("10675199.02:48:05.4775807");
    }

    [Fact]
    public void FloorOnlyRange_AdmitsTimeSpanMaxValue()
    {
        var results = Validate(TimeSpan.MaxValue);

        results.Should().BeEmpty();
    }

    [Fact]
    public void FloorOnlyRange_RejectsValueBelowTheFloor()
    {
        var results = Validate(TimeSpan.Zero);

        results.Should().ContainSingle()
            .Which.MemberNames.Should().Contain(nameof(FloorOnlyTimeSpanRangeFixture.Interval));
    }

    private static List<ValidationResult> Validate(TimeSpan interval)
    {
        var subject = new FloorOnlyTimeSpanRangeFixture { Interval = interval };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(
            subject,
            new ValidationContext(subject),
            results,
            validateAllProperties: true);

        return results;
    }
}
