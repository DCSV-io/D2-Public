// -----------------------------------------------------------------------
// <copyright file="OutboxRelationalProbeDbContextFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using DcsvIo.D2.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Outbox.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// The same configuration on the real Npgsql provider, so the relational
/// metadata a consumer's migration is generated from - table name, column names,
/// the <c>jsonb</c> payload, the <c>xmin</c> concurrency token and the filtered
/// index - can be read off the built model.
/// </summary>
/// <remarks>
/// The connection string uses a host that nothing listens on, on purpose. Every
/// test on this context reads the model and none of them opens a connection, so
/// a context that could connect might open a connection a test did not intend.
/// </remarks>
internal sealed class OutboxRelationalProbeDbContextFixture : DbContext
{
    private const string _UNREACHABLE_CONNECTION =
        "Host=outbox-probe.invalid;Database=probe;Username=probe;Password=probe";

    /// <summary>Gets the staged messages.</summary>
    public DbSet<OutboxMessageRow> OutboxMessages => Set<OutboxMessageRow>();

    /// <inheritdoc/>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        optionsBuilder.UseNpgsql(
            _UNREACHABLE_CONNECTION, o => o.AddD2NodaTime());
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new OutboxMessageRowConfiguration());
    }
}
