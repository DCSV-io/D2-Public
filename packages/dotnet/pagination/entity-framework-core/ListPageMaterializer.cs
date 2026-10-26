// -----------------------------------------------------------------------
// <copyright file="ListPageMaterializer.cs" company="DCSV">
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
/// The page-drawing step every list path shares: count, apply the cursor
/// predicate, order, take one row beyond the limit, and encode the next cursor.
/// </summary>
/// <remarks>
/// The type the page is drawn from is not always the row the page returns.
/// A relevance page is drawn from a projection carrying the materialized score,
/// so the ordering type and the returned row type are separate parameters and
/// an unwrap function maps one to the other.
/// </remarks>
internal static class ListPageMaterializer
{
    /// <summary>
    /// Materializes one page over a query already narrowed to the rows the
    /// page may draw from.
    /// </summary>
    /// <typeparam name="TPage">The type the page is ordered and cursored by.</typeparam>
    /// <typeparam name="TRow">The row type the caller receives.</typeparam>
    /// <param name="source">The pre-cursor query, used for the total count.</param>
    /// <param name="norm">The normalized request.</param>
    /// <param name="catalog">The sort catalog to resolve the applied field in.</param>
    /// <param name="codec">The cursor codec.</param>
    /// <param name="unwrap">Projects one page element back to the caller's row.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The page, or a pagination failure.</returns>
    public static async ValueTask<D2Result<PageResult<TRow>?>> MaterializeAsync<TPage, TRow>(
        IQueryable<TPage> source,
        NormalizedPageRequest norm,
        SortCatalog<TPage> catalog,
        IKeysetCursorCodec codec,
        Func<TPage, TRow> unwrap,
        CancellationToken ct)
    {
        if (!catalog.TryGet(norm.AppliedSort.Field, out var entry))
            return PaginationFailures<PageResult<TRow>?>.SortFieldNotRegistered();

        var totalCount = await source.LongCountAsync(ct);

        return await DrawAsync(source, totalCount, norm, entry, unwrap, codec, ct);
    }

    /// <summary>
    /// Materializes one page drawn from several queries that admit no row in
    /// common.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cursor predicate, the ordering and the page limit are applied to
    /// each arm before the arms are concatenated, and again to the
    /// concatenation. A relational database can walk an ordered index per arm
    /// that way and stop at the page; asked for the same rows as one query over
    /// an OR of the arms' predicates, it reads every matching row and sorts
    /// them to draw each page.
    /// </para>
    /// <para>
    /// Taking the page limit from each arm and then from their concatenation
    /// returns the same rows as taking it from the whole: no row outside an
    /// arm's own first page can be inside the first page of the union. The
    /// total is counted over the arms unlimited, so it is the count of
    /// everything the caller may read.
    /// </para>
    /// <para>
    /// The arms must admit no row in common. Concatenation keeps duplicates, so
    /// a row two arms admit is counted twice and returned twice.
    /// </para>
    /// </remarks>
    /// <typeparam name="TRow">The row type the caller receives.</typeparam>
    /// <param name="arms">
    /// The disjoint queries the page is drawn from. The caller has already
    /// checked that there is at least one and that none of them is null.
    /// </param>
    /// <param name="norm">The normalized request.</param>
    /// <param name="catalog">The sort catalog to resolve the applied field in.</param>
    /// <param name="codec">The cursor codec.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The page, or a pagination failure.</returns>
    public static async ValueTask<D2Result<PageResult<TRow>?>> MaterializeArmsAsync<TRow>(
        IReadOnlyList<IQueryable<TRow>> arms,
        NormalizedPageRequest norm,
        SortCatalog<TRow> catalog,
        IKeysetCursorCodec codec,
        CancellationToken ct)
    {
        if (!catalog.TryGet(norm.AppliedSort.Field, out var entry))
            return PaginationFailures<PageResult<TRow>?>.SortFieldNotRegistered();

        var whole = arms[0];

        for (var i = 1; i < arms.Count; i++)
            whole = whole.Concat(arms[i]);

        var totalCount = await whole.LongCountAsync(ct);
        var bounded = new IQueryable<TRow>[arms.Count];

        for (var i = 0; i < arms.Count; i++)
        {
            var cursored = ApplyCursor(arms[i], norm, entry);

            if (cursored.Failed || cursored.Data is null)
                return D2Result<PageResult<TRow>?>.BubbleFail(cursored);

            bounded[i] = entry
                .ApplyOrder(cursored.Data, norm.AppliedSort.Direction)
                .Take(norm.Limit + 1);
        }

        var drawn = bounded[0];

        for (var i = 1; i < bounded.Length; i++)
            drawn = drawn.Concat(bounded[i]);

        return await DrawOrderedAsync(
            drawn, totalCount, norm, entry, static row => row, codec, ct);
    }

    /// <summary>
    /// Materializes one page whose ordering entry the caller already holds -
    /// the relevance path, whose entry is built per request rather than
    /// registered in the catalog.
    /// </summary>
    /// <typeparam name="TPage">The type the page is ordered and cursored by.</typeparam>
    /// <typeparam name="TRow">The row type the caller receives.</typeparam>
    /// <param name="pageQuery">The query the page is drawn from.</param>
    /// <param name="totalCount">The count taken before the projection.</param>
    /// <param name="norm">The normalized request.</param>
    /// <param name="entry">The ordering entry over <typeparamref name="TPage"/>.</param>
    /// <param name="unwrap">Projects one page element back to the caller's row.</param>
    /// <param name="codec">The cursor codec.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The page, or a pagination failure.</returns>
    public static async ValueTask<D2Result<PageResult<TRow>?>> DrawAsync<TPage, TRow>(
        IQueryable<TPage> pageQuery,
        long totalCount,
        NormalizedPageRequest norm,
        SortFieldEntry<TPage> entry,
        Func<TPage, TRow> unwrap,
        IKeysetCursorCodec codec,
        CancellationToken ct)
    {
        var filtered = ApplyCursor(pageQuery, norm, entry);

        if (filtered.Failed || filtered.Data is null)
            return D2Result<PageResult<TRow>?>.BubbleFail(filtered);

        return await DrawOrderedAsync(
            filtered.Data, totalCount, norm, entry, unwrap, codec, ct);
    }

    /// <summary>
    /// Draws one page from a query the cursor predicate has already narrowed:
    /// order, take one row beyond the limit, and encode the next cursor.
    /// </summary>
    /// <typeparam name="TPage">The type the page is ordered and cursored by.</typeparam>
    /// <typeparam name="TRow">The row type the caller receives.</typeparam>
    /// <param name="pageQuery">The query the page is drawn from.</param>
    /// <param name="totalCount">The count taken before the projection.</param>
    /// <param name="norm">The normalized request.</param>
    /// <param name="entry">The ordering entry over <typeparamref name="TPage"/>.</param>
    /// <param name="unwrap">Projects one page element back to the caller's row.</param>
    /// <param name="codec">The cursor codec.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The page, or a pagination failure.</returns>
    public static async ValueTask<D2Result<PageResult<TRow>?>> DrawOrderedAsync<TPage, TRow>(
        IQueryable<TPage> pageQuery,
        long totalCount,
        NormalizedPageRequest norm,
        SortFieldEntry<TPage> entry,
        Func<TPage, TRow> unwrap,
        IKeysetCursorCodec codec,
        CancellationToken ct)
    {
        var applied = norm.AppliedSort;
        var ordered = entry.ApplyOrder(pageQuery, applied.Direction);

        var drawn = await ordered
            .Take(norm.Limit + 1)
            .ToListAsync(ct);

        string? next = null;

        if (drawn.Count > norm.Limit)
        {
            drawn = drawn.Take(norm.Limit).ToList();
            var last = drawn[^1];
            var enc = codec.Encode(
                new KeysetCursor(
                    KeysetCursor.CURRENT_VERSION,
                    applied.Field,
                    entry.EncodePrimary(last),
                    entry.TieBreakOf(last).ToString("D"),
                    applied.DirectionWire,
                    norm.Search?.QueryToken));

            if (enc.Failed || enc.Data is null)
                return D2Result<PageResult<TRow>?>.BubbleFail(enc);

            next = enc.Data;
        }

        var items = new TRow[drawn.Count];

        for (var i = 0; i < drawn.Count; i++)
            items[i] = unwrap(drawn[i]);

        return D2Result<PageResult<TRow>?>.Ok(
            new PageResult<TRow>(items, next, applied, totalCount));
    }

    /// <summary>
    /// Narrows a query to the rows after the cursor, or leaves it whole when
    /// the request carried none.
    /// </summary>
    /// <typeparam name="TPage">The type the page is ordered and cursored by.</typeparam>
    /// <param name="query">The query to narrow.</param>
    /// <param name="norm">The normalized request.</param>
    /// <param name="entry">The ordering entry the cursor is read under.</param>
    /// <returns>The narrowed query, or a pagination failure.</returns>
    private static D2Result<IQueryable<TPage>?> ApplyCursor<TPage>(
        IQueryable<TPage> query,
        NormalizedPageRequest norm,
        SortFieldEntry<TPage> entry)
    {
        if (norm.Cursor is not { } cursor)
            return D2Result<IQueryable<TPage>?>.Ok(query);

        if (!cursor.TieBreakerId.TryParseTruthyNull(out var tieN) || tieN is null)
            return PaginationFailures<IQueryable<TPage>?>.CursorInvalid();

        return entry.ApplyCursor(
            query, cursor.PrimaryKey, tieN.Value, norm.AppliedSort.Direction);
    }
}
