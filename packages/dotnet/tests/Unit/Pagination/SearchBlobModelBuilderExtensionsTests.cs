// -----------------------------------------------------------------------
// <copyright file="SearchBlobModelBuilderExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Pagination;

using System.Text.RegularExpressions;
using AwesomeAssertions;
using DcsvIo.D2.Pagination.EntityFrameworkCore;
using DcsvIo.D2.Pagination.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Tests.Unit.Pagination.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xunit;

/// <summary>
/// The generated search-blob column the PostgreSQL leaf declares: its computed
/// expression, its trigram index, and the wiring defects it refuses.
/// </summary>
public sealed partial class SearchBlobModelBuilderExtensionsTests
{
    private static SearchIndexDescriptor DefaultDescriptor =>
        new(
            nameof(TrigramProbeRowFixture.SearchBlob),
            [nameof(TrigramProbeRowFixture.Email), nameof(TrigramProbeRowFixture.Username)],
            [2d, 1d]);

    [Fact]
    public void IndexConstants_PinTheirValues()
    {
        SearchBlobModelBuilderExtensions.INDEX_METHOD.Should().Be("gin");
        SearchBlobModelBuilderExtensions.INDEX_OPERATOR_CLASS.Should().Be("gin_trgm_ops");
    }

    [Fact]
    public void WhitespaceClass_PinsItsLiteralValue()
    {
        // The set-agreement test below passes on any literal denoting the same
        // set, and this literal is not interchangeable with another that does:
        // it is written verbatim into a stored generated column, so a rewrite
        // that reorders it or splits a range differently changes the column
        // expression and therefore every row already stored.
        SearchBlobModelBuilderExtensions.WHITESPACE_CLASS.Should().Be(
            "[\\u0009-\\u000D\\u0020\\u0085\\u00A0\\u1680\\u2000-\\u200A"
            + "\\u2028\\u2029\\u202F\\u205F\\u3000]");
    }

    [Fact]
    public void HasD2SearchBlob_DeclaresTheStoredColumnAndItsIndex()
    {
        var entityType = BuildModel(DefaultDescriptor).Metadata;
        var blob = entityType.FindProperty(nameof(TrigramProbeRowFixture.SearchBlob))!;

        blob.GetComputedColumnSql().Should().Be(
            "trim(regexp_replace(coalesce(\"email\", '') || ' ' || coalesce(\"username\", '')"
            + ", '" + SearchBlobModelBuilderExtensions.WHITESPACE_CLASS + "+', ' ', 'g'))");
        blob.GetIsStored().Should().BeTrue();

        var index = entityType.GetIndexes().Single();
        index.Properties.Single().Name.Should().Be(nameof(TrigramProbeRowFixture.SearchBlob));
        index.GetMethod().Should().Be(SearchBlobModelBuilderExtensions.INDEX_METHOD);
        index.GetOperators().Should()
            .Equal([SearchBlobModelBuilderExtensions.INDEX_OPERATOR_CLASS]);
    }

    [Fact]
    public void HasD2SearchBlob_ReturnsTheBuilderForChaining()
    {
        var builder = NewEntityBuilder();
        builder.HasD2SearchBlob(DefaultDescriptor).Should().BeSameAs(builder);
    }

    [Fact]
    public void BuildBlobSql_JoinsEverySourceWithOneSpaceInDeclaredOrder()
    {
        SearchBlobModelBuilderExtensions.BuildBlobSql(["a", "b", "c"])
            .Should().Be(
                "trim(regexp_replace(coalesce(\"a\", '') || ' ' || coalesce(\"b\", '')"
                + " || ' ' || coalesce(\"c\", ''), '"
                + SearchBlobModelBuilderExtensions.WHITESPACE_CLASS
                + "+', ' ', 'g'))");
    }

    [Fact]
    public void BuildBlobSql_CollapsesWhitespaceRunsBeforeTrimming()
    {
        // The collapse is the same one query normalization applies, and it only
        // works applied to both sides: a stored value containing a tab or a
        // double space could not otherwise be matched by the single-spaced text
        // a caller types for it. It runs before the trim, because a trim of
        // spaces cannot remove a leading tab.
        var sql = SearchBlobModelBuilderExtensions.BuildBlobSql(["a"]);

        sql.Should().Be(
            "trim(regexp_replace(coalesce(\"a\", ''), '"
            + SearchBlobModelBuilderExtensions.WHITESPACE_CLASS
            + "+', ' ', 'g'))");
    }

    [Fact]
    public void WhitespaceClass_AdmitsExactlyWhatQueryNormalizationCollapses()
    {
        // The pairing this column rests on. The query side runs
        // char.IsWhiteSpace and the column side runs a character class, and a
        // character only one of them collapses makes a row unfindable by its
        // own text - silently, because the row is stored and the query is built
        // by two different pieces of code that never meet.
        var pattern = WhitespaceClassPattern();
        var disagreeing = new List<string>();

        for (var codepoint = 0; codepoint <= char.MaxValue; codepoint++)
        {
            var c = (char)codepoint;

            if (pattern.IsMatch(c.ToString()) != char.IsWhiteSpace(c))
                disagreeing.Add($"U+{codepoint:X4}");
        }

        disagreeing.Should().BeEmpty();
    }

    [Fact]
    public void WhitespaceClass_AdmitsTheNonBreakingSpacesTheProviderShorthandExcludes()
    {
        // The two characters that made the shorthand wrong: char.IsWhiteSpace
        // admits both, and [[:space:]] under the glibc UTF-8 locales - which is
        // what a bare \s resolves to - admits neither.
        var pattern = WhitespaceClassPattern();

        pattern.IsMatch("\u00A0").Should().BeTrue();
        pattern.IsMatch("\u202F").Should().BeTrue();
        SearchBlobModelBuilderExtensions.WHITESPACE_CLASS.Should().NotContain("\\s");
    }

    [Fact]
    public void BuildBlobSql_CoalescesAnAbsentSourceRatherThanNullingTheExpression()
    {
        // A bare concatenation yields null for the whole expression when any
        // operand is null, which would silently empty the blob for every row
        // missing one optional field - the rows most likely to be searched for.
        // The separators around a coalesced-away source survive as whitespace
        // and the collapse folds them back into one.
        var sql = SearchBlobModelBuilderExtensions.BuildBlobSql(["a", "b"]);

        sql.Should().Contain("coalesce(\"a\", '')");
        sql.Should().Contain("coalesce(\"b\", '')");
    }

    [Fact]
    public void BuildBlobSql_UsesOnlyImmutableOperators()
    {
        // PostgreSQL refuses a stored generated column whose expression is not
        // immutable, and concat_ws - the obvious way to write this join - is
        // marked stable. A model declaring one fails at schema creation with
        // 42P17, not at any query, so nothing that only ever built a model
        // would see it.
        var sql = SearchBlobModelBuilderExtensions.BuildBlobSql(["a", "b"]);

        sql.Should().NotContain("concat_ws");
        sql.Should().NotContain("concat(");
        sql.Should().NotContain("format(");
    }

    [Fact]
    public void BuildBlobSql_QuotesAnEmbeddedQuote()
    {
        SearchBlobModelBuilderExtensions.BuildBlobSql(["od\"d"])
            .Should().Contain("\"od\"\"d\"");
    }

    [Fact]
    public void HasD2SearchBlob_UnmappedSourceProperty_ThrowsNamingIt()
    {
        var act = () => BuildModel(
            new SearchIndexDescriptor(
                nameof(TrigramProbeRowFixture.SearchBlob),
                ["NotAProperty"],
                [1d]));

        act.Should().Throw<InvalidOperationException>().WithMessage("*NotAProperty*");
    }

    [Fact]
    public void HasD2SearchBlob_UnmappedBlobProperty_ThrowsNamingIt()
    {
        var act = () => BuildModel(
            new SearchIndexDescriptor(
                "NotABlob",
                [nameof(TrigramProbeRowFixture.Email)],
                [1d]));

        act.Should().Throw<InvalidOperationException>().WithMessage("*NotABlob*");
    }

    [Fact]
    public void HasD2SearchBlob_NoSourceProperty_Throws()
    {
        var act = () => BuildModel(
            new SearchIndexDescriptor(nameof(TrigramProbeRowFixture.SearchBlob), [], []));

        act.Should().Throw<InvalidOperationException>().WithMessage("*no source*");
    }

    [Fact]
    public void HasD2SearchBlob_NullDescriptor_Throws()
    {
        var act = () => NewEntityBuilder().HasD2SearchBlob(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// The column's whitespace class as a .NET regular expression, so the set
    /// it denotes can be compared character by character with the set the query
    /// side collapses. The pattern cannot backtrack - it is one anchored
    /// character class with no quantifier and no alternation - so no timeout
    /// is needed.
    /// </summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(
        "^" + SearchBlobModelBuilderExtensions.WHITESPACE_CLASS + "$",
        RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceClassPattern();

    private static EntityTypeBuilder<TrigramProbeRowFixture> NewEntityBuilder()
    {
        var builder = new ModelBuilder().Entity<TrigramProbeRowFixture>();
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Email).HasColumnName("email");
        builder.Property(r => r.Username).HasColumnName("username");
        builder.Property(r => r.SearchBlob).HasColumnName("search_blob");
        return builder;
    }

    private static EntityTypeBuilder<TrigramProbeRowFixture> BuildModel(
        SearchIndexDescriptor descriptor)
    {
        var builder = NewEntityBuilder();
        builder.HasD2SearchBlob(descriptor);
        return builder;
    }
}
