// -----------------------------------------------------------------------
// <copyright file="ISearchDialect.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination.EntityFrameworkCore;

using System.Linq.Expressions;
using DcsvIo.D2.Pagination;

/// <summary>
/// How one database provider expresses filtering and ranking. This package
/// fixes the order of operations; a provider package supplies the expressions.
/// </summary>
/// <remarks>
/// A dialect built on pattern matching escapes the pattern metacharacters and
/// the escape character itself when it builds its expression, because
/// <see cref="SearchQuery.Normalized"/> is the caller's literal text and
/// carries no pattern syntax.
/// </remarks>
/// <typeparam name="TRow">EF-clean page-row type.</typeparam>
public interface ISearchDialect<TRow>
{
    /// <summary>
    /// Builds the predicate selecting rows whose blob matches the query.
    /// </summary>
    /// <param name="blob">The concatenated column the filter runs against.</param>
    /// <param name="query">The validated query.</param>
    /// <returns>The match predicate.</returns>
    Expression<Func<TRow, bool>> Filter(
        Expression<Func<TRow, string>> blob,
        SearchQuery query);

    /// <summary>
    /// Builds the weighted relevance score for a row, higher being a better
    /// match.
    /// </summary>
    /// <param name="rankFields">The fields contributing to relevance, with their weights.</param>
    /// <param name="query">The validated query.</param>
    /// <returns>The score expression.</returns>
    Expression<Func<TRow, double>> Score(
        IReadOnlyList<SearchRankField<TRow>> rankFields,
        SearchQuery query);
}
