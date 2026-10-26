// -----------------------------------------------------------------------
// <copyright file="TwoInterfaceOutboxProbeDbContextFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using DcsvIo.D2.Outbox.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// One concrete context implementing two application-layer interfaces. Nothing
/// in the type system stops a consumer writing this, so it is the context the
/// registration's one-interface-per-context refusal is exercised over: both
/// interfaces satisfy the <c>TDbContext : DbContext, TDb</c> constraint, and
/// without the refusal both registrations would be accepted over the one table.
/// </summary>
/// <param name="options">The provider options the test supplies.</param>
internal sealed class TwoInterfaceOutboxProbeDbContextFixture(
    DbContextOptions<TwoInterfaceOutboxProbeDbContextFixture> options)
    : DbContext(options), IOutboxProbeDbFixture, ISecondOutboxProbeDbFixture
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
