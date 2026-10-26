// -----------------------------------------------------------------------
// <copyright file="TimeSpanRangeBounds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Utilities.Configuration;

/// <summary>
/// Named bound strings for
/// <see cref="System.ComponentModel.DataAnnotations.RangeAttribute"/> ranges
/// over <see cref="TimeSpan"/>.
/// </summary>
public static class TimeSpanRangeBounds
{
    /// <summary>
    /// The upper bound to use when a <see cref="TimeSpan"/> range has only a
    /// floor. The value is <see cref="TimeSpan.MaxValue"/> written out:
    /// <see cref="System.ComponentModel.DataAnnotations.RangeAttribute"/>
    /// parses both bounds from compile-time strings and offers no minimum-only
    /// form, so a property whose real constraint is a minimum still has to state
    /// a maximum. Referencing this constant marks that maximum as "no ceiling"
    /// and not as a chosen cap.
    /// </summary>
    public const string NO_CEILING = "10675199.02:48:05.4775807";
}
