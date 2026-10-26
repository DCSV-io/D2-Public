// -----------------------------------------------------------------------
// <copyright file="SearchCatalog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination.EntityFrameworkCore;

using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using DcsvIo.D2.Pagination;
using DcsvIo.D2.Pagination.Errors;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Immutable search shape for a page-row type. Exposes no member that mutates
/// it, so no caller can write to a process-wide instance of it.
/// </summary>
/// <remarks>
/// Registration lives on <see cref="SearchCatalogBuilder{TRow}"/>, a separate
/// type rather than a built flag, so a registration call on a built catalog
/// does not compile. No registration call can throw, do nothing or race at run
/// time.
/// </remarks>
/// <typeparam name="TRow">EF-clean page-row type.</typeparam>
public sealed class SearchCatalog<TRow>
{
    private static readonly MemberInfo sr_scoredRow =
        typeof(Scored<TRow>).GetProperty(nameof(Scored<TRow>.Row))
        ?? throw new InvalidOperationException("Scored<TRow> lost its Row member.");

    private static readonly MemberInfo sr_scoredScore =
        typeof(Scored<TRow>).GetProperty(nameof(Scored<TRow>.Score))
        ?? throw new InvalidOperationException("Scored<TRow> lost its Score member.");

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchCatalog{TRow}"/>
    /// class. Reachable only from <see cref="SearchCatalogBuilder{TRow}"/>, so
    /// a built catalog exposes no registration member.
    /// </summary>
    /// <param name="blob">The concatenated column the filter runs against.</param>
    /// <param name="rankFields">The fields contributing to relevance.</param>
    /// <param name="tieBreak">The stable tie-break for equal scores.</param>
    internal SearchCatalog(
        Expression<Func<TRow, string>> blob,
        IReadOnlyList<SearchRankField<TRow>> rankFields,
        Expression<Func<TRow, Guid>> tieBreak)
    {
        Blob = blob;
        RankFields = rankFields;
        TieBreak = tieBreak;
    }

    /// <summary>Gets the concatenated column the filter runs against.</summary>
    public Expression<Func<TRow, string>> Blob { get; }

    /// <summary>Gets the fields contributing to relevance, with their weights.</summary>
    public IReadOnlyList<SearchRankField<TRow>> RankFields { get; }

    /// <summary>Gets the stable tie-break for rows of equal score.</summary>
    public Expression<Func<TRow, Guid>> TieBreak { get; }

    /// <summary>
    /// Builds the relevance sort entry over the projection: score descending,
    /// then tie-break. Allocates and returns a fresh entry, and writes nothing
    /// to this catalog.
    /// </summary>
    /// <remarks>
    /// It needs neither the dialect nor the query because <see cref="Project"/>
    /// has already put the score where the entry can read it. Building a second
    /// expression for the same quantity would be a recomputation that could
    /// disagree with the dialect's own.
    /// </remarks>
    /// <returns>The relevance entry over the scored projection.</returns>
    public SortFieldEntry<Scored<TRow>> BuildRelevanceEntry()
    {
        var tieBreak = Rebase(TieBreak);

        return SortFieldEntry<Scored<TRow>>.CreateOrderedPrimary(
            SortFieldNames.RELEVANCE,
            static s => s.Score,
            tieBreak,
            EncodeScore,
            DecodeScore);
    }

    /// <summary>
    /// Projects rows to their score for this query, so the score is a
    /// materialized value the cursor can be encoded from.
    /// </summary>
    /// <param name="dialect">The provider's filtering and ranking expressions.</param>
    /// <param name="query">The validated query.</param>
    /// <returns>The projection expression.</returns>
    public Expression<Func<TRow, Scored<TRow>>> Project(
        ISearchDialect<TRow> dialect,
        SearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(dialect);
        ArgumentNullException.ThrowIfNull(query);

        var score = dialect.Score(RankFields, query);
        var row = Expression.Parameter(typeof(TRow), "r");
        var scoreBody = ParameterRebaser.Replace(score.Body, score.Parameters[0], row);

        // Member initialization rather than a constructor call: the ordering
        // and the cursor filter are applied to this projection, and a provider
        // binds a member read back to a settable member and refuses to bind one
        // back to a constructor argument.
        return Expression.Lambda<Func<TRow, Scored<TRow>>>(
            Expression.MemberInit(
                Expression.New(typeof(Scored<TRow>)),
                Expression.Bind(sr_scoredRow, row),
                Expression.Bind(sr_scoredScore, scoreBody)),
            row);
    }

    /// <summary>
    /// The invariant round-trip form for a score. The cursor comparison tests
    /// the page boundary for equality, so a value that re-decodes even one unit
    /// away moves the boundary and silently drops or repeats rows.
    /// </summary>
    private static string EncodeScore(double score) =>
        score.ToString("R", CultureInfo.InvariantCulture);

    private static D2Result<double> DecodeScore(string wire)
    {
        if (wire.Falsey()
            || !double.TryParse(
                wire,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var score))
        {
            return PaginationFailures<double>.CursorKeyUndecodable();
        }

        return D2Result<double>.Ok(score);
    }

    /// <summary>
    /// Rewrites an expression over the row into the same expression over the
    /// scored wrapper, so the tie-break composes through to the inner row.
    /// </summary>
    private static Expression<Func<Scored<TRow>, TValue>> Rebase<TValue>(
        Expression<Func<TRow, TValue>> over)
    {
        var scored = Expression.Parameter(typeof(Scored<TRow>), "s");
        var row = Expression.Property(scored, nameof(Scored<TRow>.Row));
        var body = ParameterRebaser.Replace(over.Body, over.Parameters[0], row);
        return Expression.Lambda<Func<Scored<TRow>, TValue>>(body, scored);
    }
}
