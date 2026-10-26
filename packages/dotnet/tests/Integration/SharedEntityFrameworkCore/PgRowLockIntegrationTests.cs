// -----------------------------------------------------------------------
// <copyright file="PgRowLockIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.SharedEntityFrameworkCore;

using System;
using System.Data.Common;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.EntityFrameworkCore;
using DcsvIo.D2.Tests.Integration.DataGovernance;
using DcsvIo.D2.Tests.Integration.SharedEntityFrameworkCorePostgres.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// Live-database tests for <see cref="PgRowLock"/> against a mixed-case table and
/// key column, which is where an undelimited identifier fails.
/// </summary>
[Collection("Postgres")]
[Trait("Category", "Integration")]
public sealed class PgRowLockIntegrationTests : IAsyncLifetime
{
    private readonly PostgresFixture r_fixture;
    private string _connectionString = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="PgRowLockIntegrationTests"/>
    /// class.
    /// </summary>
    /// <param name="fixture">The shared PostgreSQL container.</param>
    public PgRowLockIntegrationTests(PostgresFixture fixture)
    {
        r_fixture = fixture;
    }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        _connectionString = await r_fixture.CreateIsolatedDatabaseAsync(
            nameof(PgRowLockIntegrationTests));

        await LockHostDbContextFixture.EnsureSchemaAsync(_connectionString);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() =>
        await r_fixture.AssertNoConnectionOpenAsync(_connectionString);

    [Fact]
    public async Task TryExecuteLockedAsync_TheRowIsFree_RunsTheWorkAndCommitsIt()
    {
        var id = await SeedAsync("before");

        await using var db = LockHostDbContextFixture.Build(_connectionString);

        var ran = await PgRowLock.TryExecuteLockedAsync<LockProbeFixture>(
            db,
            p => p.Id,
            id,
            async inner =>
            {
                var row = await db.Probes.SingleAsync(p => p.Id == id, inner);
                row.Note = "after";
                await db.SaveChangesAsync(inner);
            },
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        ran.Should().BeTrue("nobody held the row");

        await using var reader = LockHostDbContextFixture.Build(_connectionString);
        var note = await reader.Probes
            .Where(p => p.Id == id)
            .Select(p => p.Note)
            .SingleAsync(TestContext.Current.CancellationToken);

        note.Should().Be("after");
    }

    [Fact]
    public async Task TryExecuteLockedAsync_TheRowIsHeldAndTheBudgetExpires_DoesNotRunTheWork()
    {
        var id = await SeedAsync("held");
        var holderTook = new TaskCompletionSource();
        var holderMayLeave = new TaskCompletionSource();
        var ran = false;

        await using var waiterDb = LockHostDbContextFixture.Build(_connectionString);

        var holder = Task.Run(
            async () =>
            {
                try
                {
                    await using var holderDb = LockHostDbContextFixture.Build(
                        _connectionString);
                    await using var tx = await holderDb.Database.BeginTransactionAsync(
                        TestContext.Current.CancellationToken);

                    await holderDb.Database.ExecuteSqlRawAsync(
                        $"SELECT 1 FROM \"{LockHostDbContextFixture.TABLE_NAME}\" "
                        + $"WHERE \"{LockHostDbContextFixture.KEY_COLUMN_NAME}\" = {{0}} FOR UPDATE",
                        [id],
                        TestContext.Current.CancellationToken);

                    holderTook.SetResult();
                    await holderMayLeave.Task;
                    await tx.RollbackAsync(TestContext.Current.CancellationToken);
                }
                catch (Exception ex)
                {
                    // Without this the awaiting test thread blocks on
                    // holderTook.Task forever and the run reports a hang rather
                    // than the fault that caused it.
                    holderTook.TrySetException(ex);
                    throw;
                }
            },
            TestContext.Current.CancellationToken);

        await holderTook.Task;

        var held = await PgRowLock.TryExecuteLockedAsync<LockProbeFixture>(
            waiterDb,
            p => p.Id,
            id,
            _ =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            TimeSpan.FromMilliseconds(150),
            TestContext.Current.CancellationToken);

        holderMayLeave.SetResult();
        await holder;

        held.Should().BeFalse("the row belonged to somebody else past the budget");
        ran.Should().BeFalse("work that did not get the row did not run");
    }

    [Fact]
    public async Task
        TryExecuteLockedAsync_InsideACallersTransaction_EnlistsRatherThanOpeningASecond()
    {
        var id = await SeedAsync("ambient");

        await using var db = LockHostDbContextFixture.Build(_connectionString);
        await using var tx = await db.Database.BeginTransactionAsync(
            TestContext.Current.CancellationToken);

        var ran = await PgRowLock.TryExecuteLockedAsync<LockProbeFixture>(
            db,
            p => p.Id,
            id,
            async inner =>
            {
                var row = await db.Probes.SingleAsync(p => p.Id == id, inner);
                row.Note = "enlisted";
                await db.SaveChangesAsync(inner);
            },
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        ran.Should().BeTrue();

        await tx.RollbackAsync(TestContext.Current.CancellationToken);

        await using var reader = LockHostDbContextFixture.Build(_connectionString);
        var note = await reader.Probes
            .Where(p => p.Id == id)
            .Select(p => p.Note)
            .SingleAsync(TestContext.Current.CancellationToken);

        note.Should().Be(
            "ambient",
            "the helper wrote inside the caller's transaction, so the caller's "
            + "rollback took the write with it");
    }

    /// <summary>
    /// The budget is applied as <c>lock_timeout</c>, which the engine keeps for
    /// the rest of the transaction. On the enlisting branch that transaction is
    /// the caller's, so a budget left behind would bound every wait the caller
    /// takes afterwards with a value it never asked for.
    /// </summary>
    [Fact]
    public async Task TryExecuteLockedAsync_InsideACallersTransaction_PutsTheCallersLockBudgetBack()
    {
        var id = await SeedAsync("budget");

        await using var db = LockHostDbContextFixture.Build(_connectionString);
        await using var tx = await db.Database.BeginTransactionAsync(
            TestContext.Current.CancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            "SELECT set_config('lock_timeout', {0}, true)",
            ["7s"],
            TestContext.Current.CancellationToken);

        await PgRowLock.TryExecuteLockedAsync<LockProbeFixture>(
            db,
            p => p.Id,
            id,
            _ => Task.CompletedTask,
            TimeSpan.FromMilliseconds(150),
            TestContext.Current.CancellationToken);

        var budget = await db.Database
            .SqlQueryRaw<string>("SELECT current_setting('lock_timeout') AS \"Value\"")
            .SingleAsync(TestContext.Current.CancellationToken);

        await tx.RollbackAsync(TestContext.Current.CancellationToken);

        budget.Should().Be(
            "7s",
            "the helper bounded its own wait for the row and gave the caller's "
            + "transaction back the budget it was running under");
    }

    /// <summary>
    /// The same restore must not widen the wait the helper itself takes: a
    /// caller with a generous transaction-level budget still fails at the helper's
    /// own, much shorter, budget for the row.
    /// </summary>
    [Fact]
    public async Task TryExecuteLockedAsync_InsideACallersTransaction_StillRefusesAtItsOwnBudget()
    {
        var id = await SeedAsync("bounded");
        var holderTook = new TaskCompletionSource();
        var holderMayLeave = new TaskCompletionSource();

        await using var waiterDb = LockHostDbContextFixture.Build(_connectionString);

        var holder = Task.Run(
            async () =>
            {
                try
                {
                    await using var holderDb = LockHostDbContextFixture.Build(
                        _connectionString);
                    await using var holderTx = await holderDb.Database.BeginTransactionAsync(
                        TestContext.Current.CancellationToken);

                    await holderDb.Database.ExecuteSqlRawAsync(
                        $"SELECT 1 FROM \"{LockHostDbContextFixture.TABLE_NAME}\" "
                        + $"WHERE \"{LockHostDbContextFixture.KEY_COLUMN_NAME}\" = {{0}} FOR UPDATE",
                        [id],
                        TestContext.Current.CancellationToken);

                    holderTook.SetResult();
                    await holderMayLeave.Task;
                    await holderTx.RollbackAsync(TestContext.Current.CancellationToken);
                }
                catch (Exception ex)
                {
                    // Without this the awaiting test thread blocks on
                    // holderTook.Task forever and the run reports a hang rather
                    // than the fault that caused it.
                    holderTook.TrySetException(ex);
                    throw;
                }
            },
            TestContext.Current.CancellationToken);

        await holderTook.Task;

        await using var tx = await waiterDb.Database.BeginTransactionAsync(
            TestContext.Current.CancellationToken);

        await waiterDb.Database.ExecuteSqlRawAsync(
            "SELECT set_config('lock_timeout', {0}, true)",
            ["30s"],
            TestContext.Current.CancellationToken);

        var refused = await Assert.ThrowsAnyAsync<DbException>(
            async () => await PgRowLock.TryExecuteLockedAsync<LockProbeFixture>(
                waiterDb,
                p => p.Id,
                id,
                _ => Task.CompletedTask,
                TimeSpan.FromMilliseconds(150),
                TestContext.Current.CancellationToken));

        holderMayLeave.SetResult();
        await holder;

        refused.SqlState.Should().Be(
            "55P03",
            "the helper's own budget bounded the wait, not the caller's longer one");
    }

    [Fact]
    public async Task TryExecuteLockedAsync_NoBudgetAndTheRowIsHeld_DoesNotRunTheWork()
    {
        var id = await SeedAsync("nowait");
        var holderTook = new TaskCompletionSource();
        var holderMayLeave = new TaskCompletionSource();
        var ran = false;

        await using var waiterDb = LockHostDbContextFixture.Build(_connectionString);

        var holder = Task.Run(
            async () =>
            {
                try
                {
                    await using var holderDb = LockHostDbContextFixture.Build(
                        _connectionString);
                    await using var tx = await holderDb.Database.BeginTransactionAsync(
                        TestContext.Current.CancellationToken);

                    await holderDb.Database.ExecuteSqlRawAsync(
                        $"SELECT 1 FROM \"{LockHostDbContextFixture.TABLE_NAME}\" "
                        + $"WHERE \"{LockHostDbContextFixture.KEY_COLUMN_NAME}\" = {{0}} FOR UPDATE",
                        [id],
                        TestContext.Current.CancellationToken);

                    holderTook.SetResult();
                    await holderMayLeave.Task;
                    await tx.RollbackAsync(TestContext.Current.CancellationToken);
                }
                catch (Exception ex)
                {
                    // Without this the awaiting test thread blocks on
                    // holderTook.Task forever and the run reports a hang rather
                    // than the fault that caused it.
                    holderTook.TrySetException(ex);
                    throw;
                }
            },
            TestContext.Current.CancellationToken);

        await holderTook.Task;

        var held = await PgRowLock.TryExecuteLockedAsync<LockProbeFixture>(
            waiterDb,
            p => p.Id,
            id,
            _ =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        holderMayLeave.SetResult();
        await holder;

        held.Should().BeFalse("NOWAIT refuses rather than queueing");
        ran.Should().BeFalse();
    }

    /// <summary>
    /// A row that is not there cannot be held, so nothing serializes the work
    /// and it does not run. Two callers that target a row neither has created
    /// would otherwise both proceed with no lock between them.
    /// </summary>
    [Fact]
    public async Task TryExecuteLockedAsync_NoSuchRow_DoesNotRunTheWork()
    {
        await using var db = LockHostDbContextFixture.Build(_connectionString);
        var ran = false;

        var held = await PgRowLock.TryExecuteLockedAsync<LockProbeFixture>(
            db,
            p => p.Id,
            Guid.CreateVersion7(),
            _ =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        held.Should().BeFalse("no row carries that key, so nothing was locked");
        ran.Should().BeFalse("work that got no lock did not run");
    }

    /// <summary>
    /// The waiting form runs the work whether or not a row was there to hold,
    /// and reports which of the two happened. This pins that contract: a caller
    /// that reads the row itself still gets its work run, and the result says
    /// the row was not held.
    /// </summary>
    [Fact]
    public async Task ExecuteLockedAsync_NoSuchRow_StillRunsTheWorkAndReportsTheRowWasNotHeld()
    {
        await using var db = LockHostDbContextFixture.Build(_connectionString);
        var ran = false;

        var held = await PgRowLock.ExecuteLockedAsync<LockProbeFixture>(
            db,
            p => p.Id,
            Guid.CreateVersion7(),
            _ =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        ran.Should().BeTrue(
            "the waiting form runs the work even when no row carries the key");
        held.Should().BeFalse("no row carries that key, so nothing was locked");
    }

    /// <summary>
    /// The same call against a row that exists holds it and runs the work, so
    /// the result above is caused by the absent row and not by the query shape.
    /// </summary>
    [Fact]
    public async Task ExecuteLockedAsync_TheRowExists_RunsTheWorkAndReportsTheRowWasHeld()
    {
        var id = await SeedAsync("waiting");

        await using var db = LockHostDbContextFixture.Build(_connectionString);
        var ran = false;

        var held = await PgRowLock.ExecuteLockedAsync<LockProbeFixture>(
            db,
            p => p.Id,
            id,
            _ =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        ran.Should().BeTrue();
        held.Should().BeTrue(
            "the row is there, so the work ran serialized against it");
    }

    /// <summary>
    /// The work does not run for an absent row inside the caller's own
    /// transaction either, where the helper enlists rather than opening one of
    /// its own.
    /// </summary>
    [Fact]
    public async Task TryExecuteLockedAsync_NoSuchRowInsideACallersTransaction_DoesNotRunTheWork()
    {
        await using var db = LockHostDbContextFixture.Build(_connectionString);
        await using var tx = await db.Database.BeginTransactionAsync(
            TestContext.Current.CancellationToken);

        var ran = false;

        var held = await PgRowLock.TryExecuteLockedAsync<LockProbeFixture>(
            db,
            p => p.Id,
            Guid.CreateVersion7(),
            _ =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        await tx.RollbackAsync(TestContext.Current.CancellationToken);

        held.Should().BeFalse();
        ran.Should().BeFalse();
    }

    [Fact]
    public async Task TryExecuteLockedAsync_TheWorkThrew_RollsBackWhatItStaged()
    {
        var id = await SeedAsync("kept");

        await using var db = LockHostDbContextFixture.Build(_connectionString);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await PgRowLock.TryExecuteLockedAsync<LockProbeFixture>(
                db,
                p => p.Id,
                id,
                async inner =>
                {
                    var row = await db.Probes.SingleAsync(p => p.Id == id, inner);
                    row.Note = "discarded";
                    await db.SaveChangesAsync(inner);
                    throw new InvalidOperationException("deliberate");
                },
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken));

        await using var reader = LockHostDbContextFixture.Build(_connectionString);
        var note = await reader.Probes
            .Where(p => p.Id == id)
            .Select(p => p.Note)
            .SingleAsync(TestContext.Current.CancellationToken);

        note.Should().Be("kept", "the transaction the helper opened rolled back with the throw");
    }

    [Fact]
    public async Task TryExecuteLockedAsync_ASecondRowTheWorkSavesIsHeldPastTheBudget_AnswersFalse()
    {
        var lockedId = await SeedAsync("locked");
        var heldId = await SeedAsync("held elsewhere");

        await using var holderDb = LockHostDbContextFixture.Build(_connectionString);
        await using var holderTx = await holderDb.Database.BeginTransactionAsync(
            TestContext.Current.CancellationToken);
        await holderDb.Database
            .SqlQueryRaw<int>(
                $"SELECT 1 AS \"Value\" FROM \"{LockHostDbContextFixture.TABLE_NAME}\" "
                + $"WHERE \"{LockHostDbContextFixture.KEY_COLUMN_NAME}\" = {{0}} FOR UPDATE",
                heldId)
            .ToListAsync(TestContext.Current.CancellationToken);

        await using var db = LockHostDbContextFixture.Build(_connectionString);

        // The locked row is free, so the budget can only expire on the second
        // row the work's save waits for, which the save reports wrapped in
        // DbUpdateException rather than as a bare DbException.
        var ran = await PgRowLock.TryExecuteLockedAsync<LockProbeFixture>(
            db,
            p => p.Id,
            lockedId,
            async inner =>
            {
                var row = await db.Probes.SingleAsync(p => p.Id == heldId, inner);
                row.Note = "overwritten";
                await db.SaveChangesAsync(inner);
            },
            TimeSpan.FromMilliseconds(150),
            TestContext.Current.CancellationToken);

        await holderTx.RollbackAsync(TestContext.Current.CancellationToken);

        ran.Should().BeFalse("a row the work waited on stayed held past the budget");
    }

    [Fact]
    public async Task TryExecuteLockedAsync_ASaveTheBudgetRefused_LeavesNothingForALaterSaveToWrite()
    {
        var lockedId = await SeedAsync("locked");
        var heldId = await SeedAsync("held elsewhere");
        var stagedId = Guid.CreateVersion7();

        await using var db = LockHostDbContextFixture.Build(_connectionString);

        await using (var holderDb = LockHostDbContextFixture.Build(_connectionString))
        {
            await using var holderTx = await holderDb.Database.BeginTransactionAsync(
                TestContext.Current.CancellationToken);
            await holderDb.Database
                .SqlQueryRaw<int>(
                    $"SELECT 1 AS \"Value\" FROM \"{LockHostDbContextFixture.TABLE_NAME}\" "
                    + $"WHERE \"{LockHostDbContextFixture.KEY_COLUMN_NAME}\" = {{0}} FOR UPDATE",
                    heldId)
                .ToListAsync(TestContext.Current.CancellationToken);

            var ran = await PgRowLock.TryExecuteLockedAsync<LockProbeFixture>(
                db,
                p => p.Id,
                lockedId,
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
        note.Should().Be("held elsewhere", "the modified row was refused and not written later");
    }

    [Fact]
    public async Task TryExecuteLockedAsync_TheWorkSavedThenThrew_LeavesNothingTracked()
    {
        var id = await SeedAsync("kept");

        await using var db = LockHostDbContextFixture.Build(_connectionString);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await PgRowLock.TryExecuteLockedAsync<LockProbeFixture>(
                db,
                p => p.Id,
                id,
                async inner =>
                {
                    var row = await db.Probes.SingleAsync(p => p.Id == id, inner);
                    row.Note = "discarded";
                    db.Probes.Add(new LockProbeFixture { Id = Guid.CreateVersion7(), Note = "staged" });
                    await db.SaveChangesAsync(inner);
                    throw new InvalidOperationException("deliberate");
                },
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken));

        db.ChangeTracker.Entries().Should().BeEmpty(
            "the rollback undid the work's save, so the context tracks nothing it wrote");
    }

    [Fact]
    public async Task TryExecuteLockedAsync_TheLockStatementFailedBeforeTheWork_KeepsWhatWasTracked()
    {
        var id = await SeedAsync("kept");
        var failLock = new FailingStatementInterceptorFake("FOR UPDATE");
        var workRan = false;

        await using var db = new LockHostDbContextFixture(
            new DbContextOptionsBuilder<LockHostDbContextFixture>()
                .UseNpgsql(_connectionString)
                .AddInterceptors(failLock)
                .Options);
        db.Probes.Add(new LockProbeFixture { Id = Guid.CreateVersion7(), Note = "pending" });

        await Assert.ThrowsAnyAsync<DbException>(
            async () => await PgRowLock.TryExecuteLockedAsync<LockProbeFixture>(
                db,
                p => p.Id,
                id,
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

    [Fact]
    public async Task ExecuteLockedAsync_TheRowIsHeldPastTheCommandTimeout_ThrowsTheProviderTimeout()
    {
        var id = await SeedAsync("held");

        await using var holderDb = LockHostDbContextFixture.Build(_connectionString);
        await using var holderTx = await holderDb.Database.BeginTransactionAsync(
            TestContext.Current.CancellationToken);
        await holderDb.Database
            .SqlQueryRaw<int>(
                $"SELECT 1 AS \"Value\" FROM \"{LockHostDbContextFixture.TABLE_NAME}\" "
                + $"WHERE \"{LockHostDbContextFixture.KEY_COLUMN_NAME}\" = {{0}} FOR UPDATE",
                id)
            .ToListAsync(TestContext.Current.CancellationToken);

        await using var db = LockHostDbContextFixture.Build(_connectionString);
        db.Database.SetCommandTimeout(1);

        var thrown = await Record.ExceptionAsync(
            async () => await PgRowLock.ExecuteLockedAsync<LockProbeFixture>(
                db,
                p => p.Id,
                id,
                _ => Task.CompletedTask,
                TestContext.Current.CancellationToken));

        await holderTx.RollbackAsync(TestContext.Current.CancellationToken);

        // The helper opened the transaction here, so the execution strategy it
        // runs under wraps the provider's timeout, which it classes as transient.
        thrown.Should().NotBeNull(
            "the wait ends at the context's command timeout, not at the caller's token");
        thrown.Should().BeOfType<InvalidOperationException>();
        thrown.InnerException.Should().BeAssignableTo<DbException>()
            .Which.InnerException.Should().BeOfType<TimeoutException>();
    }

    private async Task<Guid> SeedAsync(string note)
    {
        var id = Guid.CreateVersion7();

        await using var db = LockHostDbContextFixture.Build(_connectionString);
        db.Probes.Add(new LockProbeFixture { Id = id, Note = note });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return id;
    }
}
