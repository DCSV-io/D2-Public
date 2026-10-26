// -----------------------------------------------------------------------
// <copyright file="SearchCatalogBuilder.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination.EntityFrameworkCore;

using System.Linq.Expressions;

/// <summary>
/// Builds a <see cref="SearchCatalog{TRow}"/>. Registration happens only here.
/// </summary>
/// <typeparam name="TRow">EF-clean page-row type.</typeparam>
public sealed class SearchCatalogBuilder<TRow>
{
    private readonly List<SearchRankField<TRow>> r_rankFields = [];

    private Expression<Func<TRow, string>>? _blob;

    private Expression<Func<TRow, Guid>>? _tieBreak;

    /// <summary>
    /// Registers the concatenated column the filter runs against.
    /// </summary>
    /// <param name="blob">The blob column expression.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public SearchCatalogBuilder<TRow> Blob(Expression<Func<TRow, string>> blob)
    {
        ArgumentNullException.ThrowIfNull(blob);
        _blob = blob;
        return this;
    }

    /// <summary>
    /// Registers a field contributing to relevance, with its weight.
    /// </summary>
    /// <param name="field">The text column expression.</param>
    /// <param name="weight">Relative weight; a higher weight outranks a lower one.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public SearchCatalogBuilder<TRow> RankField(
        Expression<Func<TRow, string>> field,
        double weight)
    {
        ArgumentNullException.ThrowIfNull(field);
        r_rankFields.Add(new SearchRankField<TRow>(field, weight));
        return this;
    }

    /// <summary>
    /// Registers the stable tie-break for rows of equal score.
    /// </summary>
    /// <param name="tieBreak">The tie-break expression.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public SearchCatalogBuilder<TRow> TieBreak(Expression<Func<TRow, Guid>> tieBreak)
    {
        ArgumentNullException.ThrowIfNull(tieBreak);
        _tieBreak = tieBreak;
        return this;
    }

    /// <summary>
    /// Produces the immutable catalog.
    /// </summary>
    /// <returns>The catalog.</returns>
    /// <exception cref="InvalidOperationException">
    /// No blob or no tie-break was registered. A search with no column to match
    /// against, or no stable order for equal scores, is a composition-time
    /// defect in generated or host code rather than caller traffic, so it fails
    /// where the mistake was made.
    /// </exception>
    public SearchCatalog<TRow> Build()
    {
        if (_blob is null)
        {
            throw new InvalidOperationException(
                "A search catalog needs a blob column; call Blob before Build.");
        }

        if (_tieBreak is null)
        {
            throw new InvalidOperationException(
                "A search catalog needs a tie-break; call TieBreak before Build.");
        }

        return new SearchCatalog<TRow>(_blob, r_rankFields.ToArray(), _tieBreak);
    }
}
