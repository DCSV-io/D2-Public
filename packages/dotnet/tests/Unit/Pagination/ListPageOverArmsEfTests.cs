// -----------------------------------------------------------------------
// <copyright file="ListPageOverArmsEfTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Pagination;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Pagination;
using DcsvIo.D2.Pagination.EntityFrameworkCore;
using DcsvIo.D2.Pagination.EntityFrameworkCore.NodaTime;
using DcsvIo.D2.Pagination.Errors;
using DcsvIo.D2.Result;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Xunit;

/// <summary>
/// <c>ListPageEf.ToListPageOverArmsAsync</c> over queries that admit no row in
/// common (EF InMemory).
/// </summary>
public sealed class ListPageOverArmsEfTests
{
    private static readonly SortSpec sr_default =
        new(Field: "joinedAt", Direction: SortDirection.Desc);

    private static readonly ListPagingPolicy sr_policy =
        new(DefaultSort: sr_default, DefaultLimit: 50, MaxLimit: 100);

    private static readonly SortFieldAllowSet sr_allow =
        SortFieldAllowSet.BothDirections("joinedAt", "name");

    [Fact]
    public async Task ToListPageOverArmsAsync_NoArms_Throws()
    {
        var act = async () => await Array.Empty<IQueryable<ArmsSampleRow>>()
            .ToListPageOverArmsAsync(
                new PageRequest(null, null),
                sr_policy,
                Catalog(),
                sr_allow,
                ct: CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("arms");
    }

    /// <summary>
    /// An empty arm list is the caller's own mistake. The guard runs before the
    /// request is normalized, so this call throws even where the normalizer
    /// would have refused the request as well.
    /// </summary>
    /// <returns>A task that completes when the call has thrown.</returns>
    [Fact]
    public async Task ToListPageOverArmsAsync_NoArmsAndARequestTheNormalizerRefuses_Throws()
    {
        var act = async () => await Array.Empty<IQueryable<ArmsSampleRow>>()
            .ToListPageOverArmsAsync(
                new PageRequest(null, 10, "joinedAt", SortDirectionWire.ASC),
                sr_policy,
                Catalog(),
                SortFieldAllowSet.From(
                    new SortFieldSpec("joinedAt", AllowedDirections.Descending)),
                ct: CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("arms");
    }

    [Fact]
    public async Task ToListPageOverArmsAsync_NullArms_Throws()
    {
        IReadOnlyList<IQueryable<ArmsSampleRow>> arms = null!;

        var act = async () => await arms.ToListPageOverArmsAsync(
            new PageRequest(null, null),
            sr_policy,
            Catalog(),
            sr_allow,
            ct: CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("arms");
    }

    /// <summary>
    /// The guard puts the position of a null arm in the exception's parameter
    /// name, so the caller can tell which of its arms it failed to build, rather
    /// than receiving a null reference exception from inside the query.
    /// </summary>
    /// <returns>A task that completes when the call has thrown.</returns>
    [Fact]
    public async Task ToListPageOverArmsAsync_AnArmThatIsNull_ThrowsNamingItsPosition()
    {
        await using var db = CreateDb();

        var arms = Arms(Left(db), null!);

        var act = async () => await arms.ToListPageOverArmsAsync(
            new PageRequest(null, null),
            sr_policy,
            Catalog(),
            sr_allow,
            ct: CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("arms[1]");
    }

    [Fact]
    public async Task ToListPageOverArmsAsync_NullRequest_Throws()
    {
        await using var db = CreateDb();

        var arms = Arms(Left(db));

        var act = async () => await arms.ToListPageOverArmsAsync(
            null!,
            sr_policy,
            Catalog(),
            sr_allow,
            ct: CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
    }

    [Fact]
    public async Task ToListPageOverArmsAsync_NullPolicy_Throws()
    {
        await using var db = CreateDb();

        var arms = Arms(Left(db));

        var act = async () => await arms.ToListPageOverArmsAsync(
            new PageRequest(null, null),
            null!,
            Catalog(),
            sr_allow,
            ct: CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("policy");
    }

    [Fact]
    public async Task ToListPageOverArmsAsync_NullCatalog_Throws()
    {
        await using var db = CreateDb();

        var arms = Arms(Left(db));

        var act = async () => await arms.ToListPageOverArmsAsync(
            new PageRequest(null, null),
            sr_policy,
            null!,
            sr_allow,
            ct: CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("catalog");
    }

    [Fact]
    public async Task ToListPageOverArmsAsync_NullAllowSet_Throws()
    {
        await using var db = CreateDb();

        var arms = Arms(Left(db));

        var act = async () => await arms.ToListPageOverArmsAsync(
            new PageRequest(null, null),
            sr_policy,
            Catalog(),
            null!,
            ct: CancellationToken.None);

        await act.Should()
            .ThrowAsync<ArgumentNullException>()
            .WithParameterName("allowedSortFields");
    }

    [Fact]
    public async Task ToListPageOverArmsAsync_OneArm_AnswersThatArmsPage()
    {
        await using var db = CreateDb();
        Seed(db, "left", 3);
        await db.SaveChangesAsync();

        var page = await Arms(Left(db)).ToListPageOverArmsAsync(
            new PageRequest(null, null),
            sr_policy,
            Catalog(),
            sr_allow,
            ct: CancellationToken.None);

        page.Success.Should().BeTrue();
        page.Data!.TotalCount.Should().Be(3);
        page.Data.Items.Should().HaveCount(3);
    }

    /// <summary>
    /// The page is the newest rows of both arms together, not of either alone.
    /// The arms interleave in the seeded order, so a page drawn from one arm and
    /// then topped up from the other would return a different set.
    /// </summary>
    /// <returns>A task that completes when the page has been read.</returns>
    [Fact]
    public async Task ToListPageOverArmsAsync_TwoArms_AnswersTheNewestOfBoth()
    {
        await using var db = CreateDb();
        Seed(db, "left", 4);
        Seed(db, "right", 4, dayOffset: 1);
        await db.SaveChangesAsync();

        var page = await Arms(Left(db), Right(db)).ToListPageOverArmsAsync(
            new PageRequest(null, 3),
            sr_policy,
            Catalog(),
            sr_allow,
            ct: CancellationToken.None);

        page.Success.Should().BeTrue();
        page.Data!.Items.Select(i => i.Name).Should().Equal(
            "right-03", "left-03", "right-02");
    }

    [Fact]
    public async Task ToListPageOverArmsAsync_TwoArms_CountsBothUnlimited()
    {
        await using var db = CreateDb();
        Seed(db, "left", 4);
        Seed(db, "right", 6, dayOffset: 1);
        await db.SaveChangesAsync();

        var page = await Arms(Left(db), Right(db)).ToListPageOverArmsAsync(
            new PageRequest(null, 2),
            sr_policy,
            Catalog(),
            sr_allow,
            ct: CancellationToken.None);

        page.Data!.TotalCount.Should().Be(10);
        page.Data.Items.Should().HaveCount(2);
    }

    /// <summary>
    /// Paging to the end returns every row of both arms once, in the page's
    /// order. A per-arm limit applied without the cursor would drop rows after
    /// the first page, and this walks past that boundary.
    /// </summary>
    /// <returns>A task that completes when the last page has been read.</returns>
    [Fact]
    public async Task ToListPageOverArmsAsync_PagedToTheEnd_ReturnsEveryRowOnceInOrder()
    {
        await using var db = CreateDb();
        Seed(db, "left", 7);
        Seed(db, "right", 7, dayOffset: 1);
        await db.SaveChangesAsync();

        var seen = new List<ArmsSampleRow>();
        string? cursor = null;

        do
        {
            var page = await Arms(Left(db), Right(db)).ToListPageOverArmsAsync(
                new PageRequest(cursor, 3),
                sr_policy,
                Catalog(),
                sr_allow,
                ct: CancellationToken.None);

            page.Success.Should().BeTrue();
            seen.AddRange(page.Data!.Items);
            cursor = page.Data.NextCursor;
        }
        while (cursor is not null);

        seen.Select(r => r.Name).Should().HaveCount(14).And.OnlyHaveUniqueItems();
        seen.Select(r => r.JoinedAt).Should().BeInDescendingOrder();
    }

    /// <summary>
    /// The ascending walk to the end. The descending walk above passes against a
    /// merge whose ascending resume comparison is inverted, because it never
    /// takes that arm.
    /// </summary>
    /// <returns>A task that completes when the last page has been read.</returns>
    [Fact]
    public async Task ToListPageOverArmsAsync_AscendingPagedToTheEnd_ReturnsEveryRowOnceInOrder()
    {
        await using var db = CreateDb();
        Seed(db, "left", 7);
        Seed(db, "right", 7, dayOffset: 1);
        await db.SaveChangesAsync();

        var seen = await WalkAscendingAsync(db, pageSize: 3);

        seen.Select(r => r.Name).Should().HaveCount(14).And.OnlyHaveUniqueItems();
        seen.Select(r => r.JoinedAt).Should().BeInAscendingOrder();
    }

    /// <summary>
    /// Two arms holding the same sort value across a page boundary. Every other
    /// two-arm case seeds the second arm one day apart, so no row of one arm
    /// ever shares a <c>JoinedAt</c> with a row of the other and a merge that
    /// drops or repeats a row at a tie passes the whole file.
    /// </summary>
    /// <param name="pageSize">The page size the walk uses.</param>
    /// <returns>A task that completes when the last page has been read.</returns>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ToListPageOverArmsAsync_ArmsSharingASortValue_PageWithoutDroppingOrRepeating(
        int pageSize)
    {
        await using var db = CreateDb();

        // Both arms carry the same instants, so every page boundary the walk
        // reaches falls on a value both arms hold.
        Seed(db, "left", 4);
        Seed(db, "right", 4);
        await db.SaveChangesAsync();

        var descending = await WalkDescendingAsync(db, pageSize);
        var ascending = await WalkAscendingAsync(db, pageSize);

        descending.Select(r => r.Id).Should().HaveCount(8).And.OnlyHaveUniqueItems();
        descending.Select(r => r.JoinedAt).Should().BeInDescendingOrder();

        ascending.Select(r => r.Id).Should().HaveCount(8).And.OnlyHaveUniqueItems();
        ascending.Select(r => r.JoinedAt).Should().BeInAscendingOrder();
        ascending.Select(r => r.Id).Should().BeEquivalentTo(descending.Select(r => r.Id));
    }

    [Fact]
    public async Task ToListPageOverArmsAsync_AscendingOrder_PagesFromTheOldest()
    {
        await using var db = CreateDb();
        Seed(db, "left", 3);
        Seed(db, "right", 3, dayOffset: 1);
        await db.SaveChangesAsync();

        var page = await Arms(Left(db), Right(db)).ToListPageOverArmsAsync(
            new PageRequest(null, 2, "joinedAt", SortDirectionWire.ASC),
            sr_policy,
            Catalog(),
            sr_allow,
            ct: CancellationToken.None);

        page.Data!.Items.Select(i => i.Name).Should().Equal("left-00", "right-00");
    }

    /// <summary>
    /// A cursor whose tie-break is not an identifier is refused rather than
    /// silently paging from the start of an arm.
    /// </summary>
    /// <returns>A task that completes when the read has returned.</returns>
    [Fact]
    public async Task ToListPageOverArmsAsync_ACursorWithNoIdentifierTieBreak_CursorInvalid()
    {
        await using var db = CreateDb();
        Seed(db, "left", 3);
        await db.SaveChangesAsync();

        var wire = ListPageEf.DefaultCodec.Encode(
            new KeysetCursor(
                KeysetCursor.CURRENT_VERSION,
                "joinedAt",
                Instant.FromUtc(2026, 1, 2, 0, 0).ToString(),
                "not-an-identifier"));

        var page = await Arms(Left(db)).ToListPageOverArmsAsync(
            new PageRequest(wire.Data, 2),
            sr_policy,
            Catalog(),
            sr_allow,
            ct: CancellationToken.None);

        page.ErrorCode.Should().Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_INVALID);
    }

    [Fact]
    public async Task ToListPageOverArmsAsync_CatalogSharesNoFieldWithAllowSet_SortCatalogEmpty()
    {
        await using var db = CreateDb();

        var page = await Arms(Left(db)).ToListPageOverArmsAsync(
            new PageRequest(null, null),
            sr_policy,
            Catalog(),
            SortFieldAllowSet.BothDirections("somethingElse"),
            ct: CancellationToken.None);

        page.ErrorCode.Should().Be(PaginationErrorCodes.LIB_PAGINATION_SORT_CATALOG_EMPTY);
    }

    [Fact]
    public async Task ToListPageOverArmsAsync_ADirectionTheAllowSetLocks_IsRefused()
    {
        await using var db = CreateDb();
        Seed(db, "left", 3);
        await db.SaveChangesAsync();

        var locked = SortFieldAllowSet.From(
            new SortFieldSpec("joinedAt", AllowedDirections.Descending),
            new SortFieldSpec("name", AllowedDirections.Both));

        var page = await Arms(Left(db)).ToListPageOverArmsAsync(
            new PageRequest(null, 10, "joinedAt", SortDirectionWire.ASC),
            sr_policy,
            Catalog(),
            locked,
            ct: CancellationToken.None);

        page.ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_SORT_DIRECTION_NOT_ALLOWED);
    }

    [Fact]
    public async Task ToListPageOverArmsAsync_DefaultSortFieldNotRegistered_SortFieldNotRegistered()
    {
        await using var db = CreateDb();

        var page = await Arms(Left(db)).ToListPageOverArmsAsync(
            new PageRequest(null, null),
            new ListPagingPolicy(new SortSpec("unregistered", SortDirection.Desc)),
            new SortCatalog<ArmsSampleRow>().Text(r => r.Name, r => r.Id),
            SortFieldAllowSet.BothDirections("name", "unregistered"),
            ct: CancellationToken.None);

        page.ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_SORT_FIELD_NOT_REGISTERED);
    }

    [Fact]
    public async Task ToListPageOverArmsAsync_ACodecThatCannotEncode_BubblesTheFailure()
    {
        await using var db = CreateDb();
        Seed(db, "left", 3);
        await db.SaveChangesAsync();

        var page = await Arms(Left(db)).ToListPageOverArmsAsync(
            new PageRequest(null, 1),
            sr_policy,
            Catalog(),
            sr_allow,
            new FakeRefusingArmsCursorCodec(),
            CancellationToken.None);

        page.Failed.Should().BeTrue();
    }

    [Fact]
    public async Task ToListPageOverArmsAsync_ACanceledToken_Throws()
    {
        await using var db = CreateDb();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var arms = Arms(Left(db));
        var canceled = cts.Token;

        var act = async () => await arms.ToListPageOverArmsAsync(
            new PageRequest(null, null),
            sr_policy,
            Catalog(),
            sr_allow,
            ct: canceled);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static SortCatalog<ArmsSampleRow> Catalog()
        => new SortCatalog<ArmsSampleRow>()
            .Instant(r => r.JoinedAt, r => r.Id)
            .Text(r => r.Name, r => r.Id);

    private static IReadOnlyList<IQueryable<ArmsSampleRow>> Arms(
        params IQueryable<ArmsSampleRow>[] arms) => arms;

    private static IQueryable<ArmsSampleRow> Left(ArmsSampleDb db)
        => db.Rows.AsNoTracking()
            .Where(e => e.Side == "left")
            .Select(e => new ArmsSampleRow
            {
                Id = e.Id, Name = e.Name, JoinedAt = e.JoinedAt,
            });

    private static IQueryable<ArmsSampleRow> Right(ArmsSampleDb db)
        => db.Rows.AsNoTracking()
            .Where(e => e.Side == "right")
            .Select(e => new ArmsSampleRow
            {
                Id = e.Id, Name = e.Name, JoinedAt = e.JoinedAt,
            });

    private static ArmsSampleDb CreateDb()
        => new(new DbContextOptionsBuilder<ArmsSampleDb>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static async Task<List<ArmsSampleRow>> WalkDescendingAsync(
        ArmsSampleDb db, int pageSize)
        => await WalkAsync(db, pageSize, SortDirectionWire.DESC);

    private static async Task<List<ArmsSampleRow>> WalkAscendingAsync(
        ArmsSampleDb db, int pageSize)
        => await WalkAsync(db, pageSize, SortDirectionWire.ASC);

    private static async Task<List<ArmsSampleRow>> WalkAsync(
        ArmsSampleDb db, int pageSize, string direction)
    {
        var seen = new List<ArmsSampleRow>();
        string? cursor = null;

        do
        {
            var page = await Arms(Left(db), Right(db)).ToListPageOverArmsAsync(
                new PageRequest(cursor, pageSize, "joinedAt", direction),
                sr_policy,
                Catalog(),
                sr_allow,
                ct: CancellationToken.None);

            page.Success.Should().BeTrue();
            seen.AddRange(page.Data!.Items);
            cursor = page.Data.NextCursor;
        }
        while (cursor is not null);

        return seen;
    }

    /// <summary>
    /// Seeds one arm's rows. The arms interleave by day, so a page drawn over
    /// both contains rows of each.
    /// </summary>
    /// <param name="db">The context to seed.</param>
    /// <param name="side">Which arm the rows belong to.</param>
    /// <param name="count">How many rows to seed.</param>
    /// <param name="dayOffset">Days to add, so the arms interleave.</param>
    private static void Seed(ArmsSampleDb db, string side, int count, int dayOffset = 0)
    {
        for (var i = 0; i < count; i++)
        {
            db.Rows.Add(new ArmsSampleEntity
            {
                Id = Guid.CreateVersion7(),
                Side = side,
                Name = $"{side}-{i:D2}",
                JoinedAt = Instant.FromUtc(2026, 1, 1, 0, 0)
                    .Plus(Duration.FromDays((i * 2) + dayOffset)),
            });
        }
    }

    private sealed class ArmsSampleRow
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public Instant JoinedAt { get; set; }
    }

    private sealed class ArmsSampleDb(DbContextOptions<ArmsSampleDb> options)
        : DbContext(options)
    {
        public DbSet<ArmsSampleEntity> Rows => Set<ArmsSampleEntity>();

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);
            modelBuilder.Entity<ArmsSampleEntity>().HasKey(e => e.Id);
        }
    }

    private sealed class ArmsSampleEntity
    {
        public Guid Id { get; set; }

        public string Side { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public Instant JoinedAt { get; set; }
    }

    private sealed class FakeRefusingArmsCursorCodec : IKeysetCursorCodec
    {
        public D2Result<string?> Encode(KeysetCursor cursor) =>
            D2Result<string?>.ServiceUnavailable();

        public D2Result<KeysetCursor?> Decode(string? encoded) =>
            D2Result<KeysetCursor?>.ValidationFailed();
    }
}
