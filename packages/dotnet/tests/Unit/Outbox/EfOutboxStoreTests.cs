// -----------------------------------------------------------------------
// <copyright file="EfOutboxStoreTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox;

using AwesomeAssertions;
using DcsvIo.D2.Outbox;
using DcsvIo.D2.Outbox.EntityFrameworkCore;
using DcsvIo.D2.Tests.Unit.Outbox.Fixtures;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Xunit;

/// <summary>
/// The relay's read and write path. The claim predicate decides whether a
/// backed-off message is tried early, and the record path decides whether a
/// delivered message is delivered twice.
/// </summary>
public sealed class EfOutboxStoreTests
{
    private static readonly Instant sr_now = Instant.FromUtc(2026, 8, 17, 10, 0);

    private static readonly Duration sr_lease = Duration.FromSeconds(60);

    [Fact]
    public async Task ClaimDueAsync_AnEmptyTable_IsOkWithNothing()
    {
        await using var db = NewDb();
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        claimed.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task ClaimDueAsync_AStagedRowWithNoNextAttempt_ClaimsIt()
    {
        await using var db = NewDb();
        await SeedAsync(db, Staged(sr_now - Duration.FromMinutes(1)));
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        claimed.Data.Should().ContainSingle();
    }

    [Fact]
    public async Task ClaimDueAsync_ARowDueExactlyNow_ClaimsIt()
    {
        // The boundary is inclusive: a message whose wait has just elapsed is
        // attemptable, otherwise it waits a whole further tick for nothing.
        await using var db = NewDb();
        await SeedAsync(db, Staged(sr_now) with { NextAttemptAt = sr_now });
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        claimed.Data.Should().ContainSingle();
    }

    [Fact]
    public async Task ClaimDueAsync_ARowNotYetDue_LeavesIt()
    {
        // The admitting counterpart of the test above: without this the backoff
        // would have no effect, and a failing broker would receive an attempt
        // every tick.
        await using var db = NewDb();
        await SeedAsync(
            db,
            Staged(sr_now) with { NextAttemptAt = sr_now + Duration.FromSeconds(1) });
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        claimed.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task ClaimDueAsync_APublishedRow_LeavesIt()
    {
        await using var db = NewDb();
        await SeedAsync(db, Staged(sr_now).Publish(sr_now));
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        claimed.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task ClaimDueAsync_AnAbandonedRow_LeavesIt()
    {
        // Abandonment is terminal until an operator re-stages it, so the relay
        // must not pick it up again on its own.
        await using var db = NewDb();
        await SeedAsync(
            db, Staged(sr_now).Fail(sr_now, "boom", limit: 1, Duration.Zero));
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        claimed.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task ClaimDueAsync_ManyDueRows_ReturnsTheOldestFirst()
    {
        await using var db = NewDb();
        var older = Staged(sr_now - Duration.FromMinutes(5));
        var newer = Staged(sr_now - Duration.FromMinutes(1));
        await SeedAsync(db, newer, older);
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        claimed.Data!.Select(m => m.Id).Should().Equal(older.Id, newer.Id);
    }

    [Fact]
    public async Task ClaimDueAsync_MoreDueRowsThanTheBatch_ReturnsAtMostTheBatch()
    {
        // The bound on a tick's work. Without it a backlog built during an outage
        // is claimed whole into memory on the first tick after recovery.
        await using var db = NewDb();
        await SeedAsync(
            db,
            Staged(sr_now - Duration.FromMinutes(3)),
            Staged(sr_now - Duration.FromMinutes(2)),
            Staged(sr_now - Duration.FromMinutes(1)));
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now, 2, sr_lease, TestContext.Current.CancellationToken);

        claimed.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task ClaimDueAsync_ABatchSizeOfZero_IsValidationFailed()
    {
        await using var db = NewDb();
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now, 0, sr_lease, TestContext.Current.CancellationToken);

        claimed.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ClaimDueAsync_ANegativeBatchSize_IsValidationFailed()
    {
        await using var db = NewDb();
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now, -1, sr_lease, TestContext.Current.CancellationToken);

        claimed.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ClaimDueAsync_TheDatabaseRefusing_IsServiceUnavailable()
    {
        // A read that fails is an outage rather than an empty backlog, and
        // returning Ok with no messages would look like a drained queue.
        // Not an `await using`: the dispose below IS this test's disposal, and
        // the context after it is the subject rather than a leak.
        var db = NewDb();
        await db.DisposeAsync();
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        claimed.IsServiceUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task ClaimDueAsync_ACanceledToken_PropagatesTheCancellation()
    {
        await using var db = NewDb();
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // The call is made before the assertion lambda so the lambda captures
        // the task alone - a lambda closing over the store would close over the
        // context this scope disposes.
        var claiming = store.ClaimDueAsync(sr_now, 10, sr_lease, cts.Token).AsTask();
        var act = () => claiming;

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RecordAsync_APublishedState_IsOk()
    {
        await using var db = NewDb();
        var staged = Staged(sr_now);
        await SeedAsync(db, staged);
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        var recorded = await store.RecordAsync(
            staged.Publish(sr_now), TestContext.Current.CancellationToken);

        recorded.IsOk.Should().BeTrue();
    }

    /// <summary>
    /// The claim counts the attempt on the row before the message is handed
    /// over, so the retry budget survives a database that reads and refuses
    /// writes: the outcome may never be recorded, and the row still records one
    /// more attempt.
    /// </summary>
    [Fact]
    public async Task ClaimDueAsync_AClaimedRow_CountsTheAttemptOnTheRow()
    {
        await using var db = NewDb();
        await SeedAsync(db, Staged(sr_now - Duration.FromMinutes(1)));
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        _ = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);
        var row = await db.OutboxMessages.SingleAsync(TestContext.Current.CancellationToken);

        row.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task ClaimDueAsync_AClaimedRow_PushesItsNextAttemptOutByTheLease()
    {
        await using var db = NewDb();
        await SeedAsync(db, Staged(sr_now - Duration.FromMinutes(1)));
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        _ = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);
        var row = await db.OutboxMessages.SingleAsync(TestContext.Current.CancellationToken);

        row.NextAttemptAt.Should().Be(sr_now + sr_lease);
    }

    [Fact]
    public async Task ClaimDueAsync_AClaimedMessage_CarriesTheAttemptCountItHadGoingIn()
    {
        // The retry decision counts from this value and the claim has already
        // written the count it reaches, so the two agree whether or not the
        // outcome is recorded afterwards.
        await using var db = NewDb();
        await SeedAsync(db, Staged(sr_now - Duration.FromMinutes(1)));
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        claimed.Data.Should().ContainSingle().Which.Attempts.Should().Be(0);
    }

    [Fact]
    public async Task ClaimDueAsync_AClaimedRow_IsNotClaimableAgainInsideTheLease()
    {
        await using var db = NewDb();
        await SeedAsync(db, Staged(sr_now - Duration.FromMinutes(1)));
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        _ = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);
        var again = await store.ClaimDueAsync(
            sr_now + Duration.FromSeconds(5),
            10,
            sr_lease,
            TestContext.Current.CancellationToken);

        again.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task ClaimDueAsync_ANegativeLease_IsValidationFailed()
    {
        await using var db = NewDb();
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now,
            10,
            Duration.FromSeconds(-1),
            TestContext.Current.CancellationToken);

        claimed.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ClaimDueAsync_AZeroLease_IsValidationFailed()
    {
        // A zero lease stamps the next attempt at now, and the claim predicate
        // matches a row due at now, so a message still being dispatched would be
        // claimable again at once.
        await using var db = NewDb();
        await SeedAsync(db, Staged(sr_now - Duration.FromMinutes(1)));
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now, 10, Duration.Zero, TestContext.Current.CancellationToken);

        claimed.IsValidationFailed.Should().BeTrue();
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(1_000L)]
    [InlineData(999_999L)]
    public async Task ClaimDueAsync_ALeaseBelowOneMillisecond_IsValidationFailed(long nanoseconds)
    {
        // A PostgreSQL timestamp keeps microseconds, so a lease under one
        // microsecond is stored as now and the claim matches the message again
        // at once. The floor is one millisecond, the relay's own. The in-memory
        // provider keeps nanoseconds and cannot show the truncation, so the
        // floor is pinned here and the stored instant in the PostgreSQL suite.
        await using var db = NewDb();
        await SeedAsync(db, Staged(sr_now - Duration.FromMinutes(1)));
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now,
            10,
            Duration.FromNanoseconds(nanoseconds),
            TestContext.Current.CancellationToken);

        claimed.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ClaimDueAsync_ALeaseOfOneMillisecond_ClaimsIt()
    {
        // The admitting case at the floor.
        await using var db = NewDb();
        await SeedAsync(db, Staged(sr_now - Duration.FromMinutes(1)));
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now, 10, Duration.FromMilliseconds(1), TestContext.Current.CancellationToken);
        var again = await store.ClaimDueAsync(
            sr_now, 10, Duration.FromMilliseconds(1), TestContext.Current.CancellationToken);

        claimed.Data.Should().ContainSingle();
        again.Data.Should().BeEmpty("the lease moved the next attempt past now");
    }

    [Fact]
    public async Task RecordAsync_AReStagedState_PersistsWhatWentWrong()
    {
        // The code is recorded on every failed attempt rather than only on the
        // one that abandons, so an operator querying during an outage can tell a
        // missing dispatcher from a transport that is down.
        await using var db = NewDb();
        var staged = Staged(sr_now);
        await SeedAsync(db, staged);
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        _ = await store.RecordAsync(
            staged.Fail(sr_now, "boom", limit: 5, Duration.FromSeconds(8)),
            TestContext.Current.CancellationToken);
        var row = await db.OutboxMessages.SingleAsync(TestContext.Current.CancellationToken);

        row.LastFailureCode.Should().Be("boom");
    }

    [Fact]
    public async Task RecordAsync_APublishedState_MakesTheRowNoLongerClaimable()
    {
        // This store behavior keeps at-least-once delivery from repeating
        // forever: a confirmed message that the store still returned would be
        // dispatched again every tick.
        await using var db = NewDb();
        var staged = Staged(sr_now);
        await SeedAsync(db, staged);
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        _ = await store.RecordAsync(
            staged.Publish(sr_now), TestContext.Current.CancellationToken);
        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        claimed.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordAsync_AReStagedState_PersistsTheNextAttemptInstant()
    {
        await using var db = NewDb();
        var staged = Staged(sr_now);
        await SeedAsync(db, staged);
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);
        var next = sr_now + Duration.FromSeconds(8);

        _ = await store.RecordAsync(
            staged.Fail(sr_now, "boom", limit: 5, Duration.FromSeconds(8)),
            TestContext.Current.CancellationToken);
        var row = await db.OutboxMessages.SingleAsync(TestContext.Current.CancellationToken);

        row.NextAttemptAt.Should().Be(next);
    }

    [Fact]
    public async Task RecordAsync_AnAbandonedState_KeepsThePayloadOnTheRow()
    {
        // Abandonment is escalation, not deletion: an operator has to be able to
        // read what was never delivered and decide whether to re-stage it.
        await using var db = NewDb();
        var staged = Staged(sr_now);
        await SeedAsync(db, staged);
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        _ = await store.RecordAsync(
            staged.Fail(sr_now, "boom", limit: 1, Duration.Zero),
            TestContext.Current.CancellationToken);
        var row = await db.OutboxMessages.SingleAsync(TestContext.Current.CancellationToken);

        row.Payload.Should().Be(staged.Payload);
    }

    [Fact]
    public async Task RecordAsync_ARowThatIsGone_IsNotFound()
    {
        await using var db = NewDb();
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        var recorded = await store.RecordAsync(
            Staged(sr_now).Publish(sr_now), TestContext.Current.CancellationToken);

        recorded.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task RecordAsync_ANullMessage_Throws()
    {
        await using var db = NewDb();
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        var act = async () => await store.RecordAsync(
            null!, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RecordAsync_TheDatabaseRefusing_IsServiceUnavailable()
    {
        // Not an `await using`: the dispose below IS this test's disposal, and
        // the context after it is the subject rather than a leak.
        var db = NewDb();
        var staged = Staged(sr_now);
        await SeedAsync(db, staged);
        await db.DisposeAsync();
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);

        var recorded = await store.RecordAsync(
            staged.Publish(sr_now), TestContext.Current.CancellationToken);

        recorded.IsServiceUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task RecordAsync_ACanceledToken_PropagatesTheCancellation()
    {
        await using var db = NewDb();
        var staged = Staged(sr_now);
        await SeedAsync(db, staged);
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var recording = store.RecordAsync(staged.Publish(sr_now), cts.Token).AsTask();
        var act = () => recording;

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RecordAsync_TwiceForOneMessage_IsIdempotent()
    {
        // The relay is at-least-once, so a state it already recorded can be
        // recorded again after a restart, and the second must not fail.
        await using var db = NewDb();
        var staged = Staged(sr_now);
        await SeedAsync(db, staged);
        var store = new EfOutboxStore<IOutboxProbeDbFixture, OutboxProbeDbContextFixture>(db);
        var published = staged.Publish(sr_now);

        _ = await store.RecordAsync(published, TestContext.Current.CancellationToken);
        var second = await store.RecordAsync(
            published, TestContext.Current.CancellationToken);

        second.IsOk.Should().BeTrue();
    }

    private static StagedOutboxMessage Staged(Instant at) => new()
    {
        Id = new OutboxMessageId(Guid.CreateVersion7()),
        Kind = nameof(FakeOutboxKind.ThingHappened),
        Payload = """{"detail":"a value"}""",
        StagedAt = at,
        Attempts = 0,
    };

    private static async Task SeedAsync(
        OutboxProbeDbContextFixture db, params OutboxMessage[] messages)
    {
        foreach (var message in messages)
            db.OutboxMessages.Add(message.ToNewRow());

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
    }

    private static OutboxProbeDbContextFixture NewDb()
        => new(new DbContextOptionsBuilder<OutboxProbeDbContextFixture>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options);
}
