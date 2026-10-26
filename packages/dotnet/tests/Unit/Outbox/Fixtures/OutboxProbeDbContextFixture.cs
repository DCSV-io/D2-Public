// -----------------------------------------------------------------------
// <copyright file="OutboxProbeDbContextFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using DcsvIo.D2.Outbox.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// A consumer's context standing in for a real one, over the in-memory provider,
/// so the store's claim predicate, ordering and write path are exercised against
/// an actual change tracker rather than a hand-written double.
/// </summary>
/// <remarks>
/// The in-memory provider is non-relational, so the column names, the
/// <c>jsonb</c> type and the filtered index this configuration declares are
/// asserted on the relational model instead - see
/// <see cref="OutboxRelationalProbeDbContextFixture"/>.
/// </remarks>
/// <param name="options">The provider options the test supplies.</param>
internal sealed class OutboxProbeDbContextFixture(
    DbContextOptions<OutboxProbeDbContextFixture> options)
    : DbContext(options), IOutboxProbeDbFixture
{
    /// <summary>Gets the staged messages.</summary>
    public DbSet<OutboxMessageRow> OutboxMessages => Set<OutboxMessageRow>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new OutboxMessageRowConfiguration());
    }
}
