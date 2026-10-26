// -----------------------------------------------------------------------
// <copyright file="IGeoNameResolver.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.Abstractions.NameResolution;

using DcsvIo.D2.Result;

/// <summary>
/// Resolves a free-form place-name string from a 3rd-party source (a WhoIs
/// response, IP-geolocation enrichment, a vendor API or user input) to the
/// matching catalog record. It belongs at the integration boundary, where
/// free-form input becomes a typed value.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fail-closed semantics.</b> Implementations MUST be fail-closed. When
/// the input is null, empty, whitespace, ambiguous or too short for a fuzzy
/// pass, or when several entities match at the same score within a pass,
/// the resolver returns <see cref="D2Result.NotFound"/> and does not guess.
/// An unresolved outcome is safe because the raw upstream string remains
/// available for the audit trail. A wrong fuzzy match is not safe.
/// </para>
/// <para>
/// <b>Cascade.</b> The resolution algorithm is a cascade of the passes
/// below, each with a minimum input length:
/// </para>
/// <list type="number">
///   <item>
///     <description>Pass 1 (exact) - no minimum input length. Always runs.</description>
///   </item>
///   <item>
///     <description>Pass 2 (startsWith) - skipped if <c>q.length &lt; 4</c>.
///     A short prefix matches too many names.</description>
///   </item>
///   <item>
///     <description>Pass 3 (contains) - skipped if <c>q.length &lt; 5</c>.
///     A short substring matches too many names.</description>
///   </item>
///   <item>
///     <description>Pass 4 (Levenshtein) - skipped if <c>q.length &lt; 5</c>;
///     within the pass, <c>maxDistance &lt;= min(2, floor(q.length / 5))</c>.
///     Fuzzy matching on a short input merges distinct entities.</description>
///   </item>
/// </list>
/// <para>
/// The first matching pass wins, and later passes do not run. Ambiguity
/// within a pass (several matches at the same score) returns
/// <see cref="D2Result.NotFound"/>. The code generator enforces catalog
/// uniqueness with <c>D2GEO010</c>, so taking the first match gives a
/// deterministic result.
/// </para>
/// </remarks>
public interface IGeoNameResolver
{
    /// <summary>
    /// Resolves a free-form country-name string to a typed
    /// <see cref="Country"/> identifier through the fail-closed cascade.
    /// </summary>
    /// <param name="name">
    /// The free-form country name, which may be null, empty or whitespace.
    /// Implementations MUST validate the input before any lookup and return
    /// <c>D2Result.ValidationFailed</c> for null, empty or whitespace, or
    /// <c>D2Result.NotFound</c> at the implementation's discretion. Either
    /// way the result is never a wrong country for bad input.
    /// </param>
    /// <returns>
    /// <c>D2Result.Ok(country)</c> on an unambiguous match; <c>NotFound</c>
    /// on ambiguity, on an input too short for any pass to match, or when no
    /// pass matches; <c>ValidationFailed</c> on null, empty or whitespace
    /// input.
    /// </returns>
    D2Result<Country> TryResolveCountryByName(string name);

    /// <summary>
    /// Resolves a free-form subdivision-name string to the catalog's
    /// <see cref="Subdivision"/> record through the fail-closed cascade,
    /// scoped to <paramref name="parentCountry"/>. The parent country is
    /// required: "Georgia" is both a country (GE) and a US state (US-GA),
    /// and the resolver MUST NOT pick one silently.
    /// </summary>
    /// <param name="name">The free-form subdivision name.</param>
    /// <param name="parentCountry">The country whose subdivisions to search.</param>
    /// <returns>
    /// <c>D2Result.Ok(subdivision)</c> on an unambiguous match;
    /// <c>NotFound</c> on ambiguity, on too-short input, when that
    /// country's catalog has no match, or when the catalog has a coverage
    /// gap; <c>ValidationFailed</c> on null, empty or whitespace input.
    /// </returns>
    D2Result<Subdivision> TryResolveSubdivisionByName(
        string name, Country parentCountry);
}
