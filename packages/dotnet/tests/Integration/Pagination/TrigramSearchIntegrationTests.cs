// -----------------------------------------------------------------------
// <copyright file="TrigramSearchIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Pagination;

using AwesomeAssertions;
using DcsvIo.D2.Pagination;
using DcsvIo.D2.Pagination.EntityFrameworkCore;
using DcsvIo.D2.Pagination.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Pagination.Errors;
using DcsvIo.D2.Tests.Integration.DataGovernance;
using DcsvIo.D2.Tests.Integration.Pagination.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// The PostgreSQL leaf against a real PostgreSQL: the generated blob column
/// the database computes, the trigram index it is declared on, the pattern the
/// dialect renders, and the score-keyed cursor paging over the result.
/// </summary>
/// <remarks>
/// Everything here is provider behavior the unit suite cannot observe. The
/// unit suite runs on an in-memory provider that translates nothing and on a
/// model that never opens a connection, so a computed-column expression the
/// database rejects, a pattern that does not translate, and a floating-point
/// page boundary that does not re-evaluate to the same value all read as
/// correct there.
/// </remarks>
[Collection("Postgres")]
[Trait("Category", "Integration")]
public sealed class TrigramSearchIntegrationTests : IAsyncLifetime
{
    /// <summary>
    /// How many seeded rows carry an email address, and so how many
    /// the term the relevance walk uses is expected to reach.
    /// </summary>
    private const int _EMAIL_ROW_COUNT = 5;

    private static readonly Guid sr_reachA = Guid.CreateVersion7();
    private static readonly Guid sr_reachB = Guid.CreateVersion7();

    /// <summary>
    /// The row whose display name carries a tab and a double space, so the
    /// collapse the blob applies is observable against it.
    /// </summary>
    private static readonly Guid sr_spacedRowId = Guid.CreateVersion7();

    /// <summary>
    /// The row whose display name is separated by U+00A0, one of the two
    /// characters the provider's own whitespace shorthand does not collapse.
    /// </summary>
    private static readonly Guid sr_nonBreakingSpaceRowId = Guid.CreateVersion7();

    /// <summary>The row separated by U+202F, the other one.</summary>
    private static readonly Guid sr_narrowSpaceRowId = Guid.CreateVersion7();

    /// <summary>
    /// The row the relevance term matches strongly: the term is most of its
    /// display name, which is the field carrying the larger rank weight.
    /// </summary>
    private static readonly Guid sr_strongMatchRowId = Guid.CreateVersion7();

    /// <summary>
    /// The row the same term matches weakly: it contains the term, so it
    /// survives the filter, and it is buried in a display name long enough that
    /// its similarity is far lower.
    /// </summary>
    private static readonly Guid sr_weakMatchRowId = Guid.CreateVersion7();

    private readonly PostgresFixture r_fixture;
    private string r_connectionString = string.Empty;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="TrigramSearchIntegrationTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared Postgres Testcontainers fixture.</param>
    public TrigramSearchIntegrationTests(PostgresFixture fixture)
    {
        r_fixture = fixture;
    }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        r_connectionString =
            await r_fixture.CreateIsolatedDatabaseAsync(nameof(TrigramSearchIntegrationTests));

        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);
        await ctx.Database.EnsureCreatedAsync();

        Seed(ctx);
        await ctx.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() =>
        await r_fixture.AssertNoConnectionOpenAsync(r_connectionString);

    // =========================================================================
    // The generated column: the database computes it, from the sources, in the
    // form the query it is matched against is normalized into.
    // =========================================================================

    [Fact]
    public async Task SearchBlob_IsComputedByTheDatabaseFromEverySource()
    {
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        var row = await ctx.Members.AsNoTracking()
            .SingleAsync(e => e.DisplayName == "Ada Lovelace");

        row.SearchBlob.Should().Be("Ada Lovelace ada@example.com");
    }

    [Fact]
    public async Task SearchBlob_AbsentSourceContributesNothingAndDoesNotDoubleItsSeparator()
    {
        // A plain concatenation yields null for the whole expression when any
        // operand is null, which would silently empty the blob for every row
        // missing one optional field - the rows most likely to be searched for.
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        var row = await ctx.Members.AsNoTracking()
            .SingleAsync(e => e.DisplayName == "Katherine Johnson");

        row.Email.Should().BeNull();
        row.SearchBlob.Should().Be("Katherine Johnson");
    }

    [Fact]
    public async Task SearchBlob_CollapsesWhitespaceRunsInsideASource()
    {
        // The collapse is the same one query normalization applies, and it only
        // works applied to both sides.
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        var row = await ctx.Members.AsNoTracking()
            .SingleAsync(e => e.Id == sr_spacedRowId);

        row.DisplayName.Should().Be("Grace \t Hopper");
        row.SearchBlob.Should().Be("Grace Hopper grace@example.com");
    }

    [Fact]
    public async Task SearchBlob_CollapsesTheWhitespaceTheProviderShorthandWouldHaveKept()
    {
        // The column's character class and char.IsWhiteSpace have to admit the
        // same characters, and the provider's own shorthand does not admit
        // these two. A row keeping one verbatim in its blob while the caller's
        // identical text had it collapsed is a row unfindable by its own text.
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        var nonBreaking = await ctx.Members.AsNoTracking()
            .SingleAsync(e => e.Id == sr_nonBreakingSpaceRowId);
        var narrow = await ctx.Members.AsNoTracking()
            .SingleAsync(e => e.Id == sr_narrowSpaceRowId);

        nonBreaking.SearchBlob.Should().Be("Rosalind Franklin");
        narrow.SearchBlob.Should().Be("Barbara McClintock");
    }

    [Fact]
    public async Task ToTrigramSearchPageAsync_FindsARowSeparatedByANonBreakingSpace()
    {
        // The caller types an ordinary space, because that is what a keyboard
        // gives them. The row was stored with U+00A0 and U+202F.
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        var nonBreaking = await Page(ctx, new PageRequest(null, 20, Query: "rosalind franklin"));
        var narrow = await Page(ctx, new PageRequest(null, 20, Query: "barbara mcclintock"));

        nonBreaking.Success.Should().BeTrue();
        nonBreaking.Data!.Items.Should().ContainSingle();
        nonBreaking.Data.Items[0].Id.Should().Be(sr_nonBreakingSpaceRowId);

        narrow.Success.Should().BeTrue();
        narrow.Data!.Items.Should().ContainSingle();
        narrow.Data.Items[0].Id.Should().Be(sr_narrowSpaceRowId);
    }

    [Fact]
    public async Task SearchBlob_IsStoredUnfolded()
    {
        // Case-insensitivity is the dialect's job. A folded column plus a
        // folded query is two independently written transforms that have to
        // agree, and outside the ASCII range they do not.
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        var blobs = await ctx.Members.AsNoTracking()
            .Select(e => e.SearchBlob).ToListAsync();

        blobs.Should().Contain(b => b.StartsWith("Ada", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SearchBlob_IsIndexedWithTheTrigramOperatorClass()
    {
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        var definition = await ScalarAsync(
            ctx,
            "SELECT indexdef FROM pg_indexes WHERE tablename = '"
            + TrigramMemberDbContextFixture.TABLE
            + "' AND indexdef LIKE '%"
            + TrigramMemberDbContextFixture.BLOB_COLUMN
            + "%'");

        definition.Should().NotBeNull();
        definition.Should().Contain(SearchBlobModelBuilderExtensions.INDEX_METHOD);
        definition.Should().Contain(SearchBlobModelBuilderExtensions.INDEX_OPERATOR_CLASS);
    }

    // =========================================================================
    // The dialect against the provider: the pattern translates, it is
    // case-insensitive, and its escape clause holds.
    // =========================================================================

    [Fact]
    public async Task ToTrigramSearchPageAsync_MatchesOnTheGeneratedColumn()
    {
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        var page = await Page(ctx, new PageRequest(null, 20, Query: "lovelace"));

        page.Success.Should().BeTrue();
        page.Data!.Items.Should().ContainSingle();
        page.Data.Items[0].DisplayName.Should().Be("Ada Lovelace");
    }

    [Fact]
    public async Task ToTrigramSearchPageAsync_MatchesRegardlessOfCase()
    {
        // The pairing the design requires: an unfolded column, an
        // unfolded query, and a case-insensitive operator between them.
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        var lower = await Page(ctx, new PageRequest(null, 20, Query: "lovelace"));
        var upper = await Page(ctx, new PageRequest(null, 20, Query: "LOVELACE"));
        var mixed = await Page(ctx, new PageRequest(null, 20, Query: "LoVeLaCe"));

        lower.Data!.TotalCount.Should().Be(1);
        upper.Data!.TotalCount.Should().Be(1);
        mixed.Data!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task ToTrigramSearchPageAsync_MatchesTheSingleSpacedTextACallerTypes()
    {
        // The stored row carries a tab and a double space; the caller types one
        // space. Both sides collapse, so the row is found.
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        var page = await Page(ctx, new PageRequest(null, 20, Query: "grace hopper"));

        page.Success.Should().BeTrue();
        page.Data!.Items.Should().ContainSingle();
        page.Data.Items[0].Id.Should().Be(sr_spacedRowId);
    }

    [Fact]
    public async Task ToTrigramSearchPageAsync_MatchesARowByTheSourceItDoesHave()
    {
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        var page = await Page(ctx, new PageRequest(null, 20, Query: "johnson"));

        page.Success.Should().BeTrue();
        page.Data!.Items.Should().ContainSingle();
        page.Data.Items[0].DisplayName.Should().Be("Katherine Johnson");
    }

    [Fact]
    public async Task ToTrigramSearchPageAsync_WildcardQueryMatchesLiterallyRatherThanEverything()
    {
        // Without the escape clause a query of pattern metacharacters matches
        // every row instead of the rows whose text contains those characters.
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        var page = await Page(ctx, new PageRequest(null, 20, Query: "1%_max"));

        page.Success.Should().BeTrue();
        page.Data!.TotalCount.Should().Be(1);
        page.Data.Items[0].DisplayName.Should().Be("Percent 1%_max Row");
    }

    [Fact]
    public async Task ToTrigramSearchPageAsync_AppliesTheReachPredicate()
    {
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        var page = await ctx.PageRows().ToTrigramSearchPageAsync(
            new PageRequest(null, 20, Query: "example.com"),
            new TrigramMemberDescriptorFixture(),
            SearchReach<TrigramMemberPageRowFixture>.Restrict(r => r.ReachId == sr_reachA),
            ct: TestContext.Current.CancellationToken);

        page.Success.Should().BeTrue();
        page.Data!.Items.Should().NotBeEmpty();
        page.Data.Items.Should().OnlyContain(r => r.ReachId == sr_reachA);
    }

    // =========================================================================
    // Relevance: the score the database computes, and the cursor minted from it.
    // =========================================================================

    [Fact]
    public async Task ToTrigramSearchPageAsync_RelevanceOrdersTheStrongerMatchFirst()
    {
        // Both rows contain the term, so both survive the filter and the order
        // is decided by the score alone. The first is the term and little else;
        // the second buries it in a long display name, so its similarity on the
        // heavier-weighted field is far lower.
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        var page = await Page(ctx, new PageRequest(null, 20, Query: "curie"));

        page.Success.Should().BeTrue();
        page.Data!.AppliedSort.Field.Should().Be(SortFieldNames.RELEVANCE);
        page.Data.AppliedSort.Direction.Should().Be(SortDirection.Desc);
        page.Data.Items.Select(r => r.Id).Should()
            .Equal([sr_strongMatchRowId, sr_weakMatchRowId]);
    }

    [Fact]
    public async Task ToTrigramSearchPageAsync_RelevancePagesEveryRowExactlyOnce()
    {
        // The in-memory provider cannot give a correct result for this case.
        // The relevance page orders by a score the database computes and mints
        // a cursor carrying that score, and the next page re-evaluates the
        // same expression and compares it for equality at the boundary. A
        // score that does not re-evaluate bit-identically drops or repeats a
        // row right there.
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        var seen = new List<Guid>();
        string? cursor = null;

        // One more attempt than there are rows, so a walk that fails to
        // terminate is reported as a walk that failed to terminate rather than
        // as a count that happened to match.
        for (var page = 0; page <= _EMAIL_ROW_COUNT; page++)
        {
            var result = await Page(ctx, new PageRequest(cursor, 1, Query: "example.com"));

            result.Success.Should().BeTrue();
            seen.AddRange(result.Data!.Items.Select(r => r.Id));
            cursor = result.Data.NextCursor;

            if (cursor is null)
                break;
        }

        cursor.Should().BeNull("the walk must terminate rather than run out of attempts");
        seen.Should().OnlyHaveUniqueItems();
        seen.Should().HaveCount(_EMAIL_ROW_COUNT);
    }

    [Fact]
    public async Task ToTrigramSearchPageAsync_TiedScoresPageWithoutRepeatingOrDroppingRows()
    {
        // Two rows whose only searchable text is identical score identically,
        // so the page boundary falls on the tie-break rather than the score.
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        var seen = new List<Guid>();
        string? cursor = null;

        for (var page = 0; page < 6; page++)
        {
            var result = await Page(ctx, new PageRequest(cursor, 1, Query: "identical"));

            result.Success.Should().BeTrue();
            seen.AddRange(result.Data!.Items.Select(r => r.Id));
            cursor = result.Data.NextCursor;

            if (cursor is null)
                break;
        }

        cursor.Should().BeNull();
        seen.Should().OnlyHaveUniqueItems();
        seen.Should().HaveCount(2);
    }

    [Fact]
    public async Task ToTrigramSearchPageAsync_RelevanceCursorWithNoQuery_RelevanceRequiresQuery()
    {
        // A caller-constructible cursor: the codec is unauthenticated, so the
        // sort field it contains is caller-supplied input, and relevance is never
        // registered in the sort catalog.
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        var first = await Page(ctx, new PageRequest(null, 1, Query: "example.com"));
        first.Data!.NextCursor.Should().NotBeNullOrWhiteSpace();

        var replayed = await Page(ctx, new PageRequest(first.Data.NextCursor, 1));

        replayed.ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_RELEVANCE_REQUIRES_QUERY);
        replayed.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ToTrigramSearchPageAsync_SortedByARegisteredField_PagesTheQueryDirectly()
    {
        await using var ctx = TrigramMemberDbContextFixture.Build(r_connectionString);

        var page = await Page(
            ctx,
            new PageRequest(
                null,
                20,
                Sort: "displayName",
                Order: SortDirectionWire.ASC,
                Query: "example.com"));

        page.Success.Should().BeTrue();
        page.Data!.AppliedSort.Field.Should().Be("displayName");

        // The database orders text, not .NET. This is the pinned engine's en_US.utf8
        // ordering, where case and the embedded tab carry no primary weight, so
        // "identical" precedes "Percent 1%_max Row". An ordinal comparison puts those
        // two the other way round, which is why this states the sequence instead of
        // asserting ascending against a .NET comparer.
        page.Data.Items.Select(r => r.DisplayName).Should().Equal(
            "Ada Lovelace",
            "Grace \t Hopper",
            "identical",
            "identical",
            "Percent 1%_max Row");
    }

    private static async ValueTask<
        D2.Result.D2Result<PageResult<TrigramMemberPageRowFixture>?>> Page(
        TrigramMemberDbContextFixture ctx,
        PageRequest request) =>
        await ctx.PageRows().ToTrigramSearchPageAsync(
            request,
            new TrigramMemberDescriptorFixture(),
            SearchReach<TrigramMemberPageRowFixture>.Unrestricted(),
            ct: TestContext.Current.CancellationToken);

    private static async Task<string?> ScalarAsync(TrigramMemberDbContextFixture ctx, string sql)
    {
        await using var command = ctx.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;

        await ctx.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);

        try
        {
            var value = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            return value as string;
        }
        finally
        {
            await ctx.Database.CloseConnectionAsync();
        }
    }

    private static void Seed(TrigramMemberDbContextFixture ctx)
    {
        // The rows carrying an email address at example.com are the
        // ones "example.com" reaches, and _EMAIL_ROW_COUNT counts them. Two of
        // them carry identical searchable text, so a score tie is reachable.
        // Every row seeded for another purpose below takes an address at a
        // different domain, so that count stays the property it states.
        Add(ctx, Guid.CreateVersion7(), sr_reachA, "Ada Lovelace", "ada@example.com");
        Add(ctx, sr_spacedRowId, sr_reachA, "Grace \t Hopper", "grace@example.com");
        Add(ctx, Guid.CreateVersion7(), sr_reachB, "Percent 1%_max Row", "pct@example.com");
        Add(ctx, Guid.CreateVersion7(), sr_reachB, "identical", "same@example.com");
        Add(ctx, Guid.CreateVersion7(), sr_reachB, "identical", "same@example.com");
        Add(ctx, Guid.CreateVersion7(), sr_reachA, "Katherine Johnson", null);

        // The two characters char.IsWhiteSpace collapses and the provider's
        // whitespace shorthand does not.
        Add(ctx, sr_nonBreakingSpaceRowId, sr_reachA, "Rosalind\u00A0Franklin", null);
        Add(ctx, sr_narrowSpaceRowId, sr_reachA, "Barbara\u202FMcClintock", null);

        // Two rows one term reaches with visibly different strength.
        Add(ctx, sr_strongMatchRowId, sr_reachA, "Marie Curie", "curie@lab.test");
        Add(
            ctx,
            sr_weakMatchRowId,
            sr_reachB,
            "Aeronautical Engineering Directorate Curie Annex",
            "annex@lab.test");
    }

    private static void Add(
        TrigramMemberDbContextFixture ctx,
        Guid id,
        Guid reachId,
        string displayName,
        string? email) =>
        ctx.Members.Add(new TrigramMemberEntityFixture
        {
            Id = id,
            ReachId = reachId,
            DisplayName = displayName,
            Email = email,
        });
}
