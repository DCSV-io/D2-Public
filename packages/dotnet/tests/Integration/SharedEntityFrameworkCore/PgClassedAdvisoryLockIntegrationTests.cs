// -----------------------------------------------------------------------
// <copyright file="PgClassedAdvisoryLockIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.SharedEntityFrameworkCore;

using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.EntityFrameworkCore;
using DcsvIo.D2.Tests.Integration.DataGovernance;
using DcsvIo.D2.Tests.Integration.SharedEntityFrameworkCorePostgres.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Xunit;

/// <summary>
/// Live-database tests for <see cref="PgClassedAdvisoryLock"/>: what the lock
/// serializes, what the budget rejects, and how the disjoint keyspace behaves.
/// </summary>
[Collection("Postgres")]
[Trait("Category", "Integration")]
public sealed class PgClassedAdvisoryLockIntegrationTests : IAsyncLifetime
{
    private const int _CLASS = 7710001;
    private const int _OTHER_CLASS = 7710002;

    /// <summary>Takes one probe row with FOR UPDATE.</summary>
    private const string _ROW_LOCK_SQL =
        $"SELECT 1 AS \"Value\" FROM \"{LockHostDbContextFixture.TABLE_NAME}\" "
        + $"WHERE \"{LockHostDbContextFixture.KEY_COLUMN_NAME}\" = {{0}} FOR UPDATE";

    private readonly PostgresFixture r_fixture;
    private string _connectionString = string.Empty;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="PgClassedAdvisoryLockIntegrationTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared PostgreSQL container.</param>
    public PgClassedAdvisoryLockIntegrationTests(PostgresFixture fixture)
    {
        r_fixture = fixture;
    }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        _connectionString = await r_fixture.CreateIsolatedDatabaseAsync(
            nameof(PgClassedAdvisoryLockIntegrationTests));

        await LockHostDbContextFixture.EnsureSchemaAsync(_connectionString);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() =>
        await r_fixture.AssertNoConnectionOpenAsync(_connectionString);

    [Fact]
    public async Task ExecuteLockedAsync_TwoCallersOnOneKey_RunOneAfterTheOther()
    {
        var key = Guid.CreateVersion7();
        var inside = 0;
        var overlapped = false;
        var firstEntered = new TaskCompletionSource();
        var firstMayLeave = new TaskCompletionSource();

        await using var one = LockHostDbContextFixture.Build(_connectionString);
        await using var two = LockHostDbContextFixture.Build(_connectionString);
        await using var probe = LockHostDbContextFixture.Build(_connectionString);

        var first = PgClassedAdvisoryLock.ExecuteLockedAsync(
            one,
            _CLASS,
            key,
            async _ =>
            {
                if (Interlocked.Increment(ref inside) > 1)
                    overlapped = true;

                firstEntered.SetResult();
                await firstMayLeave.Task;
                Interlocked.Decrement(ref inside);
            },
            TestContext.Current.CancellationToken);

        await firstEntered.Task;

        var second = PgClassedAdvisoryLock.ExecuteLockedAsync(
            two,
            _CLASS,
            key,
            _ =>
            {
                if (Interlocked.Increment(ref inside) > 1)
                    overlapped = true;

                Interlocked.Decrement(ref inside);
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        // The holder stays inside its body until the key is observed held. A
        // zero-budget attempt on a third connection reports that without
        // queueing, so the case fails when the lock stops serializing rather
        // than passing on how the callers happened to be scheduled.
        var probeRan = await PgClassedAdvisoryLock.TryExecuteLockedAsync(
            probe,
            _CLASS,
            key,
            _ => Task.CompletedTask,
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        probeRan.Should().BeFalse("the first caller still holds the key");

        firstMayLeave.SetResult();
        await first;
        await second;

        overlapped.Should().BeFalse("the lock keeps the two bodies apart");
    }

    [Fact]
    public async Task ExecuteLockedAsync_TwoCallersOnDifferentClasses_DoNotWaitForEachOther()
    {
        var key = Guid.CreateVersion7();
        var firstEntered = new TaskCompletionSource();
        var firstMayLeave = new TaskCompletionSource();

        await using var one = LockHostDbContextFixture.Build(_connectionString);
        await using var two = LockHostDbContextFixture.Build(_connectionString);

        var first = PgClassedAdvisoryLock.ExecuteLockedAsync(
            one,
            _CLASS,
            key,
            async _ =>
            {
                firstEntered.SetResult();
                await firstMayLeave.Task;
            },
            TestContext.Current.CancellationToken);

        await firstEntered.Task;

        // NOWAIT rather than the waiting form: the case exists to catch the two
        // classes stopping to be disjoint, and the waiting form would block on
        // the held key with the release signal below it, so the regression would
        // report as a run that never ends. This project ships no framework test
        // timeout.
        var ranOnTheOtherClass = await PgClassedAdvisoryLock.TryExecuteLockedAsync(
            two,
            _OTHER_CLASS,
            key,
            _ => Task.CompletedTask,
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        ranOnTheOtherClass.Should().BeTrue(
            "one key under two classes is two locks, so neither caller waits");

        firstMayLeave.SetResult();
        await first;
    }

    [Fact]
    public async Task ExecuteLockedAsync_Generic_AnswersWhatTheWorkAnswered()
    {
        await using var db = LockHostDbContextFixture.Build(_connectionString);

        var answer = await PgClassedAdvisoryLock.ExecuteLockedAsync(
            db,
            _CLASS,
            Guid.CreateVersion7(),
            _ => ValueTask.FromResult(41 + 1),
            TestContext.Current.CancellationToken);

        answer.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteLockedAsync_TheWorkThrew_ReleasesTheLock()
    {
        var key = Guid.CreateVersion7();

        await using var one = LockHostDbContextFixture.Build(_connectionString);
        await using var two = LockHostDbContextFixture.Build(_connectionString);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await PgClassedAdvisoryLock.ExecuteLockedAsync(
                one,
                _CLASS,
                key,
                _ => throw new InvalidOperationException("deliberate"),
                TestContext.Current.CancellationToken));

        thrown.Message.Should().Be("deliberate");

        var ran = await PgClassedAdvisoryLock.TryExecuteLockedAsync(
            two,
            _CLASS,
            key,
            _ => Task.CompletedTask,
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        ran.Should().BeTrue("a throw releases the lock on the way out");
    }

    [Fact]
    public async Task TryExecuteLockedAsync_NoBudgetAndTheKeyIsHeld_DoesNotRunTheWork()
    {
        var key = Guid.CreateVersion7();
        var firstEntered = new TaskCompletionSource();
        var firstMayLeave = new TaskCompletionSource();
        var secondRan = false;

        await using var one = LockHostDbContextFixture.Build(_connectionString);
        await using var two = LockHostDbContextFixture.Build(_connectionString);

        var holder = PgClassedAdvisoryLock.ExecuteLockedAsync(
            one,
            _CLASS,
            key,
            async _ =>
            {
                firstEntered.SetResult();
                await firstMayLeave.Task;
            },
            TestContext.Current.CancellationToken);

        await firstEntered.Task;

        var ran = await PgClassedAdvisoryLock.TryExecuteLockedAsync(
            two,
            _CLASS,
            key,
            _ =>
            {
                secondRan = true;
                return Task.CompletedTask;
            },
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        firstMayLeave.SetResult();
        await holder;

        ran.Should().BeFalse("the key was held and the caller named no budget");
        secondRan.Should().BeFalse("work that did not get the lock did not run");
    }

    [Fact]
    public async Task TryExecuteLockedAsync_NoBudgetAndTheKeyIsFree_RunsTheWorkAndCommitsIt()
    {
        var key = Guid.CreateVersion7();

        await using var db = LockHostDbContextFixture.Build(_connectionString);

        var ran = await PgClassedAdvisoryLock.TryExecuteLockedAsync(
            db,
            _CLASS,
            key,
            async inner =>
            {
                db.Probes.Add(new LockProbeFixture { Id = key, Note = "committed" });
                await db.SaveChangesAsync(inner);
            },
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        ran.Should().BeTrue("nobody held the key");

        await using var reader = LockHostDbContextFixture.Build(_connectionString);
        var row = await reader.Probes.SingleOrDefaultAsync(
            p => p.Id == key, TestContext.Current.CancellationToken);

        row.Should().NotBeNull();
        row.Note.Should().Be("committed");
    }

    [Fact]
    public async Task TryExecuteLockedAsync_TheBudgetExpiredWhileHeld_DoesNotRunTheWork()
    {
        var key = Guid.CreateVersion7();
        var firstEntered = new TaskCompletionSource();
        var firstMayLeave = new TaskCompletionSource();
        var secondRan = false;

        await using var one = LockHostDbContextFixture.Build(_connectionString);
        await using var two = LockHostDbContextFixture.Build(_connectionString);

        var holder = PgClassedAdvisoryLock.ExecuteLockedAsync(
            one,
            _CLASS,
            key,
            async _ =>
            {
                firstEntered.SetResult();
                await firstMayLeave.Task;
            },
            TestContext.Current.CancellationToken);

        await firstEntered.Task;

        var ran = await PgClassedAdvisoryLock.TryExecuteLockedAsync(
            two,
            _CLASS,
            key,
            _ =>
            {
                secondRan = true;
                return Task.CompletedTask;
            },
            TimeSpan.FromMilliseconds(150),
            TestContext.Current.CancellationToken);

        firstMayLeave.SetResult();
        await holder;

        ran.Should().BeFalse("the budget expired before the holder let go");
        secondRan.Should().BeFalse("work that did not get the lock did not run");
    }

    [Fact]
    public async Task TryExecuteLockedAsync_TheWorkThrew_RollsBackWhatItStaged()
    {
        var key = Guid.CreateVersion7();

        await using var db = LockHostDbContextFixture.Build(_connectionString);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await PgClassedAdvisoryLock.TryExecuteLockedAsync(
                db,
                _CLASS,
                key,
                async inner =>
                {
                    db.Probes.Add(new LockProbeFixture { Id = key, Note = "rolled back" });
                    await db.SaveChangesAsync(inner);
                    throw new InvalidOperationException("deliberate");
                },
                TimeSpan.Zero,
                TestContext.Current.CancellationToken));

        await using var reader = LockHostDbContextFixture.Build(_connectionString);
        var present = await reader.Probes.AnyAsync(
            p => p.Id == key, TestContext.Current.CancellationToken);

        present.Should().BeFalse("the transaction the lock opened rolled back with the throw");
    }

    [Fact]
    public async Task TryExecuteLockedAsync_ARowTheWorkSavesIsHeldPastTheBudget_AnswersFalseAndCommitsNothing()
    {
        var heldId = await SeedAsync("held");
        var stagedId = Guid.CreateVersion7();

        await using var holderDb = LockHostDbContextFixture.Build(_connectionString);
        await using var holderTx = await holderDb.Database.BeginTransactionAsync(
            TestContext.Current.CancellationToken);
        await HoldRowAsync(holderDb, heldId);

        await using var db = LockHostDbContextFixture.Build(_connectionString);

        // The advisory lock is free, so the budget can only expire on the row
        // the work's save waits for. The save reports that wait wrapped in
        // DbUpdateException rather than as a bare DbException.
        var ran = await PgClassedAdvisoryLock.TryExecuteLockedAsync(
            db,
            _CLASS,
            Guid.CreateVersion7(),
            async inner =>
            {
                var row = await db.Probes.SingleAsync(p => p.Id == heldId, inner);
                row.Note = "overwritten";
                db.Probes.Add(new LockProbeFixture { Id = stagedId, Note = "staged" });
                await db.SaveChangesAsync(inner);
            },
            TimeSpan.FromMilliseconds(150),
            TestContext.Current.CancellationToken);

        await holderTx.RollbackAsync(TestContext.Current.CancellationToken);

        ran.Should().BeFalse("a row the work waited on stayed held past the budget");

        await using var reader = LockHostDbContextFixture.Build(_connectionString);
        var staged = await reader.Probes.AnyAsync(
            p => p.Id == stagedId, TestContext.Current.CancellationToken);

        staged.Should().BeFalse("the refused transaction rolled back what the work staged");
    }

    [Fact]
    public async Task TryExecuteLockedAsync_ASaveTheBudgetRefused_LeavesNothingForALaterSaveToWrite()
    {
        var heldId = await SeedAsync("held");
        var stagedId = Guid.CreateVersion7();

        await using var db = LockHostDbContextFixture.Build(_connectionString);

        await using (var holderDb = LockHostDbContextFixture.Build(_connectionString))
        {
            await using var holderTx = await holderDb.Database.BeginTransactionAsync(
                TestContext.Current.CancellationToken);
            await HoldRowAsync(holderDb, heldId);

            var ran = await PgClassedAdvisoryLock.TryExecuteLockedAsync(
                db,
                _CLASS,
                Guid.CreateVersion7(),
                async inner =>
                {
                    var row = await db.Probes.SingleAsync(p => p.Id == heldId, inner);
                    row.Note = "overwritten";
                    db.Probes.Add(new LockProbeFixture { Id = stagedId, Note = "staged" });
                    await db.SaveChangesAsync(inner);
                },
                TimeSpan.FromMilliseconds(150),
                TestContext.Current.CancellationToken);

            await holderTx.RollbackAsync(TestContext.Current.CancellationToken);

            ran.Should().BeFalse("a row the work waited on stayed held past the budget");
        }

        // The holder has let go, so a save on the same context meets no lock
        // and writes whatever the context still tracks.
        var written = await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        written.Should().Be(0, "the refused work left nothing tracked to write");

        await using var reader = LockHostDbContextFixture.Build(_connectionString);
        var staged = await reader.Probes.AnyAsync(
            p => p.Id == stagedId, TestContext.Current.CancellationToken);
        var note = await reader.Probes
            .Where(p => p.Id == heldId)
            .Select(p => p.Note)
            .SingleAsync(TestContext.Current.CancellationToken);

        staged.Should().BeFalse("the added row was refused and not written later");
        note.Should().Be("held", "the modified row was refused and not written later");
    }

    [Fact]
    public async Task TryExecuteLockedAsync_ARowTheWorkQueriesIsHeldPastTheBudget_AnswersFalse()
    {
        var heldId = await SeedAsync("queried");

        await using var holderDb = LockHostDbContextFixture.Build(_connectionString);
        await using var holderTx = await holderDb.Database.BeginTransactionAsync(
            TestContext.Current.CancellationToken);
        await HoldRowAsync(holderDb, heldId);

        await using var db = LockHostDbContextFixture.Build(_connectionString);

        // A query reports the wait wrapped in the InvalidOperationException the
        // provider's execution strategy raises for an error it classes as
        // transient.
        var ran = await PgClassedAdvisoryLock.TryExecuteLockedAsync(
            db,
            _CLASS,
            Guid.CreateVersion7(),
            async inner => await db.Database
                .SqlQueryRaw<int>(_ROW_LOCK_SQL, heldId)
                .ToListAsync(inner),
            TimeSpan.FromMilliseconds(150),
            TestContext.Current.CancellationToken);

        await holderTx.RollbackAsync(TestContext.Current.CancellationToken);

        ran.Should().BeFalse("a row the work queried stayed held past the budget");
    }

    [Fact]
    public async Task ExecuteLockedAsync_TheKeyIsHeldPastTheCommandTimeout_ThrowsTheProviderTimeout()
    {
        var key = Guid.CreateVersion7();
        var firstEntered = new TaskCompletionSource();
        var firstMayLeave = new TaskCompletionSource();

        await using var one = LockHostDbContextFixture.Build(_connectionString);
        await using var two = LockHostDbContextFixture.Build(_connectionString);
        two.Database.SetCommandTimeout(1);

        var holder = PgClassedAdvisoryLock.ExecuteLockedAsync(
            one,
            _CLASS,
            key,
            async _ =>
            {
                firstEntered.SetResult();
                await firstMayLeave.Task;
            },
            TestContext.Current.CancellationToken);

        await firstEntered.Task;

        Exception? thrown;

        try
        {
            thrown = await Record.ExceptionAsync(
                async () => await PgClassedAdvisoryLock.ExecuteLockedAsync(
                    two,
                    _CLASS,
                    key,
                    _ => Task.CompletedTask,
                    TestContext.Current.CancellationToken));
        }
        finally
        {
            firstMayLeave.SetResult();
            await holder;
        }

        thrown.Should().NotBeNull(
            "the wait ends at the context's command timeout, not at the caller's token");
        thrown.Should().BeAssignableTo<DbException>();
        thrown.InnerException.Should().BeOfType<TimeoutException>();
    }

    [Fact]
    public async Task ExecuteLockedAsync_TheWorkLeftAFailedTransactionOpen_ReleasesTheLockBeforeTheConnectionIsPooled()
    {
        var key = Guid.CreateVersion7();
        var pooled = PooledConnectionString("classed-lock-failed-tx");

        try
        {
            await using (var db = LockHostDbContextFixture.Build(pooled))
            {
                IDbContextTransaction? leftOpen = null;

                await PgClassedAdvisoryLock.ExecuteLockedAsync(
                    db,
                    _CLASS,
                    key,
                    async inner =>
                    {
                        // The work opens a transaction, lets a statement in it
                        // fail and returns without ending it, so the session
                        // rejects every later statement with 25P02 until the
                        // transaction ends.
                        leftOpen = await db.Database.BeginTransactionAsync(inner);
                        await Assert.ThrowsAnyAsync<DbException>(
                            async () => await db.Database.ExecuteSqlRawAsync("SELECT 1/0", inner));
                    },
                    TestContext.Current.CancellationToken);

                if (leftOpen is not null)
                    await leftOpen.DisposeAsync();
            }

            await using var probe = LockHostDbContextFixture.Build(_connectionString);

            var ran = await PgClassedAdvisoryLock.TryExecuteLockedAsync(
                probe,
                _CLASS,
                key,
                _ => Task.CompletedTask,
                TimeSpan.Zero,
                TestContext.Current.CancellationToken);

            ran.Should().BeTrue(
                "the lock was released before the connection went back to the pool");
        }
        finally
        {
            ClearPool(pooled);
        }
    }

    [Fact]
    public async Task ExecuteLockedAsync_TheReleaseKeepsFailing_EndsTheSessionSoThePoolHoldsNoLock()
    {
        var key = Guid.CreateVersion7();
        var pooled = PooledConnectionString("classed-lock-failed-release");
        var failRelease = new FailingStatementInterceptorFake("pg_advisory_unlock");

        try
        {
            await using (var db = BuildIntercepted(pooled, failRelease))
            {
                await PgClassedAdvisoryLock.ExecuteLockedAsync(
                    db,
                    _CLASS,
                    key,
                    _ => Task.CompletedTask,
                    TestContext.Current.CancellationToken);
            }

            failRelease.Replaced.Should().BeGreaterThan(
                0, "the release statement has to fail for this case to mean anything");

            await using var probe = LockHostDbContextFixture.Build(_connectionString);

            var ran = await PgClassedAdvisoryLock.TryExecuteLockedAsync(
                probe,
                _CLASS,
                key,
                _ => Task.CompletedTask,
                TimeSpan.Zero,
                TestContext.Current.CancellationToken);

            ran.Should().BeTrue(
                "a session that could not release the lock was ended rather than pooled");
        }
        finally
        {
            ClearPool(pooled);
        }
    }

    [Fact]
    public async Task TryExecuteLockedAsync_TheWorkSavedThenThrew_LeavesNothingTracked()
    {
        var key = Guid.CreateVersion7();

        await using var db = LockHostDbContextFixture.Build(_connectionString);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await PgClassedAdvisoryLock.TryExecuteLockedAsync(
                db,
                _CLASS,
                key,
                async inner =>
                {
                    db.Probes.Add(new LockProbeFixture { Id = key, Note = "rolled back" });
                    await db.SaveChangesAsync(inner);
                    throw new InvalidOperationException("deliberate");
                },
                TimeSpan.Zero,
                TestContext.Current.CancellationToken));

        db.ChangeTracker.Entries().Should().BeEmpty(
            "the rollback undid the work's save, so the context tracks nothing it wrote");
    }

    [Fact]
    public async Task TryExecuteLockedAsync_TheLockStatementFailedBeforeTheWork_KeepsWhatWasTracked()
    {
        var failLock = new FailingStatementInterceptorFake("pg_advisory_xact_lock");
        var workRan = false;

        await using var db = BuildIntercepted(_connectionString, failLock);
        db.Probes.Add(new LockProbeFixture { Id = Guid.CreateVersion7(), Note = "pending" });

        await Assert.ThrowsAnyAsync<DbException>(
            async () => await PgClassedAdvisoryLock.TryExecuteLockedAsync(
                db,
                _CLASS,
                Guid.CreateVersion7(),
                _ =>
                {
                    workRan = true;
                    return Task.CompletedTask;
                },
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken));

        workRan.Should().BeFalse();
        db.ChangeTracker.Entries().Should().ContainSingle(
            "nothing the work wrote was rolled back, so what the caller tracked stays");
    }

    /// <summary>Builds a context whose commands pass through an interceptor.</summary>
    /// <param name="connectionString">Npgsql connection string.</param>
    /// <param name="interceptor">The interceptor.</param>
    /// <returns>The context.</returns>
    private static LockHostDbContextFixture BuildIntercepted(
        string connectionString, FailingStatementInterceptorFake interceptor) =>
        new(new DbContextOptionsBuilder<LockHostDbContextFixture>()
            .UseNpgsql(connectionString)
            .AddInterceptors(interceptor)
            .Options);

    /// <summary>Closes the idle connections of one pool.</summary>
    /// <param name="connectionString">The pool's connection string.</param>
    private static void ClearPool(string connectionString)
    {
        using var connection = new NpgsqlConnection(connectionString);
        NpgsqlConnection.ClearPool(connection);
    }

    /// <summary>Takes one probe row inside the transaction open on the context.</summary>
    /// <param name="holderDb">The context whose open transaction holds the row.</param>
    /// <param name="id">The row to hold.</param>
    /// <returns>A task that completes when the row is held.</returns>
    private static async Task HoldRowAsync(LockHostDbContextFixture holderDb, Guid id) =>
        await holderDb.Database
            .SqlQueryRaw<int>(_ROW_LOCK_SQL, id)
            .ToListAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// The isolated database's connection string with pooling on, under an
    /// application name that gives the case a pool of its own.
    /// </summary>
    /// <param name="applicationName">The pool's application name.</param>
    /// <returns>The connection string.</returns>
    private string PooledConnectionString(string applicationName) =>
        new NpgsqlConnectionStringBuilder(_connectionString)
        {
            Pooling = true,
            ApplicationName = applicationName,
        }.ConnectionString;

    /// <summary>Commits one probe row.</summary>
    /// <param name="note">The row's note.</param>
    /// <returns>The row's key.</returns>
    private async Task<Guid> SeedAsync(string note)
    {
        var id = Guid.CreateVersion7();

        await using var db = LockHostDbContextFixture.Build(_connectionString);
        db.Probes.Add(new LockProbeFixture { Id = id, Note = note });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return id;
    }
}
