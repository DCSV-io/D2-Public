// -----------------------------------------------------------------------
// <copyright file="OutboxMessageStateTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox;

using AwesomeAssertions;
using DcsvIo.D2.Outbox;
using NodaTime;
using Xunit;

/// <summary>
/// The staged message's own transitions: which leaf each one produces, which
/// fields it keeps, and where the retry bound falls.
/// </summary>
public sealed class OutboxMessageStateTests
{
    private static readonly Instant sr_staged = Instant.FromUtc(2026, 8, 17, 9, 0);

    [Fact]
    public void OutboxMessageStatus_MembersCarryTheirPinnedWireValues()
    {
        // The status is stored by NAME, so an added member must not renumber an
        // existing one - and the names below are what a row already holds.
        ((int)OutboxMessageStatus.Staged).Should().Be(0);
        ((int)OutboxMessageStatus.Published).Should().Be(1);
        ((int)OutboxMessageStatus.Abandoned).Should().Be(2);
        Enum.GetNames<OutboxMessageStatus>().Should()
            .BeEquivalentTo(["Staged", "Published", "Abandoned"]);
    }

    [Fact]
    public void OutboxMessageId_New_MintsADistinctIdentity()
    {
        OutboxMessageId.New().Value.Should().NotBe(OutboxMessageId.New().Value);
    }

    [Fact]
    public void OutboxMessageId_New_IsVersionSeven()
    {
        // Version 7 orders by timestamp, so insertion order matches index
        // locality and the relay's claim reads the table in staged order.
        OutboxMessageId.New().Value.Version.Should().Be(7);
    }

    [Fact]
    public void OutboxMessageId_ToString_IsTheHyphenatedForm()
    {
        var value = Guid.CreateVersion7();

        new OutboxMessageId(value).ToString().Should().Be(value.ToString("D"));
    }

    [Fact]
    public void StagedOutboxMessage_Status_IsStaged()
        => Staged().Status.Should().Be(OutboxMessageStatus.Staged);

    [Fact]
    public void Publish_ProducesThePublishedLeaf()
        => Staged().Publish(sr_staged).Should().BeOfType<PublishedOutboxMessage>();

    [Fact]
    public void Publish_StampsTheConfirmInstant()
    {
        var at = sr_staged + Duration.FromMinutes(3);

        Staged().Publish(at).PublishedAt.Should().Be(at);
    }

    [Fact]
    public void Publish_CountsTheAttemptThatSucceeded()
    {
        // The attempt that delivered is an attempt. Leaving it uncounted would
        // make an operator reading the row believe delivery was free.
        Staged(attempts: 2).Publish(sr_staged).Attempts.Should().Be(3);
    }

    [Fact]
    public void Publish_CarriesThePayloadAndTheStagedInstantForward()
    {
        var published = Staged().Publish(sr_staged);

        published.Payload.Should().Be("{}");
        published.StagedAt.Should().Be(sr_staged);
        published.Kind.Should().Be("ThingHappened");
    }

    [Fact]
    public void PublishedOutboxMessage_Status_IsPublished()
        => Staged().Publish(sr_staged).Status.Should().Be(OutboxMessageStatus.Published);

    [Fact]
    public void Fail_BelowTheLimit_ProducesTheStagedLeaf()
    {
        var moved = Staged(attempts: 0)
            .Fail(sr_staged, "boom", limit: 3, Duration.FromSeconds(2));

        moved.Should().BeOfType<StagedOutboxMessage>();
    }

    [Fact]
    public void Fail_BelowTheLimit_MovesTheNextAttemptOutByTheBackoff()
    {
        var at = sr_staged + Duration.FromMinutes(1);
        var moved = (StagedOutboxMessage)Staged()
            .Fail(at, "boom", limit: 3, Duration.FromSeconds(8));

        moved.NextAttemptAt.Should().Be(at + Duration.FromSeconds(8));
    }

    [Fact]
    public void Fail_BelowTheLimit_AdvancesTheAttemptCount()
    {
        var moved = Staged(attempts: 1)
            .Fail(sr_staged, "boom", limit: 5, Duration.FromSeconds(2));

        moved.Attempts.Should().Be(2);
    }

    /// <summary>
    /// The bound. The limit counts ATTEMPTS, so a limit of three abandons on the
    /// third failure rather than the fourth, so the retry window contains one
    /// wait fewer than the limit.
    /// </summary>
    [Fact]
    public void Fail_AtTheLimit_ProducesTheAbandonedLeaf()
    {
        var moved = Staged(attempts: 2)
            .Fail(sr_staged, "boom", limit: 3, Duration.FromSeconds(2));

        moved.Should().BeOfType<AbandonedOutboxMessage>();
    }

    [Fact]
    public void Fail_AtTheLimit_KeepsThePayload()
    {
        // Abandonment is escalation rather than loss: an operator has to be able
        // to see what was not delivered, and re-stage it.
        Abandoned().Payload.Should().Be("{}");
    }

    [Fact]
    public void Fail_AtTheLimit_RecordsTheFailureCode()
        => Abandoned().LastFailureCode.Should().Be("LIB_OUTBOX_NO_DISPATCHER");

    [Fact]
    public void Fail_AtTheLimit_StampsWhenItWasAbandoned()
        => Abandoned().AbandonedAt.Should().Be(sr_staged + Duration.FromHours(1));

    [Fact]
    public void Fail_AtTheLimit_CountsTheAttemptThatExhaustedTheBudget()
        => Abandoned().Attempts.Should().Be(3);

    [Fact]
    public void Fail_AtTheLimit_CarriesTheAbandonedStatus()
        => Abandoned().Status.Should().Be(OutboxMessageStatus.Abandoned);

    [Fact]
    public void Fail_PastTheLimit_StillAbandonsRatherThanReStaging()
    {
        // A limit an operator lowered under a message already past it must not
        // re-stage it forever.
        var moved = Staged(attempts: 99)
            .Fail(sr_staged, "boom", limit: 3, Duration.FromSeconds(2));

        moved.Should().BeOfType<AbandonedOutboxMessage>();
    }

    [Fact]
    public void Fail_ALimitOfOne_AbandonsOnTheFirstFailure()
    {
        // The degenerate bound: one attempt allowed means no retry at all.
        var moved = Staged(attempts: 0)
            .Fail(sr_staged, "boom", limit: 1, Duration.FromSeconds(2));

        moved.Should().BeOfType<AbandonedOutboxMessage>();
    }

    [Fact]
    public void Fail_AZeroBackoff_LeavesTheMessageAttemptableAtTheSameInstant()
    {
        var moved = (StagedOutboxMessage)Staged()
            .Fail(sr_staged, "boom", limit: 5, Duration.Zero);

        moved.NextAttemptAt.Should().Be(sr_staged);
    }

    /// <summary>
    /// One message driven past its retry budget, so each case above pins one
    /// fact about the leaf that failure produces.
    /// </summary>
    /// <returns>The abandoned leaf.</returns>
    private static AbandonedOutboxMessage Abandoned()
        => (AbandonedOutboxMessage)Staged(attempts: 2).Fail(
            sr_staged + Duration.FromHours(1),
            "LIB_OUTBOX_NO_DISPATCHER",
            limit: 3,
            Duration.FromSeconds(2));

    private static StagedOutboxMessage Staged(int attempts = 0) => new()
    {
        Id = OutboxMessageId.New(),
        Kind = "ThingHappened",
        Payload = "{}",
        StagedAt = sr_staged,
        Attempts = attempts,
    };
}
