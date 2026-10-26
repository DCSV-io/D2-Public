// -----------------------------------------------------------------------
// <copyright file="PgRowLockTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.SharedEntityFrameworkCore;

using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.EntityFrameworkCore;
using DcsvIo.D2.Tests.Integration.SharedEntityFrameworkCorePostgres.Fixtures;
using Xunit;

/// <summary>
/// The parts of <see cref="PgRowLock"/> that need no database: the
/// expression walk, the identifier resolution, the guards, and the
/// non-relational short-circuit.
/// </summary>
public sealed class PgRowLockTests
{
    [Fact]
    public void PropertyNameOf_ADirectMemberAccess_NamesTheProperty()
    {
        PgRowLock.PropertyNameOf<LockProbeFixture>(p => p.Id).Should().Be("Id");
    }

    [Fact]
    public void PropertyNameOf_AConvertedMemberAccess_NamesTheProperty()
    {
        var parameter = Expression.Parameter(typeof(LockProbeFixture), "p");
        var member = Expression.Property(parameter, nameof(LockProbeFixture.Id));
        var converted = Expression.Convert(member, typeof(Guid));
        var lambda = Expression.Lambda<Func<LockProbeFixture, Guid>>(converted, parameter);

        PgRowLock.PropertyNameOf(lambda).Should().Be("Id");
    }

    [Fact]
    public void PropertyNameOf_AComputedExpression_IsRefused()
    {
        var thrown = Assert.Throws<ArgumentException>(
            () => PgRowLock.PropertyNameOf<LockProbeFixture>(_ => Guid.Empty));

        thrown.ParamName.Should().Be("keyProperty");
    }

    [Fact]
    public void PropertyNameOf_AMemberOfAMember_IsRefused()
    {
        Assert.Throws<ArgumentException>(
            () => PgRowLock.PropertyNameOf<NestedProbeFixture>(p => p.Inner.Id));
    }

    [Fact]
    public void BuildLockSql_AMixedCaseTableAndColumn_DelimitsBoth()
    {
        using var db = LockHostDbContextFixture.BuildInMemoryRelationalModel();

        var sql = PgRowLock.BuildLockSql<LockProbeFixture>(
            db, p => p.Id, TimeSpan.FromSeconds(1));

        sql.Should().Be(
            $"SELECT 1 AS \"Value\" FROM \"{LockHostDbContextFixture.TABLE_NAME}\" "
            + $"WHERE \"{LockHostDbContextFixture.KEY_COLUMN_NAME}\" = {{0}} FOR UPDATE");
    }

    [Fact]
    public void BuildLockSql_NoBudget_AsksTheEngineToRefuseRatherThanQueue()
    {
        using var db = LockHostDbContextFixture.BuildInMemoryRelationalModel();

        var sql = PgRowLock.BuildLockSql<LockProbeFixture>(db, p => p.Id, TimeSpan.Zero);

        sql.Should().EndWith("FOR UPDATE NOWAIT");
    }

    /// <summary>
    /// The statement selects a value under the alias the query reads, because
    /// the helper has to know whether it matched a row: a <c>FOR UPDATE</c> that
    /// matched nothing locked nothing.
    /// </summary>
    [Fact]
    public void BuildLockSql_EveryBudget_SelectsAReadableValue()
    {
        using var db = LockHostDbContextFixture.BuildInMemoryRelationalModel();

        PgRowLock.BuildLockSql<LockProbeFixture>(db, p => p.Id, TimeSpan.FromSeconds(1))
            .Should().StartWith("SELECT 1 AS \"Value\" FROM ");
        PgRowLock.BuildLockSql<LockProbeFixture>(db, p => p.Id, TimeSpan.Zero)
            .Should().StartWith("SELECT 1 AS \"Value\" FROM ");
        PgRowLock.BuildLockSql<LockProbeFixture>(db, p => p.Id, waitBudget: null)
            .Should().StartWith("SELECT 1 AS \"Value\" FROM ");
    }

    [Fact]
    public void BuildLockSql_AnUnmappedEntity_IsRefused()
    {
        using var db = LockHostDbContextFixture.BuildInMemoryRelationalModel();

        var thrown = Assert.Throws<InvalidOperationException>(
            () => PgRowLock.BuildLockSql<NestedProbeFixture>(
                db, p => p.Id, TimeSpan.FromSeconds(1)));

        thrown.Message.Should().Contain(nameof(NestedProbeFixture));
    }

    [Fact]
    public void BuildLockSql_AnUnmappedProperty_IsRefused()
    {
        using var db = LockHostDbContextFixture.BuildInMemoryRelationalModel();
        var parameter = Expression.Parameter(typeof(LockProbeFixture), "p");
        var member = Expression.Property(parameter, nameof(LockProbeFixture.Unmapped));
        var lambda = Expression.Lambda<Func<LockProbeFixture, Guid>>(member, parameter);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => PgRowLock.BuildLockSql(db, lambda, TimeSpan.FromSeconds(1)));

        thrown.Message.Should().Contain(nameof(LockProbeFixture.Unmapped));
    }

    [Fact]
    public async Task TryExecuteLockedAsync_ANonRelationalProvider_RunsTheWorkAndReportsIt()
    {
        await using var db = LockHostDbContextFixture.BuildInMemory(Guid.NewGuid().ToString());
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
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        held.Should().BeTrue();
        ran.Should().BeTrue();
    }

    [Fact]
    public async Task TryExecuteLockedAsync_ANullContext_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await PgRowLock.TryExecuteLockedAsync<LockProbeFixture>(
                null!,
                p => p.Id,
                Guid.Empty,
                _ => Task.CompletedTask,
                TimeSpan.Zero,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryExecuteLockedAsync_ANullKeyExpression_Throws()
    {
        await using var db = LockHostDbContextFixture.BuildInMemory(Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await PgRowLock.TryExecuteLockedAsync<LockProbeFixture>(
                db,
                null!,
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
            async () => await PgRowLock.TryExecuteLockedAsync<LockProbeFixture>(
                db,
                p => p.Id,
                Guid.Empty,
                null!,
                TimeSpan.Zero,
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// An entity the lock model does not map, for the cases where the helper throws.
    /// </summary>
    internal sealed class NestedProbeFixture
    {
        /// <summary>Gets or sets the key.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets a nested probe, so a member-of-member expression exists.</summary>
        public LockProbeFixture Inner { get; set; } = new();
    }
}
