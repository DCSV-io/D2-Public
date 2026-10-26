// -----------------------------------------------------------------------
// <copyright file="PostgresFixtureConnectionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.DataGovernance;

using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Tests.Integration.Outbox.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

/// <summary>
/// <see cref="PostgresFixture.CreateIsolatedDatabaseAsync"/> and
/// <see cref="PostgresFixture.AssertNoConnectionOpenAsync"/> against the shared
/// container: a connection a test disposed is closed on the server, and a
/// connection a test left open is reported.
/// </summary>
[Collection("Postgres")]
[Trait("Category", "Integration")]
public sealed class PostgresFixtureConnectionTests
{
    private readonly PostgresFixture r_fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresFixtureConnectionTests"/>
    /// class.
    /// </summary>
    /// <param name="fixture">The shared Postgres Testcontainers fixture.</param>
    public PostgresFixtureConnectionTests(PostgresFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
    public async Task AssertNoConnectionOpenAsync_EveryConnectionDisposed_DoesNotThrow()
    {
        var connectionString = await r_fixture.CreateIsolatedDatabaseAsync(
            nameof(PostgresFixtureConnectionTests));

        // One connection opened by connection string, and one opened through the
        // data source the EF provider builds and keeps for a context with a
        // plugin, which no pool-clearing call reaches.
        await using (var plain = new NpgsqlConnection(connectionString))
            await plain.OpenAsync(TestContext.Current.CancellationToken);

        await using (var ctx = OutboxHostDbContextFixture.Build(connectionString))
            await ctx.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var check = () => r_fixture.AssertNoConnectionOpenAsync(connectionString);

        await check.Should().NotThrowAsync("a disposed connection to an isolated database is closed");
    }

    [Fact]
    public async Task AssertNoConnectionOpenAsync_AConnectionStillOpen_ThrowsUntilItIsDisposed()
    {
        var connectionString = await r_fixture.CreateIsolatedDatabaseAsync(
            nameof(PostgresFixtureConnectionTests));

        var held = new NpgsqlConnection(connectionString);
        await held.OpenAsync(TestContext.Current.CancellationToken);

        var check = () => r_fixture.AssertNoConnectionOpenAsync(connectionString);

        try
        {
            await check.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("1 connection(s) to the isolated database *");
        }
        finally
        {
            await held.DisposeAsync();
        }

        await check.Should().NotThrowAsync("the held connection is disposed now");
    }
}
