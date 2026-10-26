// -----------------------------------------------------------------------
// <copyright file="SortFieldEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination.EntityFrameworkCore;

using System.Linq.Expressions;
using System.Numerics;
using DcsvIo.D2.Pagination;
using DcsvIo.D2.Pagination.Errors;
using DcsvIo.D2.Result;

/// <summary>
/// One registered sort field on a page-row catalog.
/// </summary>
/// <typeparam name="TRow">Page-row type.</typeparam>
public sealed class SortFieldEntry<TRow>
{
    private readonly Func<IQueryable<TRow>, SortDirection, IQueryable<TRow>> r_order;
    private readonly Func<
        IQueryable<TRow>,
        string,
        Guid,
        SortDirection,
        D2Result<IQueryable<TRow>?>> r_applyCursor;

    private readonly Func<TRow, string> r_encodePrimary;

    private readonly Func<TRow, Guid> r_tieBreak;

    private SortFieldEntry(
        string fieldWire,
        Func<IQueryable<TRow>, SortDirection, IQueryable<TRow>> order,
        Func<IQueryable<TRow>, string, Guid, SortDirection,
            D2Result<IQueryable<TRow>?>> applyCursor,
        Func<TRow, string> encodePrimary,
        Func<TRow, Guid> tieBreak)
    {
        FieldWire = fieldWire;
        r_order = order;
        r_applyCursor = applyCursor;
        r_encodePrimary = encodePrimary;
        r_tieBreak = tieBreak;
    }

    /// <summary>Gets the wire field name.</summary>
    public string FieldWire { get; }

    /// <summary>
    /// Builds an entry for a comparable primary + Guid tie-break.
    /// </summary>
    /// <typeparam name="TPrimary">Comparable primary type.</typeparam>
    /// <param name="fieldWire">Wire field name.</param>
    /// <param name="primary">Primary expression.</param>
    /// <param name="tieBreak">Tie-break expression.</param>
    /// <param name="encodePrimary">Encode primary to wire.</param>
    /// <param name="decodePrimary">Decode primary from wire.</param>
    /// <returns>Catalog entry.</returns>
    public static SortFieldEntry<TRow> CreateComparable<TPrimary>(
        string fieldWire,
        Expression<Func<TRow, TPrimary>> primary,
        Expression<Func<TRow, Guid>> tieBreak,
        Func<TPrimary, string> encodePrimary,
        Func<string, D2Result<TPrimary>> decodePrimary)
        where TPrimary : IComparable<TPrimary>
    {
        var primaryCompiled = primary.Compile();
        var tieBreakCompiled = tieBreak.Compile();

        IQueryable<TRow> Order(IQueryable<TRow> q, SortDirection dir)
        {
            if (dir == SortDirection.Asc)
            {
                return q.OrderBy(primary).ThenBy(tieBreak);
            }

            return q.OrderByDescending(primary).ThenByDescending(tieBreak);
        }

        D2Result<IQueryable<TRow>?> ApplyCursor(
            IQueryable<TRow> q,
            string primaryKeyWire,
            Guid tieBreakId,
            SortDirection dir)
        {
            var decoded = decodePrimary(primaryKeyWire);

            if (decoded.Failed)
                return PaginationFailures<IQueryable<TRow>?>.CursorKeyUndecodable();

            // Comparer<T>.Default.Compare so string, Instant and Guid all translate.
            var param = Expression.Parameter(typeof(TRow), "r");
            var primaryBody = ReplaceParameter(primary.Body, primary.Parameters[0], param);
            var tieBody = ReplaceParameter(tieBreak.Body, tieBreak.Parameters[0], param);
            var curPrimaryConst = Bind(decoded.Data!);
            var curIdConst = Bind(tieBreakId);

            var comparerType = typeof(Comparer<>).MakeGenericType(typeof(TPrimary));
            var defaultComparer = Expression.Property(
                null,
                comparerType.GetProperty(nameof(Comparer<int>.Default))!);
            var compareMethod = comparerType.GetMethod(
                nameof(Comparer<int>.Compare),
                [typeof(TPrimary), typeof(TPrimary)])!;
            var primaryCompare = Expression.Call(
                defaultComparer,
                compareMethod,
                primaryBody,
                curPrimaryConst);
            var zero = Expression.Constant(0);

            Expression primaryCmp;
            Expression idCmp;

            if (dir == SortDirection.Asc)
            {
                primaryCmp = Expression.GreaterThan(primaryCompare, zero);
                idCmp = Expression.GreaterThan(tieBody, curIdConst);
            }
            else
            {
                primaryCmp = Expression.LessThan(primaryCompare, zero);
                idCmp = Expression.LessThan(tieBody, curIdConst);
            }

            var equalPrimary = Expression.Equal(primaryCompare, zero);
            var predicate = Expression.OrElse(
                primaryCmp,
                Expression.AndAlso(equalPrimary, idCmp));
            var lambda = Expression.Lambda<Func<TRow, bool>>(predicate, param);
            return D2Result<IQueryable<TRow>?>.Ok(q.Where(lambda));
        }

        return new SortFieldEntry<TRow>(
            fieldWire,
            Order,
            ApplyCursor,
            row => encodePrimary(primaryCompiled(row)),
            tieBreakCompiled);
    }

    /// <summary>
    /// Builds an entry for a primary the database can order with the
    /// relational operators directly, plus a Guid tie-break.
    /// </summary>
    /// <remarks>
    /// <see cref="CreateComparable{TPrimary}"/> builds a default-comparer
    /// expression tree, which is what a string or an instant needs and is
    /// unnecessary translation risk for a numeric key.
    /// </remarks>
    /// <typeparam name="TPrimary">Primary type carrying the relational operators.</typeparam>
    /// <param name="fieldWire">Wire field name.</param>
    /// <param name="primary">Primary expression.</param>
    /// <param name="tieBreak">Tie-break expression.</param>
    /// <param name="encodePrimary">Encodes the primary to its cursor wire form.</param>
    /// <param name="decodePrimary">Decodes the primary from its cursor wire form.</param>
    /// <returns>Catalog entry.</returns>
    public static SortFieldEntry<TRow> CreateOrderedPrimary<TPrimary>(
        string fieldWire,
        Expression<Func<TRow, TPrimary>> primary,
        Expression<Func<TRow, Guid>> tieBreak,
        Func<TPrimary, string> encodePrimary,
        Func<string, D2Result<TPrimary>> decodePrimary)
        where TPrimary : IComparisonOperators<TPrimary, TPrimary, bool>
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(tieBreak);
        ArgumentNullException.ThrowIfNull(encodePrimary);
        ArgumentNullException.ThrowIfNull(decodePrimary);

        var primaryCompiled = primary.Compile();
        var tieBreakCompiled = tieBreak.Compile();

        IQueryable<TRow> Order(IQueryable<TRow> q, SortDirection dir)
        {
            if (dir == SortDirection.Asc)
                return q.OrderBy(primary).ThenBy(tieBreak);

            return q.OrderByDescending(primary).ThenByDescending(tieBreak);
        }

        D2Result<IQueryable<TRow>?> ApplyCursor(
            IQueryable<TRow> q,
            string primaryKeyWire,
            Guid tieBreakId,
            SortDirection dir)
        {
            var decoded = decodePrimary(primaryKeyWire);

            if (decoded.Failed)
                return PaginationFailures<IQueryable<TRow>?>.CursorKeyUndecodable();

            var param = Expression.Parameter(typeof(TRow), "r");
            var primaryBody = ReplaceParameter(primary.Body, primary.Parameters[0], param);
            var tieBody = ReplaceParameter(tieBreak.Body, tieBreak.Parameters[0], param);
            var curPrimaryConst = Bind(decoded.Data!);
            var curIdConst = Bind(tieBreakId);

            Expression primaryCmp;
            Expression idCmp;

            if (dir == SortDirection.Asc)
            {
                primaryCmp = Expression.GreaterThan(primaryBody, curPrimaryConst);
                idCmp = Expression.GreaterThan(tieBody, curIdConst);
            }
            else
            {
                primaryCmp = Expression.LessThan(primaryBody, curPrimaryConst);
                idCmp = Expression.LessThan(tieBody, curIdConst);
            }

            var equalPrimary = Expression.Equal(primaryBody, curPrimaryConst);
            var predicate = Expression.OrElse(
                primaryCmp,
                Expression.AndAlso(equalPrimary, idCmp));
            var lambda = Expression.Lambda<Func<TRow, bool>>(predicate, param);
            return D2Result<IQueryable<TRow>?>.Ok(q.Where(lambda));
        }

        return new SortFieldEntry<TRow>(
            fieldWire,
            Order,
            ApplyCursor,
            row => encodePrimary(primaryCompiled(row)),
            tieBreakCompiled);
    }

    /// <summary>
    /// Applies OrderBy / OrderByDescending for this field + tie-break.
    /// </summary>
    /// <param name="query">Source query.</param>
    /// <param name="direction">Sort direction.</param>
    /// <returns>Ordered query.</returns>
    public IQueryable<TRow> ApplyOrder(IQueryable<TRow> query, SortDirection direction)
        => r_order(query, direction);

    /// <summary>
    /// Applies keyset cursor Where for this field.
    /// </summary>
    /// <param name="query">Source query.</param>
    /// <param name="primaryKeyWire">Cursor primary key wire form.</param>
    /// <param name="tieBreakId">Cursor tie-break Guid.</param>
    /// <param name="direction">Applied sort direction.</param>
    /// <returns>Filtered query or ValidationFailed.</returns>
    public D2Result<IQueryable<TRow>?> ApplyCursor(
        IQueryable<TRow> query,
        string primaryKeyWire,
        Guid tieBreakId,
        SortDirection direction)
        => r_applyCursor(query, primaryKeyWire, tieBreakId, direction);

    /// <summary>
    /// Encodes the primary key wire form from a materialized row.
    /// </summary>
    /// <param name="row">Materialized page row.</param>
    /// <returns>Primary key wire string.</returns>
    public string EncodePrimary(TRow row) => r_encodePrimary(row);

    /// <summary>
    /// Reads the tie-break Guid from a materialized row.
    /// </summary>
    /// <param name="row">Materialized page row.</param>
    /// <returns>Tie-break Guid.</returns>
    public Guid TieBreakOf(TRow row) => r_tieBreak(row);

    /// <summary>
    /// Puts a decoded cursor value into the predicate as a member read off an
    /// instance rather than as a tree constant.
    /// </summary>
    /// <remarks>
    /// EF Core renders a <see cref="ConstantExpression"/> inline in the command
    /// text and lifts a member read off an object into a query parameter, which
    /// is the same shape the compiler produces for a captured local. The
    /// decoded primary key is a boundary row's own sort value - on a
    /// string-sorted surface that is human-readable text identifying a person
    /// or a record, an email address or a display name - and the
    /// cursor it arrives in is unauthenticated, so it is also caller-chosen
    /// text. Inline, it reaches the provider's command logger and its exception
    /// detail with no sensitive-data switch involved, and every distinct
    /// boundary value becomes its own statement and its own plan.
    /// </remarks>
    /// <typeparam name="TValue">The bound value's type.</typeparam>
    /// <param name="value">The decoded cursor value.</param>
    /// <returns>The member-read expression the translator parameterizes.</returns>
    private static MemberExpression Bind<TValue>(TValue value) =>
        Expression.Property(
            Expression.Constant(new BoundValue<TValue>(value)),
            nameof(BoundValue<TValue>.Value));

    private static Expression ReplaceParameter(
        Expression body,
        ParameterExpression from,
        ParameterExpression to)
    {
        // ExpressionVisitor.Visit is typed Expression? but never returns null for a live tree.
        return new ParameterReplaceVisitor(from, to).Visit(body)
            ?? throw new InvalidOperationException("Expression parameter rewrite returned null.");
    }

    /// <summary>
    /// Holds one decoded cursor value so the predicate reads it through a
    /// member instead of carrying it as a constant. Nothing calls the getter in
    /// this assembly; the translator reads it through the member expression
    /// <see cref="Bind{TValue}"/> builds.
    /// </summary>
    /// <typeparam name="TValue">The bound value's type.</typeparam>
    /// <param name="value">The decoded cursor value.</param>
    private sealed class BoundValue<TValue>(TValue value)
    {
        /// <summary>Gets the decoded cursor value.</summary>
        public TValue Value { get; } = value;
    }

    private sealed class ParameterReplaceVisitor(ParameterExpression from, ParameterExpression to)
        : ExpressionVisitor
    {
        /// <inheritdoc/>
        protected override Expression VisitParameter(ParameterExpression node)
            => node == from ? to : base.VisitParameter(node);
    }
}
