// -----------------------------------------------------------------------
// <copyright file="LockHostDbContextFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.SharedEntityFrameworkCorePostgres.Fixtures;

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Synthetic host context for the row-lock and classed-advisory-lock live-DB
/// tests.
/// </summary>
/// <remarks>
/// The fixture's only entity maps to a mixed-case table and a mixed-case key
/// column on purpose: PostgreSQL folds an unquoted identifier to lower case, so a helper
/// that hand-quotes or forgets to quote fails against this schema and passes
/// against an all-lowercase one.
/// </remarks>
internal sealed class LockHostDbContextFixture : DbContext
{
    /// <summary>The table the probe entity is mapped to.</summary>
    internal const string TABLE_NAME = "LockProbeRows";

    /// <summary>The column the probe entity's key is mapped to.</summary>
    internal const string KEY_COLUMN_NAME = "ProbeId";

    internal LockHostDbContextFixture(DbContextOptions<LockHostDbContextFixture> options)
        : base(options)
    {
    }

    /// <summary>Gets the probe rows.</summary>
    internal DbSet<LockProbeFixture> Probes => Set<LockProbeFixture>();

    /// <summary>Builds a context over a live PostgreSQL connection string.</summary>
    /// <param name="connectionString">Npgsql connection string.</param>
    /// <returns>The context.</returns>
    internal static LockHostDbContextFixture Build(string connectionString)
    {
        var options = new DbContextOptionsBuilder<LockHostDbContextFixture>()
            .UseNpgsql(connectionString)
            .Options;

        return new LockHostDbContextFixture(options);
    }

    /// <summary>Builds a context over the in-memory provider.</summary>
    /// <param name="name">The in-memory database name.</param>
    /// <returns>The context.</returns>
    internal static LockHostDbContextFixture BuildInMemory(string name)
    {
        var options = new DbContextOptionsBuilder<LockHostDbContextFixture>()
            .UseInMemoryDatabase(name)
            .Options;

        return new LockHostDbContextFixture(options);
    }

    /// <summary>
    /// Builds a context whose model is the relational one but which opens no
    /// connection, so only its model metadata can be read.
    /// </summary>
    /// <returns>The context.</returns>
    internal static LockHostDbContextFixture BuildInMemoryRelationalModel()
    {
        var options = new DbContextOptionsBuilder<LockHostDbContextFixture>()
            .UseNpgsql("Host=127.0.0.1;Database=unused;Username=unused;Password=unused")
            .Options;

        return new LockHostDbContextFixture(options);
    }

    /// <summary>Creates the probe table if it is not already there.</summary>
    /// <param name="connectionString">Npgsql connection string.</param>
    /// <returns>A task that completes when the table exists.</returns>
    internal static async Task EnsureSchemaAsync(string connectionString)
    {
        await using var ctx = Build(connectionString);

        var script = ctx.Database
            .GenerateCreateScript()
            .Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ", StringComparison.Ordinal);

        await ctx.Database.ExecuteSqlRawAsync(script);
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LockProbeFixture>(b =>
        {
            b.ToTable(TABLE_NAME);
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName(KEY_COLUMN_NAME);
            b.Property(x => x.Note).HasColumnName("Note");
            b.Ignore(x => x.Unmapped);
        });
    }
}
