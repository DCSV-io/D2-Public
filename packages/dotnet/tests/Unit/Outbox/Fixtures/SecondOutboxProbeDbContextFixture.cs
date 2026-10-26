// -----------------------------------------------------------------------
// <copyright file="SecondOutboxProbeDbContextFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using DcsvIo.D2.Outbox.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// A second consumer's context, so a host composing two modules over two
/// databases can be built and each module's seams resolved separately.
/// </summary>
/// <param name="options">The provider options the test supplies.</param>
internal sealed class SecondOutboxProbeDbContextFixture(
    DbContextOptions<SecondOutboxProbeDbContextFixture> options)
    : DbContext(options), ISecondOutboxProbeDbFixture
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
