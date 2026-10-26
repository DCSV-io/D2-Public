// -----------------------------------------------------------------------
// <copyright file="SearchPolicy.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination;

/// <summary>
/// Query-length bounds for a searchable list surface.
/// </summary>
/// <param name="MinQueryLength">
/// Shortest run of letters and digits this surface accepts, measured on the
/// normalized text. A trigram index is reached through the letters and digits
/// a query contains, so this bounds those rather than the character count.
/// </param>
/// <param name="MaxQueryLength">
/// Longest query this surface accepts, measured on the normalized text.
/// </param>
public sealed record SearchPolicy(
    int MinQueryLength = SearchPolicy.DEFAULT_MIN_QUERY_LENGTH,
    int MaxQueryLength = SearchPolicy.DEFAULT_MAX_QUERY_LENGTH)
{
    /// <summary>
    /// A trigram index is reached through a run of three letters or digits;
    /// anything shorter falls back to a scan over every reachable row.
    /// </summary>
    public const int DEFAULT_MIN_QUERY_LENGTH = 3;

    /// <summary>Bounds planner and ranking cost per request.</summary>
    public const int DEFAULT_MAX_QUERY_LENGTH = 128;
}
