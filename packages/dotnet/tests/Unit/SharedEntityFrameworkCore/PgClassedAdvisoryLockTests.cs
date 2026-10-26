// -----------------------------------------------------------------------
// <copyright file="PgClassedAdvisoryLockTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.SharedEntityFrameworkCore;

using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.EntityFrameworkCore;
using DcsvIo.D2.Tests.Integration.SharedEntityFrameworkCorePostgres.Fixtures;
using DcsvIo.D2.Tests.Unit.HandlerRepo.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

/// <summary>
/// The parts of <see cref="PgClassedAdvisoryLock"/> that need no database:
/// the key fold, the guards, and the non-relational short-circuit.
/// </summary>
public sealed class PgClassedAdvisoryLockTests
{
    [Fact]
    public void DeriveKey_AnyIdentifier_IsNonNegative()
    {
        for (var i = 0; i < 200; i++)
            PgClassedAdvisoryLock.DeriveKey(Guid.NewGuid()).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void DeriveKey_TheSameIdentifierTwice_IsTheSameKey()
    {
        var id = Guid.Parse("0192f3a4-5b6c-7d8e-9f01-234567890abc");

        PgClassedAdvisoryLock.DeriveKey(id).Should().Be(PgClassedAdvisoryLock.DeriveKey(id));
    }

    [Fact]
    public void DeriveKey_AKnownIdentifier_FoldsToTheMaskedExclusiveOrOfItsWords()
    {
        var id = Guid.Parse("00000001-0000-0000-0000-000000000000");

        PgClassedAdvisoryLock.DeriveKey(id).Should().Be(
            1,
            "the fold is the exclusive-or of the four little-endian words with the "
            + "sign bit dropped");
    }

    [Fact]
    public void DeriveKey_AnIdentifierWhoseFoldSetsTheSignBit_HasTheSignBitDropped()
    {
        var id = Guid.Parse("ffffffff-0000-0000-0000-000000000000");

        PgClassedAdvisoryLock.DeriveKey(id).Should().Be(int.MaxValue);
    }

    [Fact]
    public void DeriveKey_TwoDifferentIdentifiers_UsuallyDiffer()
    {
        var a = Guid.Parse("0192f3a4-5b6c-7d8e-9f01-234567890abc");
        var b = Guid.Parse("0192f3a4-5b6c-7d8e-9f01-234567890abd");

        PgClassedAdvisoryLock.DeriveKey(a).Should().NotBe(PgClassedAdvisoryLock.DeriveKey(b));
    }

    [Fact]
    public async Task ExecuteLockedAsync_ANullContext_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await PgClassedAdvisoryLock.ExecuteLockedAsync(
                null!,
                1,
                Guid.Empty,
                _ => Task.CompletedTask,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteLockedAsync_ANullWork_Throws()
    {
        await using var db = LockHostDbContextFixture.BuildInMemory(Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await PgClassedAdvisoryLock.ExecuteLockedAsync(
                db, 1, Guid.Empty, null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteLockedAsync_ANonRelationalProvider_RunsTheWorkDirectly()
    {
        await using var db = LockHostDbContextFixture.BuildInMemory(Guid.NewGuid().ToString());
        var ran = false;

        await PgClassedAdvisoryLock.ExecuteLockedAsync(
            db,
            1,
            Guid.CreateVersion7(),
            _ =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        ran.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteLockedAsync_GenericOnANonRelationalProvider_AnswersTheWork()
    {
        await using var db = LockHostDbContextFixture.BuildInMemory(Guid.NewGuid().ToString());

        var answer = await PgClassedAdvisoryLock.ExecuteLockedAsync(
            db,
            1,
            Guid.CreateVersion7(),
            _ => ValueTask.FromResult("answered"),
            TestContext.Current.CancellationToken);

        answer.Should().Be("answered");
    }

    [Fact]
    public async Task ExecuteLockedAsyncGeneric_ANullContext_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await PgClassedAdvisoryLock.ExecuteLockedAsync(
                null!,
                1,
                Guid.Empty,
                _ => ValueTask.FromResult(0),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteLockedAsyncGeneric_ANullWork_Throws()
    {
        await using var db = LockHostDbContextFixture.BuildInMemory(Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await PgClassedAdvisoryLock.ExecuteLockedAsync<int>(
                db, 1, Guid.Empty, null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryExecuteLockedAsync_ANonRelationalProvider_RunsTheWorkAndReportsIt()
    {
        await using var db = LockHostDbContextFixture.BuildInMemory(Guid.NewGuid().ToString());
        var ran = false;

        var held = await PgClassedAdvisoryLock.TryExecuteLockedAsync(
            db,
            1,
            Guid.CreateVersion7(),
            _ =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        held.Should().BeTrue();
        ran.Should().BeTrue();
    }

    [Fact]
    public async Task TryExecuteLockedAsync_ANullContext_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await PgClassedAdvisoryLock.TryExecuteLockedAsync(
                null!,
                1,
                Guid.Empty,
                _ => Task.CompletedTask,
                TimeSpan.Zero,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryExecuteLockedAsync_ANullWork_Throws()
    {
        await using var db = LockHostDbContextFixture.BuildInMemory(Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await PgClassedAdvisoryLock.TryExecuteLockedAsync(
                db, 1, Guid.Empty, null!, TimeSpan.Zero, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void IsLockNotAvailable_TheRefusalTwoLevelsDown_IsTheRefusal()
    {
        var refused = PgExceptionFactory.Create(PgClassedAdvisoryLock.LOCK_NOT_AVAILABLE);
        var wrapped = new InvalidOperationException(
            "transient", new DbUpdateException("save", refused));

        PgClassedAdvisoryLock.IsLockNotAvailable(wrapped).Should().BeTrue();
    }

    [Fact]
    public void IsLockNotAvailable_TheBareRefusal_IsTheRefusal()
    {
        var refused = PgExceptionFactory.Create(PgClassedAdvisoryLock.LOCK_NOT_AVAILABLE);

        PgClassedAdvisoryLock.IsLockNotAvailable(refused).Should().BeTrue();
    }

    [Fact]
    public void IsLockNotAvailable_AnotherSqlStateInTheChain_IsNotTheRefusal()
    {
        // 40P01 is deadlock_detected: also a wait that ended, but not one the
        // wait budget bounded, so IsLockNotAvailable returns false for it.
        var deadlock = PgExceptionFactory.Create("40P01");
        var wrapped = new InvalidOperationException(
            "transient", new DbUpdateException("save", deadlock));

        PgClassedAdvisoryLock.IsLockNotAvailable(wrapped).Should().BeFalse();
    }

    [Fact]
    public void IsLockNotAvailable_NoProviderExceptionInTheChain_IsNotTheRefusal()
    {
        var unrelated = new InvalidOperationException(
            "unrelated", new ArgumentException("inner"));

        PgClassedAdvisoryLock.IsLockNotAvailable(unrelated).Should().BeFalse();
    }

    [Fact]
    public async Task RollbackQuietlyAsync_RollsBackWithATokenThatCannotBeCanceled()
    {
        // The helper runs inside catch blocks whose `throw;` must rethrow the
        // original exception. It takes no token, and the rollback it runs uses one
        // that cannot be canceled, so a canceled request cannot make the rollback
        // throw OperationCanceledException and replace that exception.
        var tx = new CancellationHonoringTransactionFixture();

        var act = async () => await PgClassedAdvisoryLock.RollbackQuietlyAsync(tx);

        await act.Should().NotThrowAsync();
        tx.RollbackCount.Should().Be(1);
        tx.LastRollbackToken.CanBeCanceled.Should().BeFalse();
    }

    [Fact]
    public async Task RollbackQuietlyAsync_AnAlreadyCompletedTransaction_DoesNotThrow()
    {
        var tx = new CancellationHonoringTransactionFixture
        {
            ThrowAlreadyComplete = true,
        };

        var act = async () => await PgClassedAdvisoryLock.RollbackQuietlyAsync(tx);

        await act.Should().NotThrowAsync();
        tx.RollbackCount.Should().Be(1);
    }

    /// <summary>
    /// Transaction double whose rollback throws when the token it receives is
    /// canceled, as a real provider's rollback does, and which records the token
    /// it received.
    /// </summary>
    private sealed class CancellationHonoringTransactionFixture : IDbContextTransaction
    {
        public Guid TransactionId { get; } = Guid.NewGuid();

        public int RollbackCount { get; private set; }

        public CancellationToken LastRollbackToken { get; private set; }

        public bool ThrowAlreadyComplete { get; init; }

        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCount++;
            LastRollbackToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();

            if (ThrowAlreadyComplete)
                throw new InvalidOperationException("The transaction has completed.");

            return Task.CompletedTask;
        }

        public void Commit()
        {
        }

        public void Rollback()
        {
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
