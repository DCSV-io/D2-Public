// -----------------------------------------------------------------------
// <copyright file="ListPageEfTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Pagination;

using System;
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
/// Unit tests for ListPageEf.ToListPageAsync and the Instant catalog, run on EF
/// InMemory.
/// </summary>
public sealed class ListPageEfTests
{
    private static readonly SortSpec sr_default =
        new(Field: "joinedAt", Direction: SortDirection.Desc);

    private static readonly ListPagingPolicy sr_policy =
        new(DefaultSort: sr_default, DefaultLimit: 50, MaxLimit: 100);

    private static readonly SortFieldAllowSet sr_allow =
        SortFieldAllowSet.BothDirections("joinedAt", "name");

    [Fact]
    public async Task ToListPageAsync_Empty_TotalCountZero_AppliedDefaultSort()
    {
        await using var db = CreateDb();
        var catalog = new SortCatalog<SampleRow>()
            .Instant(r => r.JoinedAt, r => r.Id)
            .Text(r => r.Name, r => r.Id);

        var q = db.Rows.AsNoTracking()
            .Select(e => new SampleRow(e.Id, e.Name, e.JoinedAt));

        var page = await q.ToListPageAsync(
            new PageRequest(null, null),
            sr_policy,
            catalog,
            sr_allow,
            ct: CancellationToken.None);

        page.Success.Should().BeTrue();
        page.Data!.Items.Should().BeEmpty();
        page.Data.TotalCount.Should().Be(0);
        page.Data.AppliedSort.Field.Should().Be("joinedAt");
        page.Data.AppliedSort.Direction.Should().Be(SortDirection.Desc);
        page.Data.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task ToListPageAsync_ClientSort_NameAsc_AppliedSortAndTotal()
    {
        await using var db = CreateDb();
        Seed(db, 3);
        await db.SaveChangesAsync();

        var catalog = new SortCatalog<SampleRow>()
            .Instant(r => r.JoinedAt, r => r.Id)
            .Text(r => r.Name, r => r.Id);

        var q = db.Rows.AsNoTracking()
            .Select(e => new SampleRow(e.Id, e.Name, e.JoinedAt));

        var page = await q.ToListPageAsync(
            new PageRequest(null, 10, "name", SortDirectionWire.ASC),
            sr_policy,
            catalog,
            sr_allow,
            ct: CancellationToken.None);

        page.Success.Should().BeTrue();
        page.Data!.TotalCount.Should().Be(3);
        page.Data.AppliedSort.Field.Should().Be("name");
        page.Data.AppliedSort.DirectionWire.Should().Be(SortDirectionWire.ASC);
        page.Data.Items.Select(i => i.Name).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task ToListPageAsync_DisallowedSort_SortFieldNotAllowed()
    {
        await using var db = CreateDb();
        var catalog = new SortCatalog<SampleRow>()
            .Instant(r => r.JoinedAt, r => r.Id)
            .Text(r => r.Name, r => r.Id);

        var q = db.Rows.AsNoTracking()
            .Select(e => new SampleRow(e.Id, e.Name, e.JoinedAt));

        var page = await q.ToListPageAsync(
            new PageRequest(null, null, "notAField", SortDirectionWire.DESC),
            sr_policy,
            catalog,
            sr_allow,
            ct: CancellationToken.None);

        page.ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_SORT_FIELD_NOT_ALLOWED);
        page.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ToListPageAsync_LimitPlusOne_EncodesNextCursor()
    {
        await using var db = CreateDb();
        Seed(db, 5);
        await db.SaveChangesAsync();

        var catalog = new SortCatalog<SampleRow>()
            .Instant(r => r.JoinedAt, r => r.Id)
            .Text(r => r.Name, r => r.Id);

        var q = db.Rows.AsNoTracking()
            .Select(e => new SampleRow(e.Id, e.Name, e.JoinedAt));

        var page = await q.ToListPageAsync(
            new PageRequest(null, 2),
            sr_policy,
            catalog,
            sr_allow,
            ct: CancellationToken.None);

        page.Success.Should().BeTrue();
        page.Data!.Items.Should().HaveCount(2);
        page.Data.TotalCount.Should().Be(5);
        page.Data.NextCursor.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Instant_RoundTrip_ExtendedIso()
    {
        var instant = Instant.FromUtc(2026, 7, 26, 12, 0);
        var round = SortCatalogInstantExtensions.RoundTripExtendedIso(instant);
        round.Success.Should().BeTrue();
        round.Data.Should().Be(instant);
    }

    [Fact]
    public async Task ToListPageAsync_TotalCount_UnchangedAcrossCursorPages()
    {
        await using var db = CreateDb();
        Seed(db, 5);
        await db.SaveChangesAsync();

        var catalog = new SortCatalog<SampleRow>()
            .Instant(r => r.JoinedAt, r => r.Id)
            .Text(r => r.Name, r => r.Id);

        var q = db.Rows.AsNoTracking()
            .Select(e => new SampleRow(e.Id, e.Name, e.JoinedAt));

        var page1 = await q.ToListPageAsync(
            new PageRequest(null, 2),
            sr_policy,
            catalog,
            sr_allow,
            ct: CancellationToken.None);

        page1.Success.Should().BeTrue();
        page1.Data!.TotalCount.Should().Be(5);
        page1.Data.NextCursor.Should().NotBeNullOrWhiteSpace();

        var page2 = await q.ToListPageAsync(
            new PageRequest(page1.Data.NextCursor, 2),
            sr_policy,
            catalog,
            sr_allow,
            ct: CancellationToken.None);

        page2.Success.Should().BeTrue();
        page2.Data!.TotalCount.Should().Be(5);
        page2.Data.TotalCount.Should().Be(page1.Data.TotalCount);
    }

    [Fact]
    public async Task ToListPageAsync_Happy_NextCursor_Resume()
    {
        await using var db = CreateDb();
        Seed(db, 5);
        await db.SaveChangesAsync();

        var catalog = new SortCatalog<SampleRow>()
            .Instant(r => r.JoinedAt, r => r.Id)
            .Text(r => r.Name, r => r.Id);

        var q = db.Rows.AsNoTracking()
            .Select(e => new SampleRow(e.Id, e.Name, e.JoinedAt));

        var page1 = await q.ToListPageAsync(
            new PageRequest(null, 2),
            sr_policy,
            catalog,
            sr_allow,
            ct: CancellationToken.None);

        page1.Success.Should().BeTrue();
        page1.Data!.Items.Should().HaveCount(2);
        var firstIds = page1.Data.Items.Select(i => i.Id).ToArray();

        var page2 = await q.ToListPageAsync(
            new PageRequest(page1.Data.NextCursor, 2),
            sr_policy,
            catalog,
            sr_allow,
            ct: CancellationToken.None);

        page2.Success.Should().BeTrue();
        page2.Data!.Items.Should().NotBeEmpty();
        page2.Data.Items.Select(i => i.Id).Should().NotIntersectWith(firstIds);
    }

    [Fact]
    public async Task ToListPageAsync_Cancel_Propagates()
    {
        await using var db = CreateDb();
        Seed(db, 3);
        await db.SaveChangesAsync();

        var catalog = new SortCatalog<SampleRow>()
            .Instant(r => r.JoinedAt, r => r.Id)
            .Text(r => r.Name, r => r.Id);

        var q = db.Rows.AsNoTracking()
            .Select(e => new SampleRow(e.Id, e.Name, e.JoinedAt));

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Capture the token, not the source - the lambda must not close over a
        // disposable owned by this scope.
        var canceled = cts.Token;

        var act = async () => await q.ToListPageAsync(
            new PageRequest(null, 2),
            sr_policy,
            catalog,
            sr_allow,
            ct: canceled);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ToListPageAsync_EncodeFail_BubbleFail()
    {
        await using var db = CreateDb();
        Seed(db, 3);
        await db.SaveChangesAsync();

        var catalog = new SortCatalog<SampleRow>()
            .Instant(r => r.JoinedAt, r => r.Id)
            .Text(r => r.Name, r => r.Id);

        var q = db.Rows.AsNoTracking()
            .Select(e => new SampleRow(e.Id, e.Name, e.JoinedAt));

        var page = await q.ToListPageAsync(
            new PageRequest(null, 1),
            sr_policy,
            catalog,
            sr_allow,
            new FakeFailingCursorCodec(),
            CancellationToken.None);

        page.Failed.Should().BeTrue();
        page.Success.Should().BeFalse();
    }

    [Fact]
    public void DefaultCodec_IsTheOpaqueCodec_AndCarriesNoSetter()
    {
        ListPageEf.DefaultCodec.Should().BeOfType<OpaqueKeysetCursorCodec>();

        typeof(ListPageEf)
            .GetProperty(nameof(ListPageEf.DefaultCodec))!
            .SetMethod
            .Should().BeNull();
    }

    [Fact]
    public async Task ToListPageAsync_SuppliedCodec_IsUsedInsteadOfTheDefault()
    {
        await using var db = CreateDb();
        Seed(db, 3);
        await db.SaveChangesAsync();

        var catalog = new SortCatalog<SampleRow>()
            .Instant(r => r.JoinedAt, r => r.Id)
            .Text(r => r.Name, r => r.Id);

        var q = db.Rows.AsNoTracking()
            .Select(e => new SampleRow(e.Id, e.Name, e.JoinedAt));

        var codec = new FakeCountingCursorCodec();

        var page = await q.ToListPageAsync(
            new PageRequest(null, 1),
            sr_policy,
            catalog,
            sr_allow,
            codec,
            CancellationToken.None);

        page.Success.Should().BeTrue();
        codec.Encodes.Should().Be(1);
        page.Data!.NextCursor.Should().Be(FakeCountingCursorCodec.WIRE);
    }

    [Fact]
    public async Task ToListPageAsync_CatalogSharesNoFieldWithAllowSet_SortCatalogEmpty()
    {
        await using var db = CreateDb();
        var catalog = new SortCatalog<SampleRow>()
            .Text(r => r.Name, r => r.Id);

        var q = db.Rows.AsNoTracking()
            .Select(e => new SampleRow(e.Id, e.Name, e.JoinedAt));

        var page = await q.ToListPageAsync(
            new PageRequest(null, null),
            sr_policy,
            catalog,
            SortFieldAllowSet.BothDirections("somethingElse"),
            ct: CancellationToken.None);

        page.ErrorCode.Should().Be(PaginationErrorCodes.LIB_PAGINATION_SORT_CATALOG_EMPTY);
        page.StatusCode.Should().Be(System.Net.HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task ToListPageAsync_DefaultSortFieldNotRegistered_SortFieldNotRegistered()
    {
        await using var db = CreateDb();

        // The allow-set and the catalog share `name`, so the operation is
        // usable; the policy's own default field is the one with no entry.
        var catalog = new SortCatalog<SampleRow>()
            .Text(r => r.Name, r => r.Id);

        var q = db.Rows.AsNoTracking()
            .Select(e => new SampleRow(e.Id, e.Name, e.JoinedAt));

        var page = await q.ToListPageAsync(
            new PageRequest(null, null),
            new ListPagingPolicy(new SortSpec("unregistered", SortDirection.Desc)),
            catalog,
            SortFieldAllowSet.BothDirections("name", "unregistered"),
            ct: CancellationToken.None);

        page.ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_SORT_FIELD_NOT_REGISTERED);
        page.StatusCode.Should().Be(System.Net.HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task ToListPageAsync_LockedFieldRefusesTheOtherDirection()
    {
        await using var db = CreateDb();
        Seed(db, 3);
        await db.SaveChangesAsync();

        var catalog = new SortCatalog<SampleRow>()
            .Instant(r => r.JoinedAt, r => r.Id)
            .Text(r => r.Name, r => r.Id);

        var q = db.Rows.AsNoTracking()
            .Select(e => new SampleRow(e.Id, e.Name, e.JoinedAt));

        var locked = SortFieldAllowSet.From(
            new SortFieldSpec("joinedAt", AllowedDirections.Descending),
            new SortFieldSpec("name", AllowedDirections.Both));

        var page = await q.ToListPageAsync(
            new PageRequest(null, 10, "joinedAt", SortDirectionWire.ASC),
            sr_policy,
            catalog,
            locked,
            ct: CancellationToken.None);

        page.ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_SORT_DIRECTION_NOT_ALLOWED);
    }

    [Fact]
    public async Task ToListPageAsync_UndecodableCursorPrimaryKey_CursorKeyUndecodable()
    {
        await using var db = CreateDb();
        Seed(db, 3);
        await db.SaveChangesAsync();

        var catalog = new SortCatalog<SampleRow>()
            .Instant(r => r.JoinedAt, r => r.Id)
            .Text(r => r.Name, r => r.Id);

        var q = db.Rows.AsNoTracking()
            .Select(e => new SampleRow(e.Id, e.Name, e.JoinedAt));

        var wire = ListPageEf.DefaultCodec.Encode(
            new KeysetCursor(
                KeysetCursor.CURRENT_VERSION,
                "joinedAt",
                "not-an-instant",
                Guid.CreateVersion7().ToString("D")));

        var page = await q.ToListPageAsync(
            new PageRequest(wire.Data, 2),
            sr_policy,
            catalog,
            sr_allow,
            ct: CancellationToken.None);

        page.ErrorCode.Should()
            .Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_KEY_UNDECODABLE);
    }

    [Fact]
    public async Task ToListPageAsync_BoundaryRowWithAnEmptySortValue_PagesRatherThanFaults()
    {
        // The boundary row's own name is the empty string, which is data the
        // server read out of its own table. The cursor minted from it has to be
        // an ordinary cursor and the walk has to continue through it.
        await using var db = CreateDb();
        Seed(db, 2);
        db.Rows.Add(new SampleEntity
        {
            Id = Guid.CreateVersion7(),
            Name = string.Empty,
            JoinedAt = Instant.FromUtc(2026, 1, 1, 0, 0),
        });
        await db.SaveChangesAsync();

        var catalog = new SortCatalog<SampleRow>()
            .Instant(r => r.JoinedAt, r => r.Id)
            .Text(r => r.Name, r => r.Id);

        var q = db.Rows.AsNoTracking()
            .Select(e => new SampleRow(e.Id, e.Name, e.JoinedAt));

        var first = await q.ToListPageAsync(
            new PageRequest(null, 1, "name", SortDirectionWire.ASC),
            sr_policy,
            catalog,
            sr_allow,
            ct: CancellationToken.None);

        first.Success.Should().BeTrue();
        first.Data!.Items.Single().Name.Should().BeEmpty();
        first.Data.NextCursor.Should().NotBeNullOrWhiteSpace();

        var second = await q.ToListPageAsync(
            new PageRequest(first.Data.NextCursor, 10, "name", SortDirectionWire.ASC),
            sr_policy,
            catalog,
            sr_allow,
            ct: CancellationToken.None);

        second.Success.Should().BeTrue();
        second.Data!.Items.Select(r => r.Name).Should().Equal(["name-00", "name-01"]);
    }

    [Fact]
    public async Task ToListPageAsync_CursorTieBreakNotAGuid_CursorInvalid()
    {
        await using var db = CreateDb();
        Seed(db, 3);
        await db.SaveChangesAsync();

        var catalog = new SortCatalog<SampleRow>()
            .Instant(r => r.JoinedAt, r => r.Id)
            .Text(r => r.Name, r => r.Id);

        var q = db.Rows.AsNoTracking()
            .Select(e => new SampleRow(e.Id, e.Name, e.JoinedAt));

        var wire = ListPageEf.DefaultCodec.Encode(
            new KeysetCursor(
                KeysetCursor.CURRENT_VERSION,
                "joinedAt",
                "2026-01-01T00:00:00Z",
                "not-a-guid"));

        var page = await q.ToListPageAsync(
            new PageRequest(wire.Data, 2),
            sr_policy,
            catalog,
            sr_allow,
            ct: CancellationToken.None);

        page.ErrorCode.Should().Be(PaginationErrorCodes.LIB_PAGINATION_CURSOR_INVALID);
    }

    [Fact]
    public async Task ToListPageAsync_NullArguments_Throw()
    {
        await using var db = CreateDb();
        var catalog = new SortCatalog<SampleRow>().Text(r => r.Name, r => r.Id);

        var q = db.Rows.AsNoTracking()
            .Select(e => new SampleRow(e.Id, e.Name, e.JoinedAt));

        var nullRequest = async () => await q.ToListPageAsync(
            null!, sr_policy, catalog, sr_allow, ct: CancellationToken.None);

        var nullPolicy = async () => await q.ToListPageAsync(
            new PageRequest(null, null),
            null!,
            catalog,
            sr_allow,
            ct: CancellationToken.None);

        var nullCatalog = async () => await q.ToListPageAsync(
            new PageRequest(null, null),
            sr_policy,
            null!,
            sr_allow,
            ct: CancellationToken.None);

        var nullAllow = async () => await q.ToListPageAsync(
            new PageRequest(null, null),
            sr_policy,
            catalog,
            null!,
            ct: CancellationToken.None);

        await nullRequest.Should().ThrowAsync<ArgumentNullException>();
        await nullPolicy.Should().ThrowAsync<ArgumentNullException>();
        await nullCatalog.Should().ThrowAsync<ArgumentNullException>();
        await nullAllow.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ToListPageAsync_ConcurrentCallers_EachGetItsOwnOrdering()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        await using (var seedDb = CreateDb(databaseName))
        {
            Seed(seedDb, 20);
            await seedDb.SaveChangesAsync();
        }

        var catalog = new SortCatalog<SampleRow>()
            .Instant(r => r.JoinedAt, r => r.Id)
            .Text(r => r.Name, r => r.Id);

        // Two request shapes racing on one catalog and one allow-set, which are
        // process-wide values in production. Neither may see the other's sort.
        var tasks = Enumerable.Range(0, 32).Select(i =>
        {
            var ascending = i % 2 == 0;
            var request = ascending
                ? new PageRequest(null, 5, "name", SortDirectionWire.ASC)
                : new PageRequest(null, 5, "name", SortDirectionWire.DESC);

            return Task.Run(async () =>
            {
                await using var db = CreateDb(databaseName);

                var page = await db.Rows.AsNoTracking()
                    .Select(e => new SampleRow(e.Id, e.Name, e.JoinedAt))
                    .ToListPageAsync(
                        request,
                        sr_policy,
                        catalog,
                        sr_allow,
                        ct: CancellationToken.None);

                page.Success.Should().BeTrue();
                return (ascending, names: page.Data!.Items.Select(r => r.Name).ToArray());
            });
        });

        var results = await Task.WhenAll(tasks);

        foreach (var (ascending, names) in results)
        {
            if (ascending)
                names.Should().BeInAscendingOrder();
            else
                names.Should().BeInDescendingOrder();
        }
    }

    private static SampleDb CreateDb(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<SampleDb>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString("N"))
            .Options;
        return new SampleDb(options);
    }

    private static void Seed(SampleDb db, int count)
    {
        for (var i = 0; i < count; i++)
        {
            db.Rows.Add(new SampleEntity
            {
                Id = Guid.CreateVersion7(),
                Name = $"name-{i:D2}",
                JoinedAt = Instant.FromUtc(2026, 1, 1 + i, 0, 0),
            });
        }
    }

    private sealed record SampleRow(Guid Id, string Name, Instant JoinedAt);

    private sealed class SampleDb : DbContext
    {
        public SampleDb(DbContextOptions<SampleDb> options)
            : base(options)
        {
        }

        public DbSet<SampleEntity> Rows => Set<SampleEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SampleEntity>().HasKey(e => e.Id);
        }
    }

    private sealed class SampleEntity
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public Instant JoinedAt { get; set; }
    }

    private sealed class FakeFailingCursorCodec : IKeysetCursorCodec
    {
        public D2Result<string?> Encode(KeysetCursor cursor) =>
            D2Result<string?>.ServiceUnavailable();

        public D2Result<KeysetCursor?> Decode(string? encoded) =>
            D2Result<KeysetCursor?>.ValidationFailed();
    }

    private sealed class FakeCountingCursorCodec : IKeysetCursorCodec
    {
        public const string WIRE = "counted-cursor";

        private int _encodes;

        public int Encodes => _encodes;

        public D2Result<string?> Encode(KeysetCursor cursor)
        {
            Interlocked.Increment(ref _encodes);
            return D2Result<string?>.Ok(WIRE);
        }

        public D2Result<KeysetCursor?> Decode(string? encoded) =>
            D2Result<KeysetCursor?>.ValidationFailed();
    }
}
