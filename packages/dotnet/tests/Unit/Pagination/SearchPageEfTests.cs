// -----------------------------------------------------------------------
// <copyright file="SearchPageEfTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Pagination;

using System.Linq.Expressions;
using AwesomeAssertions;
using DcsvIo.D2.Pagination;
using DcsvIo.D2.Pagination.EntityFrameworkCore;
using DcsvIo.D2.Pagination.Errors;
using DcsvIo.D2.Result;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// The provider-neutral search pipeline: the reach fence, the order of
/// operations, the relevance projection and the cursor binding.
/// </summary>
public sealed class SearchPageEfTests
{
    private static readonly Guid sr_reachA = Guid.CreateVersion7();

    private static readonly Guid sr_reachB = Guid.CreateVersion7();

    private static ISearchableListPageDescriptor<SearchPageRowFixture> SearchDescriptor =>
        new SearchDescriptorFixture();

    [Fact]
    public async Task ToSearchPageAsync_NoQuery_BehavesAsTheNonSearchingPath()
    {
        await using var db = CreateDb();
        Seed(db);
        await db.SaveChangesAsync();

        var page = await Rows(db).ToSearchPageAsync(
            new PageRequest(null, 10),
            SearchDescriptor,
            SearchReach<SearchPageRowFixture>.Unrestricted(),
            new FakeSubstringDialect(),
            ct: CancellationToken.None);

        page.Success.Should().BeTrue();
        page.Data!.AppliedSort.Field.Should().Be("name");
        page.Data.TotalCount.Should().Be(6);
        page.Data.Items.Select(r => r.Name).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task ToSearchPageAsync_NoQuery_AppliesTheReachPredicate()
    {
        await using var db = CreateDb();
        Seed(db);
        await db.SaveChangesAsync();

        var page = await Rows(db).ToSearchPageAsync(
            new PageRequest(null, 10),
            SearchDescriptor,
            SearchReach<SearchPageRowFixture>.Restrict(r => r.ReachId == sr_reachA),
            new FakeSubstringDialect(),
            ct: CancellationToken.None);

        page.Success.Should().BeTrue();
        page.Data!.Items.Should().OnlyContain(r => r.ReachId == sr_reachA);
        page.Data.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task ToSearchPageAsync_Query_OrdersByRelevanceWithNoSortField()
    {
        await using var db = CreateDb();
        Seed(db);
        await db.SaveChangesAsync();

        var page = await Rows(db).ToSearchPageAsync(
            new PageRequest(null, 10, Query: "ada"),
            SearchDescriptor,
            SearchReach<SearchPageRowFixture>.Unrestricted(),
            new FakeSubstringDialect(),
            ct: CancellationToken.None);

        page.Success.Should().BeTrue();
        page.Data!.AppliedSort.Field.Should().Be(SortFieldNames.RELEVANCE);
        page.Data.AppliedSort.Direction.Should().Be(SortDirection.Desc);
        page.Data.Items.Should().OnlyContain(r => r.SearchBlob.Contains("ada"));
    }

    [Fact]
    public async Task ToSearchPageAsync_Query_CountsMatchesRatherThanReachableRows()
    {
        await using var db = CreateDb();
        Seed(db);
        await db.SaveChangesAsync();

        var page = await Rows(db).ToSearchPageAsync(
            new PageRequest(null, 10, Query: "ada"),
            SearchDescriptor,
            SearchReach<SearchPageRowFixture>.Unrestricted(),
            new FakeSubstringDialect(),
            ct: CancellationToken.None);

        page.Success.Should().BeTrue();
        page.Data!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task ToSearchPageAsync_Query_AppliesTheReachPredicateBeforeMatching()
    {
        await using var db = CreateDb();
        Seed(db);
        await db.SaveChangesAsync();

        var page = await Rows(db).ToSearchPageAsync(
            new PageRequest(null, 10, Query: "ada"),
            SearchDescriptor,
            SearchReach<SearchPageRowFixture>.Restrict(r => r.ReachId == sr_reachA),
            new FakeSubstringDialect(),
            ct: CancellationToken.None);

        page.Success.Should().BeTrue();
        page.Data!.Items.Should().OnlyContain(r => r.ReachId == sr_reachA);
        page.Data.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task ToSearchPageAsync_ReachHoldsOnTheCursorPage()
    {
        // The key case: a fence applied to the first page and dropped on the
        // second can be bypassed by a paginating caller.
        await using var db = CreateDb();
        Seed(db);
        await db.SaveChangesAsync();

        var reach = SearchReach<SearchPageRowFixture>.Restrict(r => r.ReachId == sr_reachA);

        var first = await Rows(db).ToSearchPageAsync(
            new PageRequest(null, 1, Query: "ace"),
            SearchDescriptor,
            reach,
            new FakeSubstringDialect(),
            ct: CancellationToken.None);

        first.Success.Should().BeTrue();
        first.Data!.Items.Should().OnlyContain(r => r.ReachId == sr_reachA);
        first.Data.NextCursor.Should().NotBeNullOrWhiteSpace();

        var second = await Rows(db).ToSearchPageAsync(
            new PageRequest(first.Data.NextCursor, 10, Query: "ace"),
            SearchDescriptor,
            reach,
            new FakeSubstringDialect(),
            ct: CancellationToken.None);

        second.Success.Should().BeTrue();
        second.Data!.Items.Should().NotBeEmpty();
        second.Data.Items.Should().OnlyContain(r => r.ReachId == sr_reachA);
        second.Data.Items.Select(r => r.Id)
            .Should().NotIntersectWith(first.Data.Items.Select(r => r.Id));
    }

    [Fact]
    public async Task ToSearchPageAsync_CursorDoesNotSurviveAChangedQuery()
    {
        await using var db = CreateDb();
        Seed(db);
        await db.SaveChangesAsync();

        var first = await Rows(db).ToSearchPageAsync(
            new PageRequest(null, 1, Query: "ada"),
            SearchDescriptor,
            SearchReach<SearchPageRowFixture>.Unrestricted(),
            new FakeSubstringDialect(),
            ct: CancellationToken.None);

        first.Success.Should().BeTrue();
        first.Data!.NextCursor.Should().NotBeNullOrWhiteSpace();

        var second = await Rows(db).ToSearchPageAsync(
            new PageRequest(first.Data.NextCursor, 1, Query: "grace"),
            SearchDescriptor,
            SearchReach<SearchPageRowFixture>.Unrestricted(),
            new FakeSubstringDialect(),
            ct: CancellationToken.None);

        second.ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_QUERY_MISMATCH);
        second.Data.Should().BeNull();
    }

    [Fact]
    public async Task ToSearchPageAsync_RelevanceCursorPagesWithoutRepeatingOrDroppingRows()
    {
        await using var db = CreateDb();
        Seed(db);
        await db.SaveChangesAsync();

        var seen = new List<Guid>();
        string? cursor = null;

        for (var page = 0; page < 8; page++)
        {
            var result = await Rows(db).ToSearchPageAsync(
                new PageRequest(cursor, 1, Query: "ace"),
                SearchDescriptor,
                SearchReach<SearchPageRowFixture>.Unrestricted(),
                new FakeSubstringDialect(),
                ct: CancellationToken.None);

            result.Success.Should().BeTrue();
            seen.AddRange(result.Data!.Items.Select(r => r.Id));
            cursor = result.Data.NextCursor;

            if (cursor is null)
                break;
        }

        cursor.Should().BeNull();
        seen.Should().OnlyHaveUniqueItems();
        seen.Should().HaveCount(5);
    }

    [Fact]
    public async Task ToSearchPageAsync_SortedByARegisteredField_PagesTheQueryDirectly()
    {
        await using var db = CreateDb();
        Seed(db);
        await db.SaveChangesAsync();

        var page = await Rows(db).ToSearchPageAsync(
            new PageRequest(null, 10, Sort: "name", Order: SortDirectionWire.ASC, Query: "ada"),
            SearchDescriptor,
            SearchReach<SearchPageRowFixture>.Unrestricted(),
            new FakeSubstringDialect(),
            ct: CancellationToken.None);

        page.Success.Should().BeTrue();
        page.Data!.AppliedSort.Field.Should().Be("name");
        page.Data.Items.Select(r => r.Name).Should().BeInAscendingOrder();
        page.Data.Items.Should().OnlyContain(r => r.SearchBlob.Contains("ada"));
    }

    [Fact]
    public async Task ToSearchPageAsync_AscendingRelevance_SortDirectionNotAllowed()
    {
        await using var db = CreateDb();

        var page = await Rows(db).ToSearchPageAsync(
            new PageRequest(
                null,
                10,
                Sort: SortFieldNames.RELEVANCE,
                Order: SortDirectionWire.ASC,
                Query: "ada"),
            SearchDescriptor,
            SearchReach<SearchPageRowFixture>.Unrestricted(),
            new FakeSubstringDialect(),
            ct: CancellationToken.None);

        page.ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_SORT_DIRECTION_NOT_ALLOWED);
    }

    [Fact]
    public async Task ToSearchPageAsync_RelevanceWithNoQuery_RelevanceRequiresQuery()
    {
        await using var db = CreateDb();

        var page = await Rows(db).ToSearchPageAsync(
            new PageRequest(null, 10, Sort: SortFieldNames.RELEVANCE),
            SearchDescriptor,
            SearchReach<SearchPageRowFixture>.Unrestricted(),
            new FakeSubstringDialect(),
            ct: CancellationToken.None);

        page.ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_RELEVANCE_REQUIRES_QUERY);
    }

    [Fact]
    public async Task ToSearchPageAsync_QueryBelowMinimumLength_SearchQueryTooShort()
    {
        await using var db = CreateDb();

        var page = await Rows(db).ToSearchPageAsync(
            new PageRequest(null, 10, Query: "ad"),
            SearchDescriptor,
            SearchReach<SearchPageRowFixture>.Unrestricted(),
            new FakeSubstringDialect(),
            ct: CancellationToken.None);

        page.ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_SEARCH_QUERY_TOO_SHORT);
    }

    [Fact]
    public async Task ToSearchPageAsync_CatalogSharesNoFieldWithAllowSet_SortCatalogEmpty()
    {
        await using var db = CreateDb();

        var page = await Rows(db).ToSearchPageAsync(
            new PageRequest(null, 10),
            new SearchDescriptorFixture(
                SortFieldAllowSet.From(
                    new SortFieldSpec("somethingElse", AllowedDirections.Both))),
            SearchReach<SearchPageRowFixture>.Unrestricted(),
            new FakeSubstringDialect(),
            ct: CancellationToken.None);

        page.ErrorCode.Should().Be(PaginationErrorCodes.LIB_PAGINATION_SORT_CATALOG_EMPTY);
    }

    [Fact]
    public async Task ToSearchPageAsync_SuppliedCodec_IsUsedInsteadOfTheDefault()
    {
        await using var db = CreateDb();
        Seed(db);
        await db.SaveChangesAsync();

        var codec = new FakeRecordingCursorCodec();

        var page = await Rows(db).ToSearchPageAsync(
            new PageRequest(null, 1, Query: "ada"),
            SearchDescriptor,
            SearchReach<SearchPageRowFixture>.Unrestricted(),
            new FakeSubstringDialect(),
            codec,
            CancellationToken.None);

        page.Success.Should().BeTrue();
        page.Data!.NextCursor.Should().Be(FakeRecordingCursorCodec.WIRE);
        codec.Minted.Should().ContainSingle();
        codec.Minted[0].Sort.Should().Be(SortFieldNames.RELEVANCE);
        codec.Minted[0].QueryToken.Should()
            .Be(SearchQuery.Create("ada", new SearchPolicy()).Data!.QueryToken);
    }

    [Fact]
    public async Task ToSearchPageAsync_NullArguments_Throw()
    {
        await using var db = CreateDb();
        var query = Rows(db);

        var nullRequest = async () => await query.ToSearchPageAsync(
            null!,
            SearchDescriptor,
            SearchReach<SearchPageRowFixture>.Unrestricted(),
            new FakeSubstringDialect(),
            ct: CancellationToken.None);

        var nullDescriptor = async () => await query.ToSearchPageAsync(
            new PageRequest(null, 10),
            null!,
            SearchReach<SearchPageRowFixture>.Unrestricted(),
            new FakeSubstringDialect(),
            ct: CancellationToken.None);

        var nullReach = async () => await query.ToSearchPageAsync(
            new PageRequest(null, 10),
            SearchDescriptor,
            null!,
            new FakeSubstringDialect(),
            ct: CancellationToken.None);

        var nullDialect = async () => await query.ToSearchPageAsync(
            new PageRequest(null, 10),
            SearchDescriptor,
            SearchReach<SearchPageRowFixture>.Unrestricted(),
            null!,
            ct: CancellationToken.None);

        await nullRequest.Should().ThrowAsync<ArgumentNullException>();
        await nullDescriptor.Should().ThrowAsync<ArgumentNullException>();
        await nullReach.Should().ThrowAsync<ArgumentNullException>();
        await nullDialect.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ToSearchPageAsync_CanceledToken_Propagates()
    {
        await using var db = CreateDb();
        Seed(db);
        await db.SaveChangesAsync();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Capture the token and the query, not the context or the source that
        // own them - both are disposed by this scope.
        var canceled = cts.Token;
        var query = Rows(db);

        var act = async () => await query.ToSearchPageAsync(
            new PageRequest(null, 10, Query: "ada"),
            SearchDescriptor,
            SearchReach<SearchPageRowFixture>.Unrestricted(),
            new FakeSubstringDialect(),
            ct: canceled);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ToSearchPageAsync_ConcurrentQueries_DoNotRankEachOthersPages()
    {
        // The failure this guards is one caller's query ranking another
        // caller's page on a listing whose rows the reach predicate bounds.
        var databaseName = Guid.NewGuid().ToString("N");

        await using (var seedDb = CreateDb(databaseName))
        {
            Seed(seedDb);
            await seedDb.SaveChangesAsync();
        }

        var descriptor = SearchDescriptor;
        var dialect = new FakeSubstringDialect();

        var tasks = Enumerable.Range(0, 32).Select(i =>
        {
            var searchesAda = i % 2 == 0;
            var term = searchesAda ? "ada" : "grace";
            var reachId = searchesAda ? sr_reachA : sr_reachB;

            return Task.Run(async () =>
            {
                await using var db = CreateDb(databaseName);

                var page = await Rows(db).ToSearchPageAsync(
                    new PageRequest(null, 10, Query: term),
                    descriptor,
                    SearchReach<SearchPageRowFixture>.Restrict(r => r.ReachId == reachId),
                    dialect,
                    ct: CancellationToken.None);

                page.Success.Should().BeTrue();
                return (term, reachId, rows: page.Data!.Items.ToArray());
            });
        });

        var results = await Task.WhenAll(tasks);

        foreach (var (term, reachId, rows) in results)
        {
            rows.Should().OnlyContain(r => r.ReachId == reachId);
            rows.Should().OnlyContain(r => r.SearchBlob.Contains(term));
        }
    }

    private static IQueryable<SearchPageRowFixture> Rows(SearchDbFixture db) =>
        db.SearchRows.AsNoTracking().Select(e => new SearchPageRowFixture(
            e.Id,
            e.ReachId,
            e.Name,
            e.SearchBlob));

    private static SearchDbFixture CreateDb(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<SearchDbFixture>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString("N"))
            .Options;

        return new SearchDbFixture(options);
    }

    private static void Seed(SearchDbFixture db)
    {
        // "ace" matches every row but the last; "ada" matches one row per
        // reach value, so the reach fence and the match filter are separable.
        Add(db, sr_reachA, "alpha", "ada lovelace");
        Add(db, sr_reachA, "bravo", "grace hopper");
        Add(db, sr_reachA, "charlie", "trace clarke");
        Add(db, sr_reachB, "delta", "ada peace");
        Add(db, sr_reachB, "echo", "grace murray");
        Add(db, sr_reachB, "foxtrot", "katherine johnson");
    }

    private static void Add(SearchDbFixture db, Guid reachId, string name, string blob) =>
        db.SearchRows.Add(new SearchEntityFixture
        {
            Id = Guid.CreateVersion7(),
            ReachId = reachId,
            Name = name,
            SearchBlob = blob,
        });

    /// <summary>The page row a searching operation emits, blob column included.</summary>
    public sealed record SearchPageRowFixture(
        Guid Id,
        Guid ReachId,
        string Name,
        string SearchBlob);

    /// <summary>
    /// The descriptor shape generated code implements, standing in for the
    /// emitted aggregate until a contract declares a searching operation.
    /// </summary>
    private sealed class SearchDescriptorFixture(SortFieldAllowSet? allowedSortFields = null)
        : ISearchableListPageDescriptor<SearchPageRowFixture>
    {
        private static readonly SortFieldAllowSet sr_allow = SortFieldAllowSet.From(
            new SortFieldSpec("name", AllowedDirections.Both),
            new SortFieldSpec(SortFieldNames.RELEVANCE, AllowedDirections.Descending));

        private static readonly SortCatalog<SearchPageRowFixture> sr_sortCatalog =
            new SortCatalog<SearchPageRowFixture>()
                .Text(r => r.Name, r => r.Id);

        private static readonly SearchCatalog<SearchPageRowFixture> sr_searchCatalog =
            new SearchCatalogBuilder<SearchPageRowFixture>()
                .Blob(r => r.SearchBlob)
                .RankField(r => r.Name, 1)
                .TieBreak(r => r.Id)
                .Build();

        public ListPagingPolicy Policy { get; } =
            new(new SortSpec("name", SortDirection.Desc));

        public SortFieldAllowSet AllowedSortFields => allowedSortFields ?? sr_allow;

        public SortCatalog<SearchPageRowFixture> SortCatalog => sr_sortCatalog;

        public SearchPolicy SearchPolicy { get; } = new();

        public SearchCatalog<SearchPageRowFixture> SearchCatalog => sr_searchCatalog;

        public SearchIndexDescriptor SearchIndex { get; } =
            new("SearchBlob", ["Name"], [1d]);
    }

    /// <summary>
    /// A dialect standing in for the provider leaf: substring containment for
    /// the filter, and a score that falls with the match position so relevance
    /// ordering is observable without a database.
    /// </summary>
    private sealed class FakeSubstringDialect : ISearchDialect<SearchPageRowFixture>
    {
        public Expression<Func<SearchPageRowFixture, bool>> Filter(
            Expression<Func<SearchPageRowFixture, string>> blob,
            SearchQuery query) => r => r.SearchBlob.Contains(query.Normalized);

        public Expression<Func<SearchPageRowFixture, double>> Score(
            IReadOnlyList<SearchRankField<SearchPageRowFixture>> rankFields,
            SearchQuery query) =>
            r => 1.0 / (1 + r.SearchBlob.IndexOf(query.Normalized));
    }

    /// <summary>Records the cursors a page mints, so the binding is observable.</summary>
    private sealed class FakeRecordingCursorCodec : IKeysetCursorCodec
    {
        public const string WIRE = "recorded-cursor";

        private readonly List<KeysetCursor> r_minted = [];

        public IReadOnlyList<KeysetCursor> Minted => r_minted;

        public D2Result<string?> Encode(KeysetCursor cursor)
        {
            r_minted.Add(cursor);
            return D2Result<string?>.Ok(WIRE);
        }

        public D2Result<KeysetCursor?> Decode(string? wire) =>
            PaginationFailures<KeysetCursor?>.CursorInvalid();
    }

    private sealed class SearchDbFixture(DbContextOptions<SearchDbFixture> options)
        : DbContext(options)
    {
        public DbSet<SearchEntityFixture> SearchRows => Set<SearchEntityFixture>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<SearchEntityFixture>().HasKey(e => e.Id);
    }

    private sealed class SearchEntityFixture
    {
        public Guid Id { get; set; }

        public Guid ReachId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string SearchBlob { get; set; } = string.Empty;
    }
}
