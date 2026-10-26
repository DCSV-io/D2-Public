// -----------------------------------------------------------------------
// <copyright file="SearchBlobModelBuilderExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination.EntityFrameworkCore.Postgres;

using System.Text;
using DcsvIo.D2.Pagination.EntityFrameworkCore;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Declares the generated search-blob column, its trigram index and its
/// sources on an entity.
/// </summary>
public static class SearchBlobModelBuilderExtensions
{
    /// <summary>
    /// Search-blob declaration extension block on
    /// <see cref="EntityTypeBuilder{TEntity}"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity carrying the blob column.</typeparam>
    /// <param name="builder">The entity type builder.</param>
    extension<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        /// <summary>
        /// Declares the generated blob column, its index and its sources.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The stored column holds the source text as stored, unfolded, with
        /// each source separated by one space in the descriptor's declared
        /// order, absent sources contributing nothing and their separators not
        /// doubling, and runs of whitespace inside a source collapsed to one
        /// space. That collapse must cover the same characters query
        /// normalization collapses, and
        /// <see cref="WHITESPACE_CLASS"/> is where the set is written out.
        /// </para>
        /// <para>
        /// The column is not case-folded. The dialect makes the match
        /// case-insensitive, because a provider's lower-casing function is
        /// collation-dependent outside the ASCII range and a .NET fold cannot
        /// be made to agree with it there.
        /// </para>
        /// </remarks>
        /// <param name="descriptor">The emitted search descriptor.</param>
        /// <returns>The entity type builder for fluent chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// The blob property, or one of the declared source properties, is not
        /// mapped on this entity, or the descriptor declares no source at all.
        /// Naming a property the entity does not have is a wiring defect, so
        /// the model build fails at start-up rather than the first query
        /// returning nothing.
        /// </exception>
        public EntityTypeBuilder<TEntity> HasD2SearchBlob(SearchIndexDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(descriptor);

            if (descriptor.SourceProperties.Falsey())
            {
                throw new InvalidOperationException(
                    $"Search descriptor for '{typeof(TEntity).Name}' declares no source "
                    + "property, so the blob column would always be empty.");
            }

            var sourceColumns = new List<string>(descriptor.SourceProperties.Count);

            foreach (var property in descriptor.SourceProperties)
                sourceColumns.Add(ResolveColumn(builder, property));

            // Resolved for its check: a blob property the entity does not map
            // fails here rather than at the first query.
            _ = ResolveColumn(builder, descriptor.BlobProperty);

            builder
                .Property(descriptor.BlobProperty)
                .HasComputedColumnSql(BuildBlobSql(sourceColumns), stored: true);

            builder
                .HasIndex(descriptor.BlobProperty)
                .HasMethod(INDEX_METHOD)
                .HasOperators(INDEX_OPERATOR_CLASS);

            return builder;
        }
    }

    /// <summary>The index method the blob column is indexed with.</summary>
    public const string INDEX_METHOD = "gin";

    /// <summary>The operator class the blob index is built with.</summary>
    public const string INDEX_OPERATOR_CLASS = "gin_trgm_ops";

    /// <summary>
    /// The regular-expression character class the stored column's whitespace
    /// collapse is defined over.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A row is findable by its own text only if the column collapses exactly
    /// the characters <c>SearchQuery.Create</c> collapses, and that side runs
    /// <c>char.IsWhiteSpace</c>. The database cannot run that predicate, so
    /// the same set is written out here as codepoints, and a unit test
    /// asserts over every character of the basic multilingual plane that this
    /// class and <c>char.IsWhiteSpace</c> admit the same ones.
    /// </para>
    /// <para>
    /// The provider's own whitespace shorthand is not that set and cannot be
    /// made into it: <c>\s</c> resolves to <c>[[:space:]]</c> under the
    /// database's ctype, which in the glibc UTF-8 locales excludes the
    /// non-breaking spaces U+00A0 and U+202F while <c>char.IsWhiteSpace</c>
    /// admits both. A row whose source text carried one of those kept it
    /// verbatim in the blob while the caller's identical text had it collapsed,
    /// so the row could not be found by its own text.
    /// </para>
    /// <para>
    /// The set is written out rather than derived from the predicate at model
    /// build because this is a stored generated column: the expression is
    /// fixed at schema creation, and changing it later rewrites every row. A
    /// runtime whose <c>char.IsWhiteSpace</c> moves therefore fails the
    /// agreement test rather than silently emitting a different column.
    /// </para>
    /// </remarks>
    public const string WHITESPACE_CLASS =
        "[\\u0009-\\u000D\\u0020\\u0085\\u00A0\\u1680\\u2000-\\u200A"
        + "\\u2028\\u2029\\u202F\\u205F\\u3000]";

    /// <summary>
    /// Builds the stored-column expression: join the sources with a single
    /// space, collapse every whitespace run to one space, then trim.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every operator here is immutable, and that is a constraint rather than a
    /// preference: PostgreSQL rejects a stored generated column whose
    /// expression is not, and the obvious way to write this join -
    /// <c>concat_ws</c> - is marked stable because it reaches a type's output
    /// function. A model declaring one fails at schema creation with
    /// <c>42P17</c>, not at any query.
    /// </para>
    /// <para>
    /// An absent source is handled by coalescing it to the empty string rather
    /// than by a function that skips it: the separators around it survive as
    /// whitespace, and the collapse that follows folds them back into one
    /// space, with the trim removing them entirely at either end. That is why
    /// the collapse runs before the trim rather than after - a source beginning
    /// with a tab would otherwise leave a leading space behind.
    /// </para>
    /// </remarks>
    /// <param name="sourceColumns">The source column names, in join order.</param>
    /// <returns>The computed-column SQL.</returns>
    internal static string BuildBlobSql(IReadOnlyList<string> sourceColumns)
    {
        var sb = new StringBuilder("trim(regexp_replace(");

        for (var i = 0; i < sourceColumns.Count; i++)
        {
            if (i > 0)
                sb.Append(" || ' ' || ");

            sb.Append("coalesce(");
            AppendQuoted(sb, sourceColumns[i]);
            sb.Append(", '')");
        }

        sb.Append(", '").Append(WHITESPACE_CLASS).Append("+', ' ', 'g'))");
        return sb.ToString();
    }

    private static void AppendQuoted(StringBuilder sb, string identifier)
    {
        sb.Append('"');
        sb.Append(identifier.Replace("\"", "\"\"", StringComparison.Ordinal));
        sb.Append('"');
    }

    private static string ResolveColumn<TEntity>(
        EntityTypeBuilder<TEntity> builder,
        string propertyName)
        where TEntity : class
    {
        var column = builder.Metadata.FindProperty(propertyName)?.GetColumnName();

        if (column is null)
        {
            throw new InvalidOperationException(
                $"Search descriptor names property '{propertyName}', which entity "
                + $"'{typeof(TEntity).Name}' does not map to a column.");
        }

        return column;
    }
}
