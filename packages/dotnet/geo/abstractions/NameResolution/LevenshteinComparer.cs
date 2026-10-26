// -----------------------------------------------------------------------
// <copyright file="LevenshteinComparer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.Abstractions.NameResolution;

/// <summary>
/// Pure, stateless and thread-safe helper that computes the bounded
/// Levenshtein edit distance between two strings, to score candidate
/// matches in a fuzzy name lookup.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cross-language parity:</b> the TypeScript mirror in
/// <c>@dcsv-io/d2-geo-abstractions</c>
/// (<c>src/name-resolution/levenshtein-comparer.ts</c>) implements the
/// same DP algorithm with the same early-termination semantics. The two
/// implementations MUST agree byte-for-byte on <see cref="IsWithin"/>
/// and <see cref="Compare"/> for every input pair the parity fixtures
/// exercise.
/// </para>
/// <para>
/// <b>Algorithm:</b> classic two-row Wagner-Fischer dynamic-programming
/// edit distance, where insert, delete and substitute each cost 1. Memory
/// is O(min(|a|, |b|)) because the inner loop runs over the shorter string.
/// Running time is O(|a| x |b|) in the worst case. The <c>maxDistance</c>
/// early-termination cap typically drops it to near-linear when the bound
/// is small (&lt;= 3).
/// </para>
/// <para>
/// <b>Boundary handling:</b>
/// </para>
/// <list type="bullet">
///   <item>
///     <description><c>null</c> on either side is treated as the empty
///     string - the helper never throws on null input.</description>
///   </item>
///   <item>
///     <description>A negative <c>maxDistance</c> is treated as 0 by
///     <see cref="Compare"/>, which then returns 0 for equal strings and 1
///     otherwise, and <see cref="IsWithin"/> returns <c>false</c>. Pass
///     non-negative bounds.</description>
///   </item>
///   <item>
///     <description>Length-difference shortcut: if
///     <c>|len(a) - len(b)| &gt; maxDistance</c> the distance cannot be
///     within the cap, so the method returns early without allocating the
///     DP buffers.</description>
///   </item>
/// </list>
/// </remarks>
public static class LevenshteinComparer
{
    /// <summary>
    /// Returns <c>true</c> when the Levenshtein edit distance between
    /// <paramref name="a"/> and <paramref name="b"/> is at most
    /// <paramref name="maxDistance"/>. Convenience wrapper over
    /// <see cref="Compare"/>.
    /// </summary>
    /// <param name="a">The first string. May be <c>null</c>.</param>
    /// <param name="b">The second string. May be <c>null</c>.</param>
    /// <param name="maxDistance">
    /// Inclusive distance cap. Must be non-negative; a negative value
    /// always returns <c>false</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> when the bounded distance is within the cap;
    /// <c>false</c> otherwise, including when the early-termination cap is
    /// exceeded.
    /// </returns>
    public static bool IsWithin(string? a, string? b, int maxDistance)
    {
        if (maxDistance < 0)
            return false;

        return Compare(a, b, maxDistance) <= maxDistance;
    }

    /// <summary>
    /// Returns the Levenshtein edit distance between <paramref name="a"/>
    /// and <paramref name="b"/>, capped at <paramref name="maxDistance"/>
    /// + 1 through early termination. The capped return value is a
    /// sentinel: callers MUST interpret any value greater than
    /// <paramref name="maxDistance"/> as "exceeds cap" and not as a true
    /// distance.
    /// </summary>
    /// <param name="a">The first string. May be <c>null</c>.</param>
    /// <param name="b">The second string. May be <c>null</c>.</param>
    /// <param name="maxDistance">
    /// Inclusive distance cap that enables early termination. Must be
    /// non-negative; a negative value is treated as 0.
    /// </param>
    /// <returns>
    /// The edit distance when at most <paramref name="maxDistance"/>;
    /// otherwise <c>maxDistance + 1</c>.
    /// </returns>
    public static int Compare(string? a, string? b, int maxDistance)
    {
        var cap = maxDistance < 0 ? 0 : maxDistance;
        var ceiling = cap + 1;

        // Treat null as empty and order the strings so that second is the
        // shorter one; the DP rows are sized to it for O(min) memory.
        var first = a ?? string.Empty;
        var second = b ?? string.Empty;
        if (first.Length < second.Length)
            (first, second) = (second, first);

        // Length-difference shortcut - the length difference is a lower
        // bound on the distance, so the distance cannot fit inside the cap.
        if (first.Length - second.Length > cap)
            return ceiling;

        // Trivial case - second is empty, so the distance equals
        // first.Length.
        if (second.Length == 0)
            return first.Length > cap ? ceiling : first.Length;

        // Two-row DP - previous row and current row over second (the
        // shorter string). The previous row starts as the empty-prefix
        // distances 0..second.Length.
        var previous = new int[second.Length + 1];
        var current = new int[second.Length + 1];
        for (var j = 0; j <= second.Length; j++)
            previous[j] = j;

        for (var i = 1; i <= first.Length; i++)
        {
            current[0] = i;
            var rowMin = current[0];
            var firstChar = first[i - 1];

            for (var j = 1; j <= second.Length; j++)
            {
                var cost = firstChar == second[j - 1] ? 0 : 1;
                var deletion = previous[j] + 1;
                var insertion = current[j - 1] + 1;
                var substitution = previous[j - 1] + cost;

                var cell = deletion;
                if (insertion < cell)
                    cell = insertion;
                if (substitution < cell)
                    cell = substitution;

                current[j] = cell;
                if (cell < rowMin)
                    rowMin = cell;
            }

            // Early termination - every cell in this row exceeds the cap,
            // and the distance cannot drop back below it in later rows.
            if (rowMin > cap)
                return ceiling;

            // Swap rows - `current` becomes `previous` for the next i.
            (previous, current) = (current, previous);
        }

        var distance = previous[second.Length];
        return distance > cap ? ceiling : distance;
    }
}
