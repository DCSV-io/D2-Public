// -----------------------------------------------------------------------
// <copyright file="SearchCatalogTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Pagination;

using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using DcsvIo.D2.Pagination;
using DcsvIo.D2.Pagination.EntityFrameworkCore;
using DcsvIo.D2.Pagination.Errors;
using Xunit;

/// <summary>
/// The provider-neutral search shape: the builder, the immutable catalog, the
/// reach type and the emitted index descriptor.
/// </summary>
public sealed class SearchCatalogTests
{
    private static readonly SearchQuery sr_query =
        SearchQuery.Create("ada", new SearchPolicy()).Data!;

    /// <summary>
    /// Gets the empty-string row. <see cref="string.Empty"/> is not a
    /// constant, so it cannot be an <c>InlineData</c> argument.
    /// </summary>
    public static TheoryData<string> EmptyStringRows => [string.Empty];

    [Fact]
    public void Build_KeepsTheBlobRankFieldsAndTieBreak()
    {
        var catalog = BuildCatalog();

        catalog.Blob.Should().NotBeNull();
        catalog.TieBreak.Should().NotBeNull();
        catalog.RankFields.Should().HaveCount(2);
        catalog.RankFields[0].Weight.Should().Be(2d);
        catalog.RankFields[1].Weight.Should().Be(1d);
    }

    [Fact]
    public void Build_WithoutABlob_Throws()
    {
        var act = () => new SearchCatalogBuilder<SearchRowFixture>()
            .RankField(r => r.Email, 1)
            .TieBreak(r => r.Id)
            .Build();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*blob*");
    }

    [Fact]
    public void Build_WithoutATieBreak_Throws()
    {
        var act = () => new SearchCatalogBuilder<SearchRowFixture>()
            .Blob(r => r.SearchBlob)
            .RankField(r => r.Email, 1)
            .Build();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*tie-break*");
    }

    [Fact]
    public void Build_WithNoRankField_IsAllowed()
    {
        var catalog = new SearchCatalogBuilder<SearchRowFixture>()
            .Blob(r => r.SearchBlob)
            .TieBreak(r => r.Id)
            .Build();

        catalog.RankFields.Should().BeEmpty();
    }

    [Fact]
    public void Builder_NullArguments_Throw()
    {
        var builder = new SearchCatalogBuilder<SearchRowFixture>();

        var nullBlob = () => builder.Blob(null!);
        var nullRank = () => builder.RankField(null!, 1);
        var nullTie = () => builder.TieBreak(null!);

        nullBlob.Should().Throw<ArgumentNullException>();
        nullRank.Should().Throw<ArgumentNullException>();
        nullTie.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BuiltCatalog_ExposesNoRegistrationMember()
    {
        // The freeze is a type boundary rather than a flag: the shared
        // instance's type has no registration member, so a registration call on
        // it does not compile and cannot throw, do nothing or race.
        var members = typeof(SearchCatalog<SearchRowFixture>)
            .GetMethods(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToArray();

        members.Should().NotContain("Blob");
        members.Should().NotContain("RankField");
        members.Should().NotContain("TieBreak");

        typeof(SearchCatalog<SearchRowFixture>)
            .GetProperties()
            .Should().OnlyContain(p => p.SetMethod == null);
    }

    [Fact]
    public void BuildRelevanceEntry_ReturnsAFreshEntryAndWritesNothing()
    {
        var catalog = BuildCatalog();

        var first = catalog.BuildRelevanceEntry();
        var second = catalog.BuildRelevanceEntry();

        first.Should().NotBeSameAs(second);
        first.FieldWire.Should().Be(SortFieldNames.RELEVANCE);
        second.FieldWire.Should().Be(SortFieldNames.RELEVANCE);
        catalog.RankFields.Should().HaveCount(2);
    }

    [Fact]
    public void RelevanceEntry_OrdersByScoreDescendingThenTieBreak()
    {
        var entry = BuildCatalog().BuildRelevanceEntry();
        var low = new SearchRowFixture { Id = Guid.CreateVersion7(), Email = "a" };
        var high = new SearchRowFixture { Id = Guid.CreateVersion7(), Email = "b" };

        var scored = new[]
        {
            new Scored<SearchRowFixture> { Row = low, Score = 0.25 },
            new Scored<SearchRowFixture> { Row = high, Score = 0.75 },
        }.AsQueryable();

        var ordered = entry.ApplyOrder(scored, SortDirection.Desc).ToList();

        ordered[0].Score.Should().Be(0.75);
        ordered[1].Score.Should().Be(0.25);
    }

    [Fact]
    public void RelevanceEntry_ReadsTheTieBreakThroughTheScoredWrapper()
    {
        var entry = BuildCatalog().BuildRelevanceEntry();
        var id = Guid.CreateVersion7();
        var scored = new Scored<SearchRowFixture>
        {
            Row = new SearchRowFixture { Id = id },
            Score = 0.5,
        };

        entry.TieBreakOf(scored).Should().Be(id);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(1d)]
    [InlineData(0.1234567890123456)]
    [InlineData(1e-300)]
    [InlineData(1.7976931348623157e308)]
    [InlineData(-1e-300)]
    [InlineData(double.Epsilon)]
    public void RelevanceEntry_ScoreRoundTripsExactly(double score)
    {
        // A value that re-decodes even one unit away moves the page boundary
        // and silently drops or repeats rows across a page break.
        var entry = BuildCatalog().BuildRelevanceEntry();
        var wire = entry.EncodePrimary(
            new Scored<SearchRowFixture>
            {
                Row = new SearchRowFixture { Id = Guid.CreateVersion7() },
                Score = score,
            });

        double.Parse(wire, NumberStyles.Float, CultureInfo.InvariantCulture)
            .Should().Be(score);
    }

    [Fact]
    public void RelevanceEntry_ScoreIsEncodedInvariantOfTheCurrentCulture()
    {
        var entry = BuildCatalog().BuildRelevanceEntry();
        var previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var wire = entry.EncodePrimary(
                new Scored<SearchRowFixture>
            {
                Row = new SearchRowFixture { Id = Guid.CreateVersion7() },
                Score = 0.5,
            });

            wire.Should().Be("0.5");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [MemberData(nameof(EmptyStringRows))]
    [InlineData("  ")]
    [InlineData("not-a-number")]
    [InlineData("0x10")]
    public void RelevanceEntry_UndecodableScore_CursorKeyUndecodable(string wire)
    {
        var entry = BuildCatalog().BuildRelevanceEntry();
        var scored = new[]
        {
            new Scored<SearchRowFixture>
            {
                Row = new SearchRowFixture { Id = Guid.CreateVersion7() },
                Score = 0.5,
            },
        }.AsQueryable();

        var result = entry.ApplyCursor(scored, wire, Guid.CreateVersion7(), SortDirection.Desc);

        result.ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_KEY_UNDECODABLE);
    }

    [Fact]
    public void RelevanceEntry_CursorKeepsOnlyRowsPastTheBoundary()
    {
        var entry = BuildCatalog().BuildRelevanceEntry();
        var boundaryId = Guid.CreateVersion7();
        var afterId = Guid.CreateVersion7();

        var scored = new[]
        {
            new Scored<SearchRowFixture>
            {
                Row = new SearchRowFixture { Id = Guid.CreateVersion7() },
                Score = 0.9,
            },
            new Scored<SearchRowFixture>
            {
                Row = new SearchRowFixture { Id = boundaryId },
                Score = 0.5,
            },
            new Scored<SearchRowFixture>
            {
                Row = new SearchRowFixture { Id = afterId },
                Score = 0.1,
            },
        }.AsQueryable();

        var filtered = entry.ApplyCursor(scored, "0.5", boundaryId, SortDirection.Desc);

        filtered.Success.Should().BeTrue();
        filtered.Data!.Select(s => s.Score).Should().Equal([0.1]);
    }

    [Fact]
    public void Project_MaterializesTheDialectScoreOntoTheRow()
    {
        var catalog = BuildCatalog();
        var projection = catalog.Project(new FakeConstantDialect(0.42), sr_query).Compile();

        var scored = projection(new SearchRowFixture { Id = Guid.CreateVersion7(), Email = "ada" });

        scored.Score.Should().Be(0.42);
        scored.Row.Email.Should().Be("ada");
    }

    [Fact]
    public void Project_NullArguments_Throw()
    {
        var catalog = BuildCatalog();

        var nullDialect = () => catalog.Project(null!, sr_query);
        var nullQuery = () => catalog.Project(new FakeConstantDialect(1), null!);

        nullDialect.Should().Throw<ArgumentNullException>();
        nullQuery.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Catalog_ServesConcurrentReadersWithoutInterference()
    {
        var catalog = BuildCatalog();

        var tasks = Enumerable.Range(0, 64).Select(i => Task.Run(() =>
        {
            var dialect = new FakeConstantDialect(i);
            var projection = catalog.Project(dialect, sr_query).Compile();
            var scored = projection(new SearchRowFixture { Id = Guid.CreateVersion7() });

            _ = catalog.BuildRelevanceEntry();
            return (i, scored.Score);
        }));

        var results = await Task.WhenAll(tasks);

        foreach (var (i, score) in results)
            score.Should().Be(i);

        catalog.RankFields.Should().HaveCount(2);
    }

    [Fact]
    public void SearchIndexDescriptor_CarriesPropertyNamesAndWeightsPositionally()
    {
        var descriptor = new SearchIndexDescriptor(
            "SearchBlob",
            ["Email", "Username"],
            [2d, 1d]);

        descriptor.BlobProperty.Should().Be("SearchBlob");
        descriptor.SourceProperties.Should().Equal(["Email", "Username"]);
        descriptor.Weights.Should().Equal([2d, 1d]);
    }

    [Fact]
    public void SearchRankField_CarriesItsFieldAndWeight()
    {
        Expression<Func<SearchRowFixture, string>> field = r => r.Email;
        var rank = new SearchRankField<SearchRowFixture>(field, 3d);

        rank.Field.Should().BeSameAs(field);
        rank.Weight.Should().Be(3d);
    }

    [Fact]
    public void Scored_CarriesItsRowAndScore()
    {
        var row = new SearchRowFixture { Id = Guid.CreateVersion7() };
        var scored = new Scored<SearchRowFixture> { Row = row, Score = 0.5 };

        scored.Row.Should().BeSameAs(row);
        scored.Score.Should().Be(0.5);
    }

    [Fact]
    public void Scored_RowIsARequiredMember()
    {
        var row = typeof(Scored<SearchRowFixture>)
            .GetProperty(nameof(Scored<SearchRowFixture>.Row));

        row.Should().NotBeNull();
        row.IsDefined(typeof(RequiredMemberAttribute), inherit: false).Should().BeTrue();
    }

    [Fact]
    public void Restrict_KeepsOnlyTheRowsThePredicateAdmits()
    {
        var keep = Guid.CreateVersion7();
        var rows = new[]
        {
            new SearchRowFixture { Id = keep },
            new SearchRowFixture { Id = Guid.CreateVersion7() },
        }.AsQueryable();

        var reached = SearchReach<SearchRowFixture>
            .Restrict(r => r.Id == keep)
            .Apply(rows)
            .ToList();

        reached.Should().ContainSingle();
        reached[0].Id.Should().Be(keep);
    }

    [Fact]
    public void Unrestricted_KeepsEveryRow()
    {
        var rows = new[]
        {
            new SearchRowFixture { Id = Guid.CreateVersion7() },
            new SearchRowFixture { Id = Guid.CreateVersion7() },
        }.AsQueryable();

        SearchReach<SearchRowFixture>.Unrestricted().Apply(rows).Should().HaveCount(2);
    }

    [Fact]
    public void Reach_NullArguments_Throw()
    {
        var nullPredicate = () => SearchReach<SearchRowFixture>.Restrict(null!);
        var nullQuery = () => SearchReach<SearchRowFixture>.Unrestricted().Apply(null!);

        nullPredicate.Should().Throw<ArgumentNullException>();
        nullQuery.Should().Throw<ArgumentNullException>();
    }

    private static SearchCatalog<SearchRowFixture> BuildCatalog() =>
        new SearchCatalogBuilder<SearchRowFixture>()
            .Blob(r => r.SearchBlob)
            .RankField(r => r.Email, 2)
            .RankField(r => r.Username, 1)
            .TieBreak(r => r.Id)
            .Build();

    /// <summary>A page row with the shape a searching operation emits.</summary>
    private sealed class SearchRowFixture
    {
        public Guid Id { get; init; }

        public string Email { get; init; } = string.Empty;

        public string Username { get; init; } = string.Empty;

        public string SearchBlob { get; init; } = string.Empty;
    }

    /// <summary>
    /// A dialect standing in for a provider leaf: it scores every row the same
    /// so the test observes the neutral half's order of operations.
    /// </summary>
    private sealed class FakeConstantDialect(double score) : ISearchDialect<SearchRowFixture>
    {
        public Expression<Func<SearchRowFixture, bool>> Filter(
            Expression<Func<SearchRowFixture, string>> blob,
            SearchQuery query) => _ => true;

        public Expression<Func<SearchRowFixture, double>> Score(
            IReadOnlyList<SearchRankField<SearchRowFixture>> rankFields,
            SearchQuery query) => _ => score;
    }
}
