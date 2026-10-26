// -----------------------------------------------------------------------
// <copyright file="SearchPageEf.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination.EntityFrameworkCore;

using DcsvIo.D2.Pagination;
using DcsvIo.D2.Pagination.Errors;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core list-search fluent API. This is the provider-neutral half: it fixes
/// the order of operations, and a provider package supplies the expressions
/// through <see cref="ISearchDialect{TRow}"/>.
/// </summary>
public static class SearchPageEf
{
    /// <summary>
    /// List-search extension on an EF-projected page-row query.
    /// </summary>
    /// <typeparam name="TRow">EF-clean page-row type (generated PageRow).</typeparam>
    /// <param name="query">
    /// Base query already projected to <typeparamref name="TRow"/>. The reach
    /// predicate is applied to it before anything else.
    /// </param>
    extension<TRow>(IQueryable<TRow> query)
    {
        /// <summary>
        /// Materializes one search page.
        /// </summary>
        /// <remarks>
        /// The reach predicate is applied first, then the dialect's filter, and
        /// the total is counted over that set. A request sorted by relevance
        /// then pages a projection carrying the materialized score; every other
        /// applied field pages the query directly, which is what a request
        /// carrying no query also does.
        /// </remarks>
        /// <param name="request">Wire page request (cursor, limit, sort, order, query).</param>
        /// <param name="descriptor">The operation's emitted searchable descriptor.</param>
        /// <param name="reach">The rows this search may reach.</param>
        /// <param name="dialect">The provider's filtering and ranking expressions.</param>
        /// <param name="codec">
        /// Cursor codec; <see langword="null"/> uses
        /// <see cref="ListPageEf.DefaultCodec"/>.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The page, or a pagination failure.</returns>
        public async ValueTask<D2Result<PageResult<TRow>?>> ToSearchPageAsync(
            PageRequest request,
            ISearchableListPageDescriptor<TRow> descriptor,
            SearchReach<TRow> reach,
            ISearchDialect<TRow> dialect,
            IKeysetCursorCodec? codec = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(descriptor);
            ArgumentNullException.ThrowIfNull(reach);
            ArgumentNullException.ThrowIfNull(dialect);

            var effectiveCodec = codec ?? ListPageEf.DefaultCodec;
            var catalog = descriptor.SortCatalog;
            var declared = descriptor.AllowedSortFields;
            var narrowed = declared.IntersectWith(catalog.WireFields);

            if (narrowed.Fields.Falsey())
                return PaginationFailures<PageResult<TRow>?>.SortCatalogEmpty();

            var effectiveAllow = ReadmitRelevance(declared, narrowed);

            var norm = PageRequestNormalizer.Normalize(
                request,
                descriptor.Policy,
                effectiveAllow,
                effectiveCodec,
                descriptor.SearchPolicy);

            if (norm.Failed || norm.Data is null)
                return D2Result<PageResult<TRow>?>.BubbleFail(norm);

            var normalized = norm.Data;
            var reached = reach.Apply(query);

            if (normalized.Search is not { } search)
            {
                return await ListPageMaterializer.MaterializeAsync(
                    reached,
                    normalized,
                    catalog,
                    effectiveCodec,
                    static row => row,
                    ct);
            }

            var matched = reached.Where(dialect.Filter(descriptor.SearchCatalog.Blob, search));

            if (!string.Equals(
                normalized.AppliedSort.Field,
                SortFieldNames.RELEVANCE,
                StringComparison.Ordinal))
            {
                return await ListPageMaterializer.MaterializeAsync(
                    matched,
                    normalized,
                    catalog,
                    effectiveCodec,
                    static row => row,
                    ct);
            }

            // The count is taken before the projection, so a projection in the
            // pipeline cannot move it.
            var totalCount = await matched.LongCountAsync(ct);
            var scored = matched.Select(descriptor.SearchCatalog.Project(dialect, search));

            return await ListPageMaterializer.DrawAsync(
                scored,
                totalCount,
                normalized,
                descriptor.SearchCatalog.BuildRelevanceEntry(),
                static s => s.Row,
                effectiveCodec,
                ct);
        }
    }

    /// <summary>
    /// Puts the relevance field back into a narrowed allow-set.
    /// </summary>
    /// <remarks>
    /// Relevance is never registered in the sort catalog - its ordering
    /// expression depends on the query and cannot exist at type initialization -
    /// so the intersection drops it. The re-admitted specification is the
    /// emitted one, which carries the descending-only lock; rebuilding it here
    /// would accept an ascending relevance request and order the page worst
    /// match first.
    /// </remarks>
    private static SortFieldAllowSet ReadmitRelevance(
        SortFieldAllowSet declared,
        SortFieldAllowSet narrowed)
        => declared.TryGet(SortFieldNames.RELEVANCE, out var relevance)
            ? narrowed.With(relevance)
            : narrowed;
}
