// -----------------------------------------------------------------------
// <copyright file="ListPageEf.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination.EntityFrameworkCore;

using DcsvIo.D2.Pagination;
using DcsvIo.D2.Pagination.Errors;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// EF Core multi-mode list-page fluent API. The main entry point is
/// <c>ToListPageAsync</c> on <see cref="IQueryable{T}"/>.
/// </summary>
public static class ListPageEf
{
    /// <summary>
    /// Multi-mode list-page extension on an EF-projected page-row query.
    /// </summary>
    /// <typeparam name="TRow">EF-clean page-row type (generated PageRow).</typeparam>
    /// <param name="query">
    /// Base-filtered query already projected to <typeparamref name="TRow"/>.
    /// </param>
    extension<TRow>(IQueryable<TRow> query)
    {
        /// <summary>
        /// Materializes one multi-mode list page: normalize sort, limit and
        /// cursor, count the base filter for the total, apply the cursor
        /// predicate and the catalog's ordering, take one row beyond the limit,
        /// and encode the next cursor.
        /// </summary>
        /// <param name="request">Wire page request (cursor, limit, sort, order).</param>
        /// <param name="policy">Generated page policy (default sort + limits).</param>
        /// <param name="catalog">Generated sort catalog for the page row.</param>
        /// <param name="allowedSortFields">
        /// Generated allow-set listing each field's permitted directions.
        /// </param>
        /// <param name="codec">
        /// Cursor codec; <see langword="null"/> uses <see cref="DefaultCodec"/>.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The page, or a pagination failure.</returns>
        public async ValueTask<D2Result<PageResult<TRow>?>> ToListPageAsync(
            PageRequest request,
            ListPagingPolicy policy,
            SortCatalog<TRow> catalog,
            SortFieldAllowSet allowedSortFields,
            IKeysetCursorCodec? codec = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(policy);
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(allowedSortFields);

            var effectiveCodec = codec ?? DefaultCodec;
            var effectiveAllow = allowedSortFields.IntersectWith(catalog.WireFields);

            if (effectiveAllow.Fields.Falsey())
                return PaginationFailures<PageResult<TRow>?>.SortCatalogEmpty();

            var norm = PageRequestNormalizer.Normalize(
                request,
                policy,
                effectiveAllow,
                effectiveCodec);

            if (norm.Failed || norm.Data is null)
                return D2Result<PageResult<TRow>?>.BubbleFail(norm);

            return await ListPageMaterializer.MaterializeAsync(
                query,
                norm.Data,
                catalog,
                effectiveCodec,
                static row => row,
                ct);
        }
    }

    /// <summary>
    /// Multi-mode list-page extension over several page-row queries that admit
    /// no row in common.
    /// </summary>
    /// <typeparam name="TRow">EF-clean page-row type (generated PageRow).</typeparam>
    /// <param name="arms">
    /// The disjoint queries the page is drawn from, each already projected to
    /// <typeparamref name="TRow"/>.
    /// </param>
    extension<TRow>(IReadOnlyList<IQueryable<TRow>> arms)
    {
        /// <summary>
        /// Materializes one multi-mode list page over disjoint arms: the cursor
        /// predicate, the ordering and the limit are applied to each arm and
        /// again to their concatenation, and the total counts the arms
        /// unlimited.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A predicate that admits a row on either of several columns is one
        /// such set of arms. Asked as one query over an OR of the arms' predicates,
        /// a relational database reads every matching row and sorts it to draw
        /// each page, so a page costs the whole set; asked as arms, it walks an
        /// ordered index per arm and stops at the page.
        /// </para>
        /// <para>
        /// The arms must admit no row in common: concatenation keeps
        /// duplicates, so a row two arms admit is counted twice and returned
        /// twice. Nothing here can check it; only the caller that built the arms
        /// knows their predicates.
        /// </para>
        /// </remarks>
        /// <param name="request">Wire page request (cursor, limit, sort, order).</param>
        /// <param name="policy">Generated page policy (default sort + limits).</param>
        /// <param name="catalog">Generated sort catalog for the page row.</param>
        /// <param name="allowedSortFields">
        /// Generated allow-set listing each field's permitted directions.
        /// </param>
        /// <param name="codec">
        /// Cursor codec; <see langword="null"/> uses <see cref="DefaultCodec"/>.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The page, or a pagination failure.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the arms, the request, the policy, the catalog or the
        /// allow-set is null, or when the arms hold a null arm.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when the arms are empty.</exception>
        public async ValueTask<D2Result<PageResult<TRow>?>> ToListPageOverArmsAsync(
            PageRequest request,
            ListPagingPolicy policy,
            SortCatalog<TRow> catalog,
            SortFieldAllowSet allowedSortFields,
            IKeysetCursorCodec? codec = null,
            CancellationToken ct = default)
        {
            // The guard runs before the request is normalized, so an invalid
            // argument throws and is not returned as a pagination failure about
            // the request.
            GuardArms(arms);
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(policy);
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(allowedSortFields);

            var effectiveCodec = codec ?? DefaultCodec;
            var effectiveAllow = allowedSortFields.IntersectWith(catalog.WireFields);

            if (effectiveAllow.Fields.Falsey())
                return PaginationFailures<PageResult<TRow>?>.SortCatalogEmpty();

            var norm = PageRequestNormalizer.Normalize(
                request,
                policy,
                effectiveAllow,
                effectiveCodec);

            if (norm.Failed || norm.Data is null)
                return D2Result<PageResult<TRow>?>.BubbleFail(norm);

            return await ListPageMaterializer.MaterializeArmsAsync(
                arms,
                norm.Data,
                catalog,
                effectiveCodec,
                ct);
        }
    }

    /// <summary>
    /// Gets the shared readable cursor codec list pages use when a caller
    /// supplies none.
    /// </summary>
    /// <remarks>
    /// The property has no setter. A process-wide settable codec would be
    /// shared mutable state: two test suites would interfere with each other
    /// depending on test order, and a host could change the codec under pages
    /// that are in flight. A page extension takes its own optional codec
    /// instead.
    /// </remarks>
    public static IKeysetCursorCodec DefaultCodec { get; } = new OpaqueKeysetCursorCodec();

    /// <summary>
    /// Checks the arms a page is drawn from: present, not empty, and none of
    /// them null.
    /// </summary>
    /// <typeparam name="TRow">The row type each arm is projected to.</typeparam>
    /// <param name="arms">The arms the caller passed.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the arms are null or hold a null arm, named by position.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when the arms are empty.</exception>
    private static void GuardArms<TRow>(IReadOnlyList<IQueryable<TRow>> arms)
    {
        ArgumentNullException.ThrowIfNull(arms);

        if (arms.Falsey())
        {
            throw new ArgumentException(
                "A page is drawn from at least one arm.", nameof(arms));
        }

        for (var i = 0; i < arms.Count; i++)
            ArgumentNullException.ThrowIfNull(arms[i], $"{nameof(arms)}[{i}]");
    }
}
