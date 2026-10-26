// -----------------------------------------------------------------------
// <copyright file="FloorOnlyTimeSpanRangeFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Utilities.Configuration.Fixtures;

using System.ComponentModel.DataAnnotations;
using DcsvIo.D2.Utilities.Configuration;

/// <summary>
/// A type with a floor-only range annotation: a lower bound of one second and
/// <see cref="TimeSpanRangeBounds.NO_CEILING"/> as the upper bound. A test runs the
/// annotation through <see cref="Validator"/> to see which values the range admits
/// and which it rejects.
/// </summary>
internal sealed class FloorOnlyTimeSpanRangeFixture
{
    /// <summary>Gets or sets the annotated value under test.</summary>
    [Range(typeof(TimeSpan), "00:00:01", TimeSpanRangeBounds.NO_CEILING)]
    public TimeSpan Interval { get; set; }
}
