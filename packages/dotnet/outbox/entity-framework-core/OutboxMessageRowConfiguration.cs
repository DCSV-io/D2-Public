// -----------------------------------------------------------------------
// <copyright file="OutboxMessageRowConfiguration.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF mapping for <see cref="OutboxMessageRow"/> to table
/// <c>outbox_messages</c>. A consumer applies this from its own
/// <c>OnModelCreating</c> and owns the migration that creates the table, because
/// the staged row lives in the consumer's database rather than in one this
/// package owns.
/// </summary>
/// <remarks>
/// The payload column is <c>jsonb</c> rather than text, so an operator looking
/// at an abandoned message can query into it with the same tooling as
/// every other document column here.
/// </remarks>
public sealed class OutboxMessageRowConfiguration
    : IEntityTypeConfiguration<OutboxMessageRow>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OutboxMessageRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id");

        builder.Property(m => m.Kind)
            .HasColumnName("kind")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(m => m.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(m => m.StagedAt)
            .HasColumnName("staged_at");

        builder.Property(m => m.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(m => m.Attempts)
            .HasColumnName("attempts");

        builder.Property(m => m.NextAttemptAt)
            .HasColumnName("next_attempt_at");

        builder.Property(m => m.PublishedAt)
            .HasColumnName("published_at");

        builder.Property(m => m.AbandonedAt)
            .HasColumnName("abandoned_at");

        builder.Property(m => m.LastFailureCode)
            .HasColumnName("last_failure_code")
            .HasMaxLength(128);

        builder.Property(m => m.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        // The relay only ever reads one status, so the index is filtered to it.
        // Without the filter the index grows with every message ever staged
        // while the backlog it exists to serve stays small. The filter uses the
        // persisted 'Staged' literal because the enum is stored by string name.
        builder.HasIndex(m => new { m.Status, m.NextAttemptAt })
            .HasDatabaseName("ix_outbox_messages_status_next_attempt_at")
            .HasFilter("status = 'Staged'");

        // The retention sweep reads published_at under the Published status and
        // abandoned_at under the Abandoned status. Each filter uses the
        // persisted literal because the enum is stored by string name.
        builder.HasIndex(m => m.PublishedAt)
            .HasDatabaseName("ix_outbox_messages_published_at")
            .HasFilter("status = 'Published'");

        builder.HasIndex(m => m.AbandonedAt)
            .HasDatabaseName("ix_outbox_messages_abandoned_at")
            .HasFilter("status = 'Abandoned'");
    }
}
