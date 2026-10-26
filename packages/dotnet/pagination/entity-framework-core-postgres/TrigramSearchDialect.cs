// -----------------------------------------------------------------------
// <copyright file="TrigramSearchDialect.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination.EntityFrameworkCore.Postgres;

using System.Linq.Expressions;
using System.Text;
using DcsvIo.D2.Pagination;
using DcsvIo.D2.Pagination.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// The PostgreSQL trigram search dialect: a case-insensitive containment
/// pattern for the filter, and weighted trigram similarity for the score.
/// </summary>
/// <remarks>
/// <para>
/// The filter is the index-accelerated stage and the score is the ranking
/// stage, in that order. The pattern narrows to candidate rows through the
/// trigram index; similarity then ranks only what survives. Ranking first would
/// score every reachable row.
/// </para>
/// <para>
/// This dialect matches patterns, so it escapes the pattern metacharacters and
/// the escape character itself before wrapping the query in wildcards, and
/// emits an explicit escape clause with the pattern. Without that, a query of
/// wildcard characters would match every row instead of the rows whose text
/// contains those characters.
/// </para>
/// <para>
/// It applies no case transform to either side. Case-insensitivity is a
/// property of the operator, so the stored column stays unfolded and this
/// dialect asks the database to fold nothing.
/// <see cref="SearchQuery.Normalized"/> does arrive case-folded, and that is
/// not what makes the match case-insensitive: the query is canonicalized so
/// that <see cref="SearchQuery.QueryToken"/> - the cursor's binding to its
/// query - is a digest of exactly the text handed to this dialect. Matching
/// would be case-insensitive without it.
/// </para>
/// </remarks>
/// <typeparam name="TRow">EF-clean page-row type.</typeparam>
public sealed class TrigramSearchDialect<TRow> : ISearchDialect<TRow>
{
    /// <summary>The pattern escape character, as both SQL text and a C# escape.</summary>
    public const string ESCAPE_CHARACTER = "\\";

    /// <summary>
    /// Escapes the pattern metacharacters and the escape character itself, and
    /// nothing else.
    /// </summary>
    /// <param name="literal">The literal text to match.</param>
    /// <returns>The text as a pattern that matches it literally.</returns>
    public static string EscapePattern(string literal)
    {
        ArgumentNullException.ThrowIfNull(literal);

        var sb = new StringBuilder(literal.Length);

        foreach (var c in literal)
        {
            if (c is '\\' or '%' or '_')
                sb.Append('\\');

            sb.Append(c);
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public Expression<Func<TRow, bool>> Filter(
        Expression<Func<TRow, string>> blob,
        SearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(blob);
        ArgumentNullException.ThrowIfNull(query);

        var pattern = Bind("%" + EscapePattern(query.Normalized) + "%");
        var call = Expression.Call(
            TrigramFunctions.SR_ILike,
            Expression.Constant(EF.Functions),
            blob.Body,
            pattern,
            Expression.Constant(ESCAPE_CHARACTER));

        return Expression.Lambda<Func<TRow, bool>>(call, blob.Parameters[0]);
    }

    /// <inheritdoc/>
    public Expression<Func<TRow, double>> Score(
        IReadOnlyList<SearchRankField<TRow>> rankFields,
        SearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(rankFields);
        ArgumentNullException.ThrowIfNull(query);

        var row = Expression.Parameter(typeof(TRow), "r");
        var functions = Expression.Constant(EF.Functions);
        var target = Bind(query.Normalized);
        Expression sum = Expression.Constant(0d);

        foreach (var field in rankFields)
        {
            var source = ParameterRebaser.Replace(
                field.Field.Body,
                field.Field.Parameters[0],
                row);
            var similarity = Expression.Call(
                TrigramFunctions.SR_Similarity,
                functions,
                source,
                target);
            var weighted = Expression.Multiply(
                Expression.Constant(field.Weight),
                Expression.Convert(similarity, typeof(double)));

            sum = Expression.Add(sum, weighted);
        }

        return Expression.Lambda<Func<TRow, double>>(sum, row);
    }

    /// <summary>
    /// Puts a caller-derived string into the tree as a member read off an
    /// instance rather than as a tree constant.
    /// </summary>
    /// <remarks>
    /// EF Core renders a <see cref="ConstantExpression"/> inline in the command
    /// text and lifts a member read off an object into a query parameter, which
    /// is the same shape the compiler produces for a captured local. Two things
    /// follow from taking the second shape: the caller's search text stays out
    /// of the SQL, so it is out of the command logger and out of provider
    /// exception detail; and every term produces one query with one plan
    /// instead of one per term.
    /// </remarks>
    /// <param name="value">The caller-derived text.</param>
    /// <returns>The member-read expression the translator parameterizes.</returns>
    private static MemberExpression Bind(string value) =>
        Expression.Property(
            Expression.Constant(new BoundText(value)),
            nameof(BoundText.Value));

    /// <summary>
    /// Holds one caller-derived string so the expression tree reads it through
    /// a member instead of carrying it as a constant. Nothing calls the getter
    /// in this assembly; the translator reads it through the member expression
    /// <see cref="Bind"/> builds.
    /// </summary>
    /// <param name="value">The caller-derived text.</param>
    private sealed class BoundText(string value)
    {
        /// <summary>Gets the caller-derived text.</summary>
        public string Value { get; } = value;
    }
}
