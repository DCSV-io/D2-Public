// -----------------------------------------------------------------------
// <copyright file="ParameterRebaser.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination.EntityFrameworkCore;

using System.Linq.Expressions;

/// <summary>
/// Rewrites one expression tree's parameter to another expression, so a
/// lambda written over a row can be composed into a lambda over something that
/// holds one.
/// </summary>
internal static class ParameterRebaser
{
    /// <summary>
    /// Returns <paramref name="body"/> with every occurrence of
    /// <paramref name="from"/> replaced by <paramref name="to"/>.
    /// </summary>
    /// <param name="body">The expression to rewrite.</param>
    /// <param name="from">The parameter to replace.</param>
    /// <param name="to">The expression to replace it with.</param>
    /// <returns>The rewritten expression.</returns>
    public static Expression Replace(Expression body, ParameterExpression from, Expression to)
    {
        // ExpressionVisitor.Visit is typed Expression? but never returns null for a live tree.
        return new RebaseVisitor(from, to).Visit(body)
            ?? throw new InvalidOperationException("Expression parameter rewrite returned null.");
    }

    private sealed class RebaseVisitor(ParameterExpression from, Expression to)
        : ExpressionVisitor
    {
        /// <inheritdoc/>
        protected override Expression VisitParameter(ParameterExpression node)
            => node == from ? to : base.VisitParameter(node);
    }
}
