// -----------------------------------------------------------------------
// <copyright file="OutboxHostDbContextFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Outbox.Fixtures;

using DcsvIo.D2.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Outbox.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// A consumer's context holding only the outbox table, so the mapping this
/// package ships is exercised against a real PostgreSQL server: the
/// <c>jsonb</c> payload, the status stored by name, the <c>xmin</c> concurrency
/// token and the filtered index all mean nothing until a real provider builds
/// them.
/// </summary>
internal sealed class OutboxHostDbContextFixture(
    DbContextOptions<OutboxHostDbContextFixture> options)
    : DbContext(options), IOutboxHostDbFixture
{
    /// <summary>Gets the staged messages.</summary>
    public DbSet<OutboxMessageRow> OutboxMessages => Set<OutboxMessageRow>();

    /// <summary>Builds a context over one connection string.</summary>
    /// <param name="connectionString">The isolated database to open.</param>
    /// <returns>The context.</returns>
    internal static OutboxHostDbContextFixture Build(string connectionString)
        => new(new DbContextOptionsBuilder<OutboxHostDbContextFixture>()
            .UseNpgsql(connectionString, o => o.AddD2NodaTime())
            .Options);

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new OutboxMessageRowConfiguration());
    }
}
