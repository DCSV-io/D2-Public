// -----------------------------------------------------------------------
// <copyright file="OutboxMessageRowConfigurationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox;

using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Outbox.EntityFrameworkCore;
using DcsvIo.D2.Tests.Unit.Outbox.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

/// <summary>
/// The mapping a consumer's own migration is generated from. Every assertion
/// here is a column or an index a migration will create, so a change to one is a
/// change to the schema of a consumer's database.
/// </summary>
public sealed class OutboxMessageRowConfigurationTests
{
    [Fact]
    public void Configure_WithNoBuilder_Throws()
    {
        var act = () => new OutboxMessageRowConfiguration().Configure(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TheRow_MapsToThePluralSnakeCaseTable()
        => Entity().GetTableName().Should().Be("outbox_messages");

    [Fact]
    public void TheRow_TakesItsIdentityAsThePrimaryKey()
        => Entity().FindPrimaryKey()!.Properties.Select(p => p.Name).Should().Equal("Id");

    [Theory]
    [InlineData(nameof(OutboxMessageRow.Id), "id")]
    [InlineData(nameof(OutboxMessageRow.Kind), "kind")]
    [InlineData(nameof(OutboxMessageRow.Payload), "payload")]
    [InlineData(nameof(OutboxMessageRow.StagedAt), "staged_at")]
    [InlineData(nameof(OutboxMessageRow.Status), "status")]
    [InlineData(nameof(OutboxMessageRow.Attempts), "attempts")]
    [InlineData(nameof(OutboxMessageRow.NextAttemptAt), "next_attempt_at")]
    [InlineData(nameof(OutboxMessageRow.PublishedAt), "published_at")]
    [InlineData(nameof(OutboxMessageRow.AbandonedAt), "abandoned_at")]
    [InlineData(nameof(OutboxMessageRow.LastFailureCode), "last_failure_code")]
    [InlineData(nameof(OutboxMessageRow.Version), "xmin")]
    public void EveryColumn_TakesItsSnakeCaseName(string property, string column)
        => Property(property).GetColumnName().Should().Be(column);

    [Fact]
    public void ThePayload_IsStoredAsJsonb()
    {
        // Text would work. jsonb lets an operator query into an abandoned
        // message's payload with the same tooling that every other document
        // column in this table supports.
        Property(nameof(OutboxMessageRow.Payload)).GetColumnType().Should().Be("jsonb");
    }

    [Fact]
    public void ThePayload_IsRequired()
        => Property(nameof(OutboxMessageRow.Payload)).IsNullable.Should().BeFalse();

    [Fact]
    public void TheKind_IsRequired()
        => Property(nameof(OutboxMessageRow.Kind)).IsNullable.Should().BeFalse();

    [Fact]
    public void TheKind_IsBoundedToTheLongestEnumMemberNameAConsumerCouldDeclare()
        => Property(nameof(OutboxMessageRow.Kind)).GetMaxLength().Should().Be(128);

    [Fact]
    public void TheFailureCode_IsBounded()
        => Property(nameof(OutboxMessageRow.LastFailureCode)).GetMaxLength().Should().Be(128);

    [Fact]
    public void TheFailureCode_IsOptional()
    {
        // The table stores Staged, Published and Abandoned rows, and
        // OutboxMessageRowMapper.ProjectOnto leaves the code null on a Published
        // row and on a Staged row that has not failed yet. An Abandoned row and a
        // Staged row re-staged after a failed attempt set it.
        Property(nameof(OutboxMessageRow.LastFailureCode)).IsNullable.Should().BeTrue();
    }

    [Theory]
    [InlineData(nameof(OutboxMessageRow.NextAttemptAt))]
    [InlineData(nameof(OutboxMessageRow.PublishedAt))]
    [InlineData(nameof(OutboxMessageRow.AbandonedAt))]
    public void EveryPerStateInstant_IsOptional(string property)
        => Property(property).IsNullable.Should().BeTrue();

    [Fact]
    public void TheStatus_IsStoredByName()
    {
        // The filtered index below uses the literal 'Staged', and a consumer
        // adding a member above an existing one must not renumber stored rows.
        Property(nameof(OutboxMessageRow.Status)).GetProviderClrType()
            .Should().Be<string>();
    }

    [Fact]
    public void TheStatus_IsRequired()
        => Property(nameof(OutboxMessageRow.Status)).IsNullable.Should().BeFalse();

    [Fact]
    public void TheConcurrencyToken_MapsToThePostgresTransactionIdType()
        => Property(nameof(OutboxMessageRow.Version)).GetColumnType().Should().Be("xid");

    [Fact]
    public void TheConcurrencyToken_IsAConcurrencyToken()
        => Property(nameof(OutboxMessageRow.Version)).IsConcurrencyToken.Should().BeTrue();

    [Fact]
    public void TheConcurrencyToken_IsGeneratedByTheDatabase()
        => Property(nameof(OutboxMessageRow.Version)).ValueGenerated
            .Should().Be(ValueGenerated.OnAddOrUpdate);

    [Fact]
    public void TheClaimIndex_CoversTheStatusAndTheNextAttempt()
        => Index().Properties.Select(p => p.Name).Should()
            .Equal(nameof(OutboxMessageRow.Status), nameof(OutboxMessageRow.NextAttemptAt));

    [Fact]
    public void TheClaimIndex_TakesTheProjectIndexNamingForm()
        => Index().GetDatabaseName().Should().Be("ix_outbox_messages_status_next_attempt_at");

    [Fact]
    public void TheClaimIndex_IsFilteredToTheOnlyStatusTheRelayReads()
    {
        // Without the filter the index grows with every message ever staged while
        // the backlog it exists to serve stays small.
        Index().GetFilter().Should().Be("status = 'Staged'");
    }

    [Fact]
    public void TheClaimIndex_IsNotUnique()
    {
        // Many messages sit staged for the same instant, so a unique index here
        // would refuse the second one.
        Index().IsUnique.Should().BeFalse();
    }

    [Fact]
    public void TheTable_CarriesTheClaimIndexAndTheTwoRetentionIndexes()
        => Entity().GetIndexes().Select(i => i.GetDatabaseName()).Should()
            .BeEquivalentTo(
                "ix_outbox_messages_status_next_attempt_at",
                "ix_outbox_messages_published_at",
                "ix_outbox_messages_abandoned_at");

    [Fact]
    public void ThePublishedRetentionIndex_CoversThePublishedInstant()
        => IndexNamed("ix_outbox_messages_published_at").Properties.Select(p => p.Name)
            .Should().Equal(nameof(OutboxMessageRow.PublishedAt));

    [Fact]
    public void ThePublishedRetentionIndex_IsFilteredToThePublishedStatus()
    {
        // The retention sweep reads published_at only under that status, and the
        // filter uses the persisted literal because the enum is stored by name.
        IndexNamed("ix_outbox_messages_published_at").GetFilter()
            .Should().Be("status = 'Published'");
    }

    [Fact]
    public void ThePublishedRetentionIndex_IsNotUnique()
    {
        // Many messages are published at the same instant.
        IndexNamed("ix_outbox_messages_published_at").IsUnique.Should().BeFalse();
    }

    [Fact]
    public void TheAbandonedRetentionIndex_CoversTheAbandonedInstant()
        => IndexNamed("ix_outbox_messages_abandoned_at").Properties.Select(p => p.Name)
            .Should().Equal(nameof(OutboxMessageRow.AbandonedAt));

    [Fact]
    public void TheAbandonedRetentionIndex_IsFilteredToTheAbandonedStatus()
    {
        // The retention sweep reads abandoned_at only under that status, and the
        // filter uses the persisted literal because the enum is stored by name.
        IndexNamed("ix_outbox_messages_abandoned_at").GetFilter()
            .Should().Be("status = 'Abandoned'");
    }

    [Fact]
    public void TheAbandonedRetentionIndex_IsNotUnique()
    {
        // Many messages are abandoned at the same instant.
        IndexNamed("ix_outbox_messages_abandoned_at").IsUnique.Should().BeFalse();
    }

    private static IEntityType Entity()
    {
        using var db = new OutboxRelationalProbeDbContextFixture();

        return db.Model.FindEntityType(typeof(OutboxMessageRow))!;
    }

    private static IProperty Property(string name) => Entity().FindProperty(name)!;

    private static IIndex IndexNamed(string databaseName)
        => Entity().GetIndexes().Single(i => i.GetDatabaseName() == databaseName);

    private static IIndex Index()
        => IndexNamed("ix_outbox_messages_status_next_attempt_at");
}
