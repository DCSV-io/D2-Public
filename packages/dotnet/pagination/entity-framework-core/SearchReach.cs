// -----------------------------------------------------------------------
// <copyright file="SearchReach.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination.EntityFrameworkCore;

using System.Linq.Expressions;

/// <summary>
/// The rows a search may reach. Constructing one bounds the search; there is
/// no implicit every-row case, and the unrestricted case is a named factory
/// that a search of the code can find.
/// </summary>
/// <remarks>
/// Named for reach rather than scope: in this repository a scope is an access
/// grant, and this is a row-visibility predicate.
/// </remarks>
/// <typeparam name="TRow">EF-clean page-row type.</typeparam>
public sealed class SearchReach<TRow>
{
    private static readonly SearchReach<TRow> sr_unrestricted = new(null);

    private readonly Expression<Func<TRow, bool>>? r_predicate;

    private SearchReach(Expression<Func<TRow, bool>>? predicate) => r_predicate = predicate;

    /// <summary>
    /// Bounds the search to the rows satisfying <paramref name="predicate"/>.
    /// </summary>
    /// <param name="predicate">The row-visibility predicate.</param>
    /// <returns>The bounded reach.</returns>
    public static SearchReach<TRow> Restrict(Expression<Func<TRow, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new SearchReach<TRow>(predicate);
    }

    /// <summary>
    /// No restriction. Each use is a call to this named factory, and each use
    /// must be justified at its call site.
    /// </summary>
    /// <returns>The unrestricted reach.</returns>
    public static SearchReach<TRow> Unrestricted() => sr_unrestricted;

    /// <summary>
    /// Applies this reach to a query.
    /// </summary>
    /// <param name="query">The query to bound.</param>
    /// <returns>The bounded query.</returns>
    public IQueryable<TRow> Apply(IQueryable<TRow> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return r_predicate is null ? query : query.Where(r_predicate);
    }
}
