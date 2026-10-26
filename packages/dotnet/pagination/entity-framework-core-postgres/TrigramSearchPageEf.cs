// -----------------------------------------------------------------------
// <copyright file="TrigramSearchPageEf.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination.EntityFrameworkCore.Postgres;

using DcsvIo.D2.Pagination;
using DcsvIo.D2.Pagination.EntityFrameworkCore;
using DcsvIo.D2.Result;

/// <summary>
/// One trigram-backed search page. Supplies the dialect the provider-neutral
/// overload requires, so choosing a provider stays a fact about the host rather
/// than an argument on every call site.
/// </summary>
/// <remarks>
/// The method name states the provider. A caller of this extension chooses
/// trigram matching, with its behavior and its index requirement.
/// </remarks>
public static class TrigramSearchPageEf
{
    /// <summary>
    /// Trigram search extension on an EF-projected page-row query.
    /// </summary>
    /// <typeparam name="TRow">EF-clean page-row type (generated PageRow).</typeparam>
    /// <param name="query">Base query already projected to <typeparamref name="TRow"/>.</param>
    extension<TRow>(IQueryable<TRow> query)
    {
        /// <summary>
        /// Materializes one trigram-backed search page.
        /// </summary>
        /// <param name="request">Wire page request (cursor, limit, sort, order, query).</param>
        /// <param name="descriptor">The operation's emitted searchable descriptor.</param>
        /// <param name="reach">The rows this search may reach.</param>
        /// <param name="codec">
        /// Cursor codec; <see langword="null"/> uses
        /// <see cref="ListPageEf.DefaultCodec"/>.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The page, or a pagination failure.</returns>
        public ValueTask<D2Result<PageResult<TRow>?>> ToTrigramSearchPageAsync(
            PageRequest request,
            ISearchableListPageDescriptor<TRow> descriptor,
            SearchReach<TRow> reach,
            IKeysetCursorCodec? codec = null,
            CancellationToken ct = default)
            => query.ToSearchPageAsync(
                request,
                descriptor,
                reach,
                new TrigramSearchDialect<TRow>(),
                codec,
                ct);
    }
}
