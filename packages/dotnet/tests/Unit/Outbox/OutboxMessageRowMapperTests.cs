// -----------------------------------------------------------------------
// <copyright file="OutboxMessageRowMapperTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox;

using AwesomeAssertions;
using DcsvIo.D2.Outbox;
using DcsvIo.D2.Outbox.EntityFrameworkCore;
using DcsvIo.D2.Tests.Unit.Outbox.Fixtures;
using NodaTime;
using Xunit;

/// <summary>
/// The row is one table holding the union of every state's columns, so the
/// mapper drops a leftover from a previous state rather than reading it back
/// as part of the current one.
/// </summary>
public sealed class OutboxMessageRowMapperTests
{
    private static readonly Instant sr_staged = Instant.FromUtc(2026, 8, 17, 9, 0);
    private static readonly Guid sr_id = Guid.CreateVersion7();

    [Fact]
    public void ToDomain_AStagedRow_RehydratesTheStagedLeaf()
        => Row(OutboxMessageStatus.Staged).ToDomain().Should()
            .BeOfType<StagedOutboxMessage>();

    [Fact]
    public void ToDomain_AStagedRow_CarriesTheNextAttemptInstant()
    {
        var next = sr_staged + Duration.FromSeconds(30);
        var row = Row(OutboxMessageStatus.Staged);
        row.NextAttemptAt = next;

        ((StagedOutboxMessage)row.ToDomain()).NextAttemptAt.Should().Be(next);
    }

    [Fact]
    public void ToDomain_AStagedRowWithNoNextAttempt_RehydratesItAbsent()
    {
        // Absent means attemptable now, which is how a freshly-staged row reads.
        ((StagedOutboxMessage)Row(OutboxMessageStatus.Staged).ToDomain())
            .NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public void ToDomain_APublishedRow_RehydratesThePublishedLeaf()
    {
        var row = Row(OutboxMessageStatus.Published);
        row.PublishedAt = sr_staged + Duration.FromMinutes(1);

        row.ToDomain().Should().BeOfType<PublishedOutboxMessage>();
    }

    [Fact]
    public void ToDomain_APublishedRowMissingItsInstant_Throws()
    {
        // No writer here produces this row, so reading one is corruption rather
        // than a state - and a fabricated instant would be worse than a throw.
        var act = () => Row(OutboxMessageStatus.Published).ToDomain();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*PublishedAt*");
    }

    [Fact]
    public void ToDomain_AnAbandonedRow_RehydratesTheAbandonedLeaf()
    {
        var row = Row(OutboxMessageStatus.Abandoned);
        row.AbandonedAt = sr_staged + Duration.FromHours(1);
        row.LastFailureCode = "LIB_OUTBOX_NO_DISPATCHER";

        row.ToDomain().Should().BeOfType<AbandonedOutboxMessage>();
    }

    [Fact]
    public void ToDomain_AnAbandonedRowMissingItsInstant_Throws()
    {
        var row = Row(OutboxMessageStatus.Abandoned);
        row.LastFailureCode = "boom";

        var act = () => row.ToDomain();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AbandonedAt*");
    }

    [Fact]
    public void ToDomain_AnAbandonedRowMissingItsFailureCode_Throws()
    {
        var row = Row(OutboxMessageStatus.Abandoned);
        row.AbandonedAt = sr_staged;

        var act = () => row.ToDomain();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*LastFailureCode*");
    }

    [Fact]
    public void ToDomain_ARowCarryingAStatusOutsideTheSet_Throws()
    {
        // A status value that matches no member is what a hand-edited row or a
        // newer writer produces, and returning a state for it would be a guess.
        var row = Row((OutboxMessageStatus)99);

        var act = () => row.ToDomain();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*unhandled status*");
    }

    [Fact]
    public void ProjectOnto_ANullRow_Throws()
    {
        var act = () => Staged().ProjectOnto(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ProjectOnto_APublishedState_NullsTheNextAttemptColumn()
    {
        // The leftover matters: a published row still carrying a due next-attempt
        // would be re-claimed by the relay's own predicate if the status ever
        // widened, and the column reads as a live retry to an operator.
        var row = Row(OutboxMessageStatus.Staged);
        row.NextAttemptAt = sr_staged;

        Staged().Publish(sr_staged).ProjectOnto(row);

        row.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public void ProjectOnto_APublishedState_StampsThePublishedColumn()
    {
        var at = sr_staged + Duration.FromMinutes(2);
        var row = Row(OutboxMessageStatus.Staged);

        Staged().Publish(at).ProjectOnto(row);

        row.PublishedAt.Should().Be(at);
    }

    [Fact]
    public void ProjectOnto_APublishedState_SetsTheStatusColumn()
    {
        var row = Row(OutboxMessageStatus.Staged);

        Staged().Publish(sr_staged).ProjectOnto(row);

        row.Status.Should().Be(OutboxMessageStatus.Published);
    }

    [Fact]
    public void ProjectOnto_AnAbandonedState_NullsThePublishedColumn()
    {
        var row = Row(OutboxMessageStatus.Published);
        row.PublishedAt = sr_staged;

        Abandoned().ProjectOnto(row);

        row.PublishedAt.Should().BeNull();
    }

    [Fact]
    public void ProjectOnto_AnAbandonedState_RecordsTheFailureCode()
    {
        var row = Row(OutboxMessageStatus.Staged);

        Abandoned().ProjectOnto(row);

        row.LastFailureCode.Should().Be("LIB_OUTBOX_NO_DISPATCHER");
    }

    [Fact]
    public void ProjectOnto_AReStagedState_ClearsAFailureCodeAPreviousStateLeft()
    {
        var row = Row(OutboxMessageStatus.Abandoned);
        row.LastFailureCode = "stale";
        row.AbandonedAt = sr_staged;

        Staged().ProjectOnto(row);

        row.LastFailureCode.Should().BeNull();
    }

    [Fact]
    public void ProjectOnto_AReStagedState_ClearsAnAbandonInstantAPreviousStateLeft()
    {
        var row = Row(OutboxMessageStatus.Abandoned);
        row.AbandonedAt = sr_staged;

        Staged().ProjectOnto(row);

        row.AbandonedAt.Should().BeNull();
    }

    [Fact]
    public void ProjectOnto_AnUnhandledLeaf_Throws()
    {
        // A state added without a mapper arm must throw here rather than write
        // a row whose columns describe none of the states.
        var act = () => new UnmappedOutboxMessageStub
        {
            Id = new OutboxMessageId(sr_id),
            Kind = "ThingHappened",
            Payload = "{}",
            StagedAt = sr_staged,
            Attempts = 0,
        }.ProjectOnto(Row(OutboxMessageStatus.Staged));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*UnmappedOutboxMessageStub*");
    }

    [Fact]
    public void ToNewRow_ThenToDomain_RoundTripsAStagedMessage()
    {
        var staged = Staged() with { NextAttemptAt = sr_staged + Duration.FromSeconds(4) };

        staged.ToNewRow().ToDomain().Should().Be(staged);
    }

    [Fact]
    public void ToNewRow_ThenToDomain_RoundTripsAPublishedMessage()
    {
        var published = Staged().Publish(sr_staged + Duration.FromMinutes(1));

        published.ToNewRow().ToDomain().Should().Be(published);
    }

    [Fact]
    public void ToNewRow_ThenToDomain_RoundTripsAnAbandonedMessage()
    {
        var abandoned = Abandoned();

        abandoned.ToNewRow().ToDomain().Should().Be(abandoned);
    }

    [Fact]
    public void ToNewRow_LeavesTheConcurrencyTokenAtItsUnwrittenValue()
    {
        // The token is the database's to set. A value invented here would be
        // compared against the stored one on the first update and lose.
        Staged().ToNewRow().Version.Should().Be(0u);
    }

    private static StagedOutboxMessage Staged() => new()
    {
        Id = new OutboxMessageId(sr_id),
        Kind = "ThingHappened",
        Payload = """{"detail":"a value"}""",
        StagedAt = sr_staged,
        Attempts = 0,
    };

    private static AbandonedOutboxMessage Abandoned() => new()
    {
        Id = new OutboxMessageId(sr_id),
        Kind = "ThingHappened",
        Payload = """{"detail":"a value"}""",
        StagedAt = sr_staged,
        Attempts = 70,
        AbandonedAt = sr_staged + Duration.FromHours(1),
        LastFailureCode = "LIB_OUTBOX_NO_DISPATCHER",
    };

    private static OutboxMessageRow Row(OutboxMessageStatus status) => new()
    {
        Id = sr_id,
        Kind = "ThingHappened",
        Payload = """{"detail":"a value"}""",
        StagedAt = sr_staged,
        Status = status,
        Attempts = 0,
    };
}
