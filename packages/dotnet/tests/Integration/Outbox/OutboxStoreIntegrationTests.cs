// -----------------------------------------------------------------------
// <copyright file="OutboxStoreIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Outbox;

using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Outbox;
using DcsvIo.D2.Outbox.EntityFrameworkCore;
using DcsvIo.D2.Tests.Integration.DataGovernance;
using DcsvIo.D2.Tests.Integration.Outbox.Fixtures;
using DcsvIo.D2.Time;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Npgsql;
using Xunit;

/// <summary>
/// The outbox mapping and store against a real PostgreSQL server. The value
/// conversions, the <c>jsonb</c> column and the <c>xmin</c> concurrency token
/// come from the provider, so a test against an in-memory store proves
/// nothing about any of them.
/// </summary>
[Collection("Postgres")]
[Trait("Category", "Integration")]
public sealed class OutboxStoreIntegrationTests : IAsyncLifetime
{
    private const string _KIND = "ThingHappened";

    private static readonly Instant sr_now = Instant.FromUtc(2026, 8, 17, 15, 0);

    private static readonly Duration sr_lease = Duration.FromSeconds(60);

    private readonly PostgresFixture r_fixture;
    private string _connectionString = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxStoreIntegrationTests"/>
    /// class.
    /// </summary>
    /// <param name="fixture">The shared Postgres Testcontainers fixture.</param>
    public OutboxStoreIntegrationTests(PostgresFixture fixture)
    {
        r_fixture = fixture;
    }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        _connectionString = await r_fixture.CreateIsolatedDatabaseAsync(
            nameof(OutboxStoreIntegrationTests));

        await using var db = OutboxHostDbContextFixture.Build(_connectionString);
        await db.Database.EnsureCreatedAsync();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() =>
        await r_fixture.AssertNoConnectionOpenAsync(_connectionString);

    [Fact]
    public async Task TheMigration_CreatesThePayloadColumnAsJsonb()
    {
        // Text would round-trip too. The declared type lets an operator query
        // into an abandoned message's payload.
        (await ColumnTypeAsync("payload")).Should().Be("jsonb");
    }

    [Fact]
    public async Task TheMigration_CreatesTheStatusColumnAsText()
        => (await ColumnTypeAsync("status")).Should().Be("text");

    [Fact]
    public async Task TheMigration_CreatesTheStagedInstantAsATimestampWithTimeZone()
        => (await ColumnTypeAsync("staged_at")).Should()
            .Be("timestamp with time zone");

    [Fact]
    public async Task TheMigration_CreatesTheFilteredClaimIndex()
    {
        var definition = await ScalarAsync(
            "SELECT indexdef FROM pg_indexes WHERE indexname = "
            + "'ix_outbox_messages_status_next_attempt_at'");

        definition.Should().NotBeNull();
    }

    [Fact]
    public async Task TheMigration_FiltersTheClaimIndexToTheStagedStatus()
    {
        // Without the filter the index grows with every message ever staged.
        var definition = await ScalarAsync(
            "SELECT indexdef FROM pg_indexes WHERE indexname = "
            + "'ix_outbox_messages_status_next_attempt_at'");

        definition.Should().Contain("'Staged'");
    }

    [Fact]
    public async Task AStagedMessage_IsStoredWithItsStatusSpelledOut()
    {
        // The status is stored by name, so a consumer adding an enum member above
        // an existing one does not silently re-point every stored row.
        await StageAsync();

        (await ScalarAsync("SELECT status FROM outbox_messages LIMIT 1"))
            .Should().Be("Staged");
    }

    [Fact]
    public async Task AStagedMessage_ReadsBackAsTheStagedLeaf()
    {
        var id = await StageAsync();

        await using var db = OutboxHostDbContextFixture.Build(_connectionString);
        var store = new EfOutboxStore<IOutboxHostDbFixture, OutboxHostDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        claimed.Data.Should().ContainSingle().Which.Id.Value.Should().Be(id);
    }

    [Fact]
    public async Task AStagedMessage_KeepsItsPayloadThroughTheJsonbColumn()
    {
        await StageAsync();

        await using var db = OutboxHostDbContextFixture.Build(_connectionString);
        var store = new EfOutboxStore<IOutboxHostDbFixture, OutboxHostDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        var read = OutboxPayloadSerializer.Deserialize<SampleOutboxIntegrationPayload>(
            claimed.Data!.Single().Payload);

        read.Should().Be(new SampleOutboxIntegrationPayload("a value"));
    }

    /// <summary>
    /// What <c>jsonb</c> does to the stored text.
    /// PostgreSQL parses a <c>jsonb</c> value and re-renders it on read, so the
    /// stored text is equivalent to what the serializer wrote but not identical
    /// to it - whitespace and key order are the server's to choose.
    /// </summary>
    /// <remarks>
    /// A signature, a hash or a byte comparison over the stored payload would
    /// therefore be wrong. The golden-file test on
    /// <c>OutboxPayloadSerializer</c> pins what the serializer WRITES, which is
    /// the text that must stay compatible; this test pins that the column
    /// does not preserve those bytes.
    /// </remarks>
    [Fact]
    public async Task TheJsonbColumn_ReRendersTheStoredTextRatherThanKeepingItsBytes()
    {
        await StageAsync();

        await using var db = OutboxHostDbContextFixture.Build(_connectionString);
        var store = new EfOutboxStore<IOutboxHostDbFixture, OutboxHostDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        claimed.Data!.Single().Payload.Should().NotBe("""{"detail":"a value"}""");
    }

    [Fact]
    public async Task AStagedMessage_KeepsItsStagedInstantThroughTheTimestampColumn()
    {
        await StageAsync();

        await using var db = OutboxHostDbContextFixture.Build(_connectionString);
        var store = new EfOutboxStore<IOutboxHostDbFixture, OutboxHostDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        claimed.Data.Should().ContainSingle().Which.StagedAt.Should().Be(sr_now);
    }

    [Fact]
    public async Task APublishedState_IsDurableAndNoLongerClaimable()
    {
        var id = await StageAsync();

        await using (var writer = OutboxHostDbContextFixture.Build(_connectionString))
        {
            var store = new EfOutboxStore<IOutboxHostDbFixture, OutboxHostDbContextFixture>(writer);
            var claimed = await store.ClaimDueAsync(
                sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

            var recorded = await store.RecordAsync(
                claimed.Data!.Single().Publish(sr_now),
                TestContext.Current.CancellationToken);

            recorded.IsOk.Should().BeTrue();
        }

        await using var reader = OutboxHostDbContextFixture.Build(_connectionString);
        var again = await new EfOutboxStore<
            IOutboxHostDbFixture, OutboxHostDbContextFixture>(reader)
            .ClaimDueAsync(sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        again.Data.Should().BeEmpty("the message with id " + id + " was confirmed");
    }

    [Fact]
    public async Task AnAbandonedState_KeepsItsPayloadAndFailureCodeOnTheRow()
    {
        // Abandonment is escalation rather than deletion: an operator has to be
        // able to read what was never delivered and decide whether to re-stage it.
        await StageAsync();

        await using var db = OutboxHostDbContextFixture.Build(_connectionString);
        var store = new EfOutboxStore<IOutboxHostDbFixture, OutboxHostDbContextFixture>(db);
        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        _ = await store.RecordAsync(
            claimed.Data!.Single().Fail(sr_now, "LIB_OUTBOX_NO_DISPATCHER", 1, Duration.Zero),
            TestContext.Current.CancellationToken);

        (await ScalarAsync("SELECT last_failure_code FROM outbox_messages LIMIT 1"))
            .Should().Be("LIB_OUTBOX_NO_DISPATCHER");
    }

    /// <summary>
    /// The concurrency token. Two relay instances never claim the
    /// same batch because one holds the advisory lock, but the token is what
    /// makes that a property of the row rather than only of the lock.
    /// </summary>
    [Fact]
    public async Task AStateAnotherWriterAlreadyChanged_IsConflict()
    {
        await StageAsync();

        await using var mine = OutboxHostDbContextFixture.Build(_connectionString);
        var store = new EfOutboxStore<IOutboxHostDbFixture, OutboxHostDbContextFixture>(mine);

        // The claim tracks the row at the transaction id it carried when read.
        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);
        var message = claimed.Data!.Single();

        // A second writer moves the row, which advances that transaction id.
        await using (var theirs = OutboxHostDbContextFixture.Build(_connectionString))
        {
            await theirs.Database.ExecuteSqlRawAsync(
                "UPDATE outbox_messages SET attempts = attempts + 1",
                TestContext.Current.CancellationToken);
        }

        var recorded = await store.RecordAsync(
            message.Publish(sr_now), TestContext.Current.CancellationToken);

        recorded.IsConflict.Should().BeTrue();
    }

    [Fact]
    public async Task ClaimDueAsync_AnotherRelayClaimedTheBatchBetweenTheReadAndTheWrite_IsConflict()
    {
        var id = await StageAsync();

        // The rival claims the same row after this store has read it and before
        // this store's claim is written, which advances the row's transaction id
        // under the version this store read.
        var rivalClaim = new BeforeFirstSaveHookFixture(async ct =>
        {
            await using var theirs = OutboxHostDbContextFixture.Build(_connectionString);
            var rival = new EfOutboxStore<IOutboxHostDbFixture, OutboxHostDbContextFixture>(theirs);
            var won = await rival.ClaimDueAsync(sr_now, 10, sr_lease, ct);

            won.Data.Should().ContainSingle("the rival read the row before this store wrote");
        });

        await using var mine = new OutboxHostDbContextFixture(
            new DbContextOptionsBuilder<OutboxHostDbContextFixture>()
                .UseNpgsql(_connectionString, o => o.AddD2NodaTime())
                .AddInterceptors(rivalClaim)
                .Options);
        var store = new EfOutboxStore<IOutboxHostDbFixture, OutboxHostDbContextFixture>(mine);

        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        claimed.IsConflict.Should().BeTrue("another relay claimed the row first");
        claimed.Data.Should().BeNull("a lost claim hands nothing to dispatch");
        (await ScalarAsync($"SELECT attempts::text FROM outbox_messages WHERE id = '{id}'"))
            .Should().Be("1", "only the rival's claim counted an attempt");
    }

    [Fact]
    public async Task ClaimDueAsync_ALeaseShorterThanAMicrosecond_IsRefusedAndClaimsNothing()
    {
        // The column keeps microseconds, so this lease would be stored as now and
        // the message would be claimable again while it is still dispatched.
        await StageAsync();

        await using var db = OutboxHostDbContextFixture.Build(_connectionString);
        var store = new EfOutboxStore<IOutboxHostDbFixture, OutboxHostDbContextFixture>(db);

        var claimed = await store.ClaimDueAsync(
            sr_now, 10, Duration.FromNanoseconds(500), TestContext.Current.CancellationToken);

        claimed.IsValidationFailed.Should().BeTrue();
        (await ScalarAsync("SELECT attempts::text FROM outbox_messages"))
            .Should().Be("0", "a refused claim counts no attempt");
    }

    [Fact]
    public async Task ClaimDueAsync_ALeaseOfOneMillisecond_IsStoredPastNow()
    {
        // The admitting case at the floor, through the real column.
        await StageAsync();

        await using (var first = OutboxHostDbContextFixture.Build(_connectionString))
        {
            var claimed = await new EfOutboxStore<IOutboxHostDbFixture, OutboxHostDbContextFixture>(first)
                .ClaimDueAsync(
                    sr_now, 10, Duration.FromMilliseconds(1), TestContext.Current.CancellationToken);

            claimed.Data.Should().ContainSingle();
        }

        await using var second = OutboxHostDbContextFixture.Build(_connectionString);
        var again = await new EfOutboxStore<IOutboxHostDbFixture, OutboxHostDbContextFixture>(second)
            .ClaimDueAsync(sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        again.Data.Should().BeEmpty("the stored next attempt is after now");
    }

    [Fact]
    public async Task AStateNoOtherWriterTouched_IsOk()
    {
        // The admitting pair to the conflict above. Without it, a store that
        // refused every write would satisfy that test too, and the token would
        // read as working while delivery had stopped.
        await StageAsync();

        await using var mine = OutboxHostDbContextFixture.Build(_connectionString);
        var store = new EfOutboxStore<IOutboxHostDbFixture, OutboxHostDbContextFixture>(mine);
        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        var recorded = await store.RecordAsync(
            claimed.Data!.Single().Publish(sr_now),
            TestContext.Current.CancellationToken);

        recorded.IsOk.Should().BeTrue();
    }

    [Fact]
    public async Task AStateNoRowBacks_IsNotFound()
    {
        await using var db = OutboxHostDbContextFixture.Build(_connectionString);

        var recorded = await new EfOutboxStore<
            IOutboxHostDbFixture, OutboxHostDbContextFixture>(db).RecordAsync(
            new StagedOutboxMessage
            {
                Id = new OutboxMessageId(Guid.CreateVersion7()),
                Kind = _KIND,
                Payload = "{}",
                StagedAt = sr_now,
                Attempts = 0,
            },
            TestContext.Current.CancellationToken);

        recorded.IsNotFound.Should().BeTrue();
    }

    /// <summary>
    /// The retention sweep against the real provider: a published row past its
    /// window goes, and the query shape the sweep uses translates.
    /// </summary>
    [Fact]
    public async Task PruneSettledAsync_APublishedRowPastItsWindow_DeletesIt()
    {
        await StageAsync();

        await using var db = OutboxHostDbContextFixture.Build(_connectionString);
        var store = new EfOutboxStore<IOutboxHostDbFixture, OutboxHostDbContextFixture>(db);
        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        _ = await store.RecordAsync(
            claimed.Data!.Single().Publish(sr_now),
            TestContext.Current.CancellationToken);

        var pruned = await store.PruneSettledAsync(
            sr_now + Duration.FromDays(8),
            sr_now + Duration.FromDays(91),
            10,
            TestContext.Current.CancellationToken);

        pruned.IsOk.Should().BeTrue();
        pruned.Data.Should().Be(1);
        (await RowCountAsync()).Should().Be(0);
    }

    /// <summary>
    /// A row inside its window stays, and so does a staged row however old it
    /// is - it is still due for delivery.
    /// </summary>
    [Fact]
    public async Task PruneSettledAsync_ARowInsideItsWindowOrStillStaged_KeepsIt()
    {
        await StageAsync();

        await using var db = OutboxHostDbContextFixture.Build(_connectionString);
        var store = new EfOutboxStore<IOutboxHostDbFixture, OutboxHostDbContextFixture>(db);

        var stagedKept = await store.PruneSettledAsync(
            sr_now + Duration.FromDays(3650),
            sr_now + Duration.FromDays(3650),
            10,
            TestContext.Current.CancellationToken);

        stagedKept.Data.Should().Be(0, "a staged message is still owed delivery");

        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);
        _ = await store.RecordAsync(
            claimed.Data!.Single().Publish(sr_now),
            TestContext.Current.CancellationToken);

        var insideWindow = await store.PruneSettledAsync(
            sr_now - Duration.FromDays(1),
            sr_now - Duration.FromDays(1),
            10,
            TestContext.Current.CancellationToken);

        insideWindow.Data.Should().Be(0, "the row was confirmed after the cut-off");
        (await RowCountAsync()).Should().Be(1);
    }

    /// <summary>
    /// An abandoned row is deleted on its own window, which is the longer one:
    /// the payload an operator might re-stage is kept until it passes.
    /// </summary>
    [Fact]
    public async Task PruneSettledAsync_AnAbandonedRowPastItsWindow_DeletesIt()
    {
        await StageAsync();

        await using var db = OutboxHostDbContextFixture.Build(_connectionString);
        var store = new EfOutboxStore<IOutboxHostDbFixture, OutboxHostDbContextFixture>(db);
        var claimed = await store.ClaimDueAsync(
            sr_now, 10, sr_lease, TestContext.Current.CancellationToken);

        _ = await store.RecordAsync(
            claimed.Data!.Single().Fail(sr_now, "LIB_OUTBOX_NO_DISPATCHER", 1, Duration.Zero),
            TestContext.Current.CancellationToken);

        var insideWindow = await store.PruneSettledAsync(
            sr_now + Duration.FromDays(8),
            sr_now - Duration.FromDays(1),
            10,
            TestContext.Current.CancellationToken);

        insideWindow.Data.Should().Be(0, "the abandoned row has its own, longer window");

        var pastWindow = await store.PruneSettledAsync(
            sr_now + Duration.FromDays(8),
            sr_now + Duration.FromDays(91),
            10,
            TestContext.Current.CancellationToken);

        pastWindow.Data.Should().Be(1);
    }

    [Fact]
    public async Task PruneSettledAsync_ABatchSizeThatIsNotPositive_IsRefused()
    {
        await using var db = OutboxHostDbContextFixture.Build(_connectionString);
        var store = new EfOutboxStore<IOutboxHostDbFixture, OutboxHostDbContextFixture>(db);

        var refused = await store.PruneSettledAsync(
            sr_now, sr_now, 0, TestContext.Current.CancellationToken);

        refused.IsValidationFailed.Should().BeTrue();
    }

    private async Task<Guid> StageAsync()
    {
        await using var db = OutboxHostDbContextFixture.Build(_connectionString);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM outbox_messages", TestContext.Current.CancellationToken);

        var outbox = new EfMessageOutbox<IOutboxHostDbFixture, OutboxHostDbContextFixture>(
            db, new TestClock(sr_now));
        _ = await outbox.StageAsync(
            SampleOutboxIntegrationKind.ThingHappened,
            new SampleOutboxIntegrationPayload("a value"),
            TestContext.Current.CancellationToken);

        var id = db.ChangeTracker.Entries<OutboxMessageRow>().Single().Entity.Id;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return id;
    }

    private async Task<string?> ColumnTypeAsync(string column)
        => await ScalarAsync(
            "SELECT data_type FROM information_schema.columns WHERE table_name = "
            + $"'outbox_messages' AND column_name = '{column}'");

    private async Task<long> RowCountAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM outbox_messages";

        return (long)(await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private async Task<string?> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        var value = await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        return value as string;
    }
}
