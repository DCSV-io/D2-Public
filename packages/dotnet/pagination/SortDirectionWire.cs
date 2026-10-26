// -----------------------------------------------------------------------
// <copyright file="SortDirectionWire.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination;

/// <summary>
/// Single source of truth for sort-order wire tokens (<c>asc</c> / <c>desc</c>).
/// Use these constants for the tokens, never bare string literals.
/// </summary>
public static class SortDirectionWire
{
    /// <summary>Wire token for ascending order.</summary>
    public const string ASC = "asc";

    /// <summary>Wire token for descending order.</summary>
    public const string DESC = "desc";

    /// <summary>
    /// Parses a wire order token (case-insensitive) into <see cref="SortDirection"/>.
    /// </summary>
    /// <param name="wire">Client order string.</param>
    /// <param name="direction">Parsed direction when true.</param>
    /// <returns>
    /// <see langword="true"/> when the wire is <see cref="ASC"/> or
    /// <see cref="DESC"/>.
    /// </returns>
    public static bool TryParse(string? wire, out SortDirection direction)
    {
        if (string.Equals(wire, ASC, StringComparison.OrdinalIgnoreCase))
        {
            direction = SortDirection.Asc;
            return true;
        }

        if (string.Equals(wire, DESC, StringComparison.OrdinalIgnoreCase))
        {
            direction = SortDirection.Desc;
            return true;
        }

        direction = default;
        return false;
    }
}
