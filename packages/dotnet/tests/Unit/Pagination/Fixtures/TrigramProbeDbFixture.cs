// -----------------------------------------------------------------------
// <copyright file="TrigramProbeDbFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Pagination.Fixtures;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// A model over <see cref="TrigramProbeRowFixture"/> on the real Npgsql provider, for
/// tests that need the provider's own translation of an expression the dialect
/// built.
/// </summary>
/// <remarks>
/// The connection string uses a host that nothing listens on, on purpose. Every
/// test on this context renders SQL and none of them executes it, so the
/// context must not be able to connect.
/// </remarks>
internal sealed class TrigramProbeDbFixture : DbContext
{
    private const string _UNREACHABLE_CONNECTION =
        "Host=trigram-probe.invalid;Database=probe;Username=probe;Password=probe";

    /// <summary>Gets the probe rows.</summary>
    public DbSet<TrigramProbeRowFixture> Rows => Set<TrigramProbeRowFixture>();

    /// <inheritdoc/>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseNpgsql(_UNREACHABLE_CONNECTION);

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<TrigramProbeRowFixture>();
        entity.ToTable("trigram_probe");
        entity.HasKey(r => r.Id);
        entity.Property(r => r.Id).HasColumnName("id");
        entity.Property(r => r.Email).HasColumnName("email");
        entity.Property(r => r.Username).HasColumnName("username");
        entity.Property(r => r.SearchBlob).HasColumnName("search_blob");
    }
}
