// -----------------------------------------------------------------------
// <copyright file="TrigramMemberDbContextFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Pagination.Fixtures;

using DcsvIo.D2.Pagination.EntityFrameworkCore;
using DcsvIo.D2.Pagination.EntityFrameworkCore.Postgres;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// A live-database model that declares its search blob through
/// <c>HasD2SearchBlob</c> and nothing else, so the schema contains only what that
/// registration produced.
/// </summary>
internal sealed class TrigramMemberDbContextFixture : DbContext
{
    /// <summary>The table the entity maps to.</summary>
    public const string TABLE = "trigram_member";

    /// <summary>The generated blob column.</summary>
    public const string BLOB_COLUMN = "search_blob";

    private TrigramMemberDbContextFixture(DbContextOptions<TrigramMemberDbContextFixture> options)
        : base(options)
    {
    }

    /// <summary>Gets the search index this model declares.</summary>
    public static SearchIndexDescriptor SearchIndex { get; } =
        new(
            nameof(TrigramMemberEntityFixture.SearchBlob),
            [
                nameof(TrigramMemberEntityFixture.DisplayName),
                nameof(TrigramMemberEntityFixture.Email),
            ],
            [2d, 1d]);

    /// <summary>Gets the searchable rows.</summary>
    public DbSet<TrigramMemberEntityFixture> Members => Set<TrigramMemberEntityFixture>();

    /// <summary>
    /// Builds a context over the given connection.
    /// </summary>
    /// <param name="connectionString">Npgsql connection string.</param>
    /// <returns>The context.</returns>
    public static TrigramMemberDbContextFixture Build(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TrigramMemberDbContextFixture>()
            .UseNpgsql(connectionString)
            .Options;

        return new TrigramMemberDbContextFixture(options);
    }

    /// <summary>
    /// The page-row projection of the searchable rows, including the blob
    /// member.
    /// </summary>
    /// <returns>The projected query.</returns>
    public IQueryable<TrigramMemberPageRowFixture> PageRows() =>
        Members.AsNoTracking().Select(e => new TrigramMemberPageRowFixture
        {
            Id = e.Id,
            ReachId = e.ReachId,
            DisplayName = e.DisplayName,
            Email = e.Email ?? string.Empty,
            SearchBlob = e.SearchBlob,
        });

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pg_trgm");

        var entity = modelBuilder.Entity<TrigramMemberEntityFixture>();
        entity.ToTable(TABLE);
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.ReachId).HasColumnName("reach_id");
        entity.Property(e => e.DisplayName).HasColumnName("display_name");
        entity.Property(e => e.Email).HasColumnName("email");
        entity.Property(e => e.SearchBlob).HasColumnName(BLOB_COLUMN);
        entity.HasD2SearchBlob(SearchIndex);
    }
}
