// -----------------------------------------------------------------------
// <copyright file="OutboxRelaySerializationIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Outbox;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Outbox;
using DcsvIo.D2.Outbox.EntityFrameworkCore;
using DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Tests.Integration.DataGovernance;
using DcsvIo.D2.Tests.Integration.Outbox.Fixtures;
using DcsvIo.D2.Tests.Unit.Handler;
using DcsvIo.D2.Tests.Unit.Outbox.Fixtures;
using DcsvIo.D2.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Npgsql;
using Xunit;

/// <summary>
/// What stops two relay instances draining one batch, against a real PostgreSQL
/// server. The mechanisms are the row's <c>xmin</c> concurrency token, which the
/// provider supplies, and the per-database session advisory lock the tick takes
/// before it claims anything. Neither can be observed in memory.
/// </summary>
[Collection("Postgres")]
[Trait("Category", "Integration")]
public sealed class OutboxRelaySerializationIntegrationTests : IAsyncLifetime
{
    private const string _KIND = "ThingHappened";

    private const long _LOCK_KEY = 907_713_001;

    private static readonly Instant sr_now = Instant.FromUtc(2026, 8, 17, 15, 0);

    private static readonly Duration sr_lease = Duration.FromSeconds(60);

    private readonly PostgresFixture r_fixture;
    private string _connectionString = string.Empty;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="OutboxRelaySerializationIntegrationTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared Postgres Testcontainers fixture.</param>
    public OutboxRelaySerializationIntegrationTests(PostgresFixture fixture)
    {
        r_fixture = fixture;
    }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        _connectionString = await r_fixture.CreateIsolatedDatabaseAsync(
            nameof(OutboxRelaySerializationIntegrationTests));

        await using var db = OutboxHostDbContextFixture.Build(_connectionString);
        await db.Database.EnsureCreatedAsync();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() =>
        await r_fixture.AssertNoConnectionOpenAsync(_connectionString);

    [Fact]
    public async Task ClaimDueAsync_TwoConcurrentCallers_ClaimEachRowAtMostOnce()
    {
        // The claim is a read followed by a save with no explicit transaction
        // and no FOR UPDATE SKIP LOCKED, so what stops a second caller taking
        // the same batch is the xmin token the provider writes. A test above
        // this level cannot observe whether that holds.
        var staged = await StageManyAsync(8);

        await using var firstDb = OutboxHostDbContextFixture.Build(_connectionString);
        await using var secondDb = OutboxHostDbContextFixture.Build(_connectionString);

        var first = new EfOutboxStore<IOutboxHostDbFixture, OutboxHostDbContextFixture>(firstDb);
        var second = new EfOutboxStore<IOutboxHostDbFixture, OutboxHostDbContextFixture>(secondDb);

        var results = await Task.WhenAll(
            Task.Run(async () =>
                await first.ClaimDueAsync(
                    sr_now, 8, sr_lease, TestContext.Current.CancellationToken)),
            Task.Run(async () =>
                await second.ClaimDueAsync(
                    sr_now, 8, sr_lease, TestContext.Current.CancellationToken)));

        var claimedIds = results
            .Where(r => r.IsOk)
            .SelectMany(r => r.Data!.Select(m => m.Id.Value))
            .ToList();

        claimedIds.Should().OnlyHaveUniqueItems(
            "a staged row is claimed by exactly one caller, or the same message is "
            + "delivered twice");
        claimedIds.Should().BeSubsetOf(staged);

        // Whatever the losing caller returned, the rows it read must not carry
        // a second attempt: two claims on one row would count two.
        (await AttemptsAsync()).Should().OnlyContain(
            a => a <= 1,
            "one claim counts one attempt on a row");
    }

    [Fact]
    public async Task RunTickAsync_TheRelayLockIsHeldElsewhere_ClaimsAndDispatchesNothing()
    {
        var staged = await StageManyAsync(3);

        await using var held = await PgAdvisoryLock.TryAcquireSessionAsync(
            _connectionString, _LOCK_KEY, TestContext.Current.CancellationToken);

        held.IsHeld.Should().BeTrue("the test session owns the lock for this case");

        var dispatcher = new FakeOutboxDispatcher(_KIND);
        var (relay, logger, provider) = BuildRelay(dispatcher);

        using (provider)
        {
            await relay.RunTickAsync(
                relay.BuildIndex(), TestContext.Current.CancellationToken);
        }

        dispatcher.Calls.Should().Be(0, "the tick took no lock, so it drained nothing");
        logger.Entries.Should().Contain(e => e.EventId.Id == 2);
        (await AttemptsAsync()).Should().AllSatisfy(a => a.Should().Be(0));
        (await StagedIdsAsync()).Should().BeEquivalentTo(staged);
    }

    [Fact]
    public async Task RunTickAsync_TheRelayLockIsFree_ClaimsAndDispatches()
    {
        // The admitting side. Without it the case above passes against a tick
        // that never claims anything whatever the lock says.
        var staged = await StageManyAsync(3);

        var dispatcher = new FakeOutboxDispatcher(_KIND);
        var (relay, logger, provider) = BuildRelay(dispatcher);

        using (provider)
        {
            await relay.RunTickAsync(
                relay.BuildIndex(), TestContext.Current.CancellationToken);
        }

        dispatcher.Calls.Should().Be(staged.Count);
        logger.Entries.Should().NotContain(e => e.EventId.Id == 2);
        (await StagedIdsAsync()).Should().BeEmpty("every staged row was published");
    }

    private (OutboxRelay<IOutboxHostDbFixture> Relay,
        TestLogger<OutboxRelay<IOutboxHostDbFixture>> Logger,
        ServiceProvider Provider) BuildRelay(IOutboxDispatcher dispatcher)
    {
        var logger = new TestLogger<OutboxRelay<IOutboxHostDbFixture>>();

        var services = new ServiceCollection();
        services.AddDbContext<OutboxHostDbContextFixture>(
            o => o.UseNpgsql(_connectionString, n => n.AddD2NodaTime()));
        services.AddScoped<IOutboxStore<IOutboxHostDbFixture>>(sp =>
            new EfOutboxStore<IOutboxHostDbFixture, OutboxHostDbContextFixture>(
                sp.GetRequiredService<OutboxHostDbContextFixture>()));

        var provider = services.BuildServiceProvider();

        var options = new OutboxOptionsFor<IOutboxHostDbFixture>
        {
            RelayAdvisoryLockKey = _LOCK_KEY,
            RelayConnectionString = _connectionString,
        };

        var relay = new OutboxRelay<IOutboxHostDbFixture>(
            new FakeSystemWorkScopeFactory(provider),
            FakeOutboxDispatcherBinding<IOutboxHostDbFixture>.Bind(dispatcher),
            new TestClock(sr_now),
            new StaticOptionsMonitorFixture<OutboxOptionsFor<IOutboxHostDbFixture>>(options),
            logger,
            new FakeServiceProviderIsService(answer: true));

        return (relay, logger, provider);
    }

    private async Task<List<Guid>> StageManyAsync(int count)
    {
        await using var db = OutboxHostDbContextFixture.Build(_connectionString);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM outbox_messages", TestContext.Current.CancellationToken);

        var outbox = new EfMessageOutbox<IOutboxHostDbFixture, OutboxHostDbContextFixture>(
            db, new TestClock(sr_now));

        for (var i = 0; i < count; i++)
        {
            _ = await outbox.StageAsync(
                SampleOutboxIntegrationKind.ThingHappened,
                new SampleOutboxIntegrationPayload("value " + i),
                TestContext.Current.CancellationToken);
        }

        var ids = db.ChangeTracker.Entries<OutboxMessageRow>()
            .Select(e => e.Entity.Id)
            .ToList();

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return ids;
    }

    private async Task<List<int>> AttemptsAsync()
        => await ReadAsync("SELECT attempts FROM outbox_messages", r => r.GetInt32(0));

    private async Task<List<Guid>> StagedIdsAsync()
        => await ReadAsync(
            "SELECT id FROM outbox_messages WHERE status = 'Staged'", r => r.GetGuid(0));

    private async Task<List<T>> ReadAsync<T>(string sql, Func<NpgsqlDataReader, T> read)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        await using var reader = await cmd.ExecuteReaderAsync(
            TestContext.Current.CancellationToken);

        var values = new List<T>();

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            values.Add(read(reader));

        return values;
    }
}
