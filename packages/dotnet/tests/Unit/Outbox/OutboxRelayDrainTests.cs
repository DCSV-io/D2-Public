// -----------------------------------------------------------------------
// <copyright file="OutboxRelayDrainTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox;

using AwesomeAssertions;
using DcsvIo.D2.Outbox;
using DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Result;
using DcsvIo.D2.Tests.Unit.Handler;
using DcsvIo.D2.Tests.Unit.Outbox.Fixtures;
using DcsvIo.D2.Time;
using NodaTime;
using Xunit;

/// <summary>
/// One tick's work: claim a batch, hand each message to the dispatcher whose kind
/// matches, and record what happened.
/// </summary>
public sealed class OutboxRelayDrainTests
{
    private const string _KIND = "ThingHappened";

    private static readonly Instant sr_now = Instant.FromUtc(2026, 8, 17, 11, 0);

    /// <summary>
    /// Both markers are written to the persisted last-failure column an
    /// operator reads, and their shape is the contract: lower-case and dotted,
    /// so a relay marker is distinguishable at a glance from a generated error
    /// code, which is upper-case and underscored. Asserting them against the literal is what
    /// makes that a pin rather than a description - a test that compares the
    /// constant to itself passes whatever the constant becomes.
    /// </summary>
    [Fact]
    public void NoDispatcherMarker_IsThePinnedPersistedValue()
        => OutboxRelay<IOutboxProbeDbFixture>.NO_DISPATCHER_MARKER.Should()
            .Be("outbox.no-dispatcher");

    [Fact]
    public void DispatcherThrewMarker_IsThePinnedPersistedValue()
        => OutboxRelay<IOutboxProbeDbFixture>.DISPATCHER_THREW_MARKER.Should()
            .Be("outbox.dispatcher-threw");

    [Fact]
    public void BuildIndex_TwoDispatchersForDifferentKinds_IndexesBoth()
    {
        var relay = Relay(
            out _,
            new FakeOutboxDispatcher(_KIND),
            new FakeOutboxDispatcher("OtherThingHappened"));

        relay.BuildIndex().Keys.Should()
            .BeEquivalentTo([_KIND, "OtherThingHappened"]);
    }

    [Fact]
    public void BuildIndex_NoDispatchers_IsEmpty()
        => Relay(out _).BuildIndex().Should().BeEmpty();

    /// <summary>
    /// A duplicate kind refuses to build rather than picking a winner: a
    /// group-and-take-first would deliver that kind to a dispatcher the consumer
    /// did not choose, quietly and forever.
    /// </summary>
    [Fact]
    public void BuildIndex_TwoDispatchersClaimingOneKind_Throws()
    {
        var relay = Relay(
            out _, new FakeOutboxDispatcher(_KIND), new FakeOutboxDispatcher(_KIND));

        var act = () => relay.BuildIndex();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BuildIndex_KindsDifferingOnlyByCase_AreDistinct()
    {
        // The index compares ordinally because the stored value is an enum member
        // name, and two members differing only in case are two members.
        var relay = Relay(
            out _, new FakeOutboxDispatcher(_KIND), new FakeOutboxDispatcher("thinghappened"));

        relay.BuildIndex().Should().HaveCount(2);
    }

    [Fact]
    public async Task DrainAsync_AnEmptyBacklog_PublishesNothing()
    {
        var relay = Relay(out _);
        var store = new FakeOutboxStore();

        var published = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        published.Should().Be(0);
    }

    [Fact]
    public async Task DrainAsync_AsksTheStoreForTheConfiguredBatchSize()
    {
        var relay = Relay(out _, options: o => o.BatchSize = 7);
        var store = new FakeOutboxStore();

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        store.ClaimedBatchSizes.Should().Equal(7);
    }

    [Fact]
    public async Task DrainAsync_AsksTheStoreForWhatIsDueAtTheClockInstant()
    {
        var relay = Relay(out _);
        var store = new FakeOutboxStore();

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        store.ClaimedAt.Should().Equal(sr_now);
    }

    [Fact]
    public async Task DrainAsync_AClaimTheStoreRefused_PublishesNothing()
    {
        var dispatcher = new FakeOutboxDispatcher(_KIND);
        var relay = Relay(out _, dispatcher);
        var store = new FakeOutboxStore
        {
            ClaimAnswer = D2Result<IReadOnlyList<StagedOutboxMessage>>.ServiceUnavailable(),
        };

        var published = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        published.Should().Be(0);
    }

    [Fact]
    public async Task DrainAsync_AClaimTheStoreRefused_DispatchesNothing()
    {
        // The batch is unknown rather than empty, so nothing may be sent.
        var dispatcher = new FakeOutboxDispatcher(_KIND);
        var relay = Relay(out _, dispatcher);
        var store = new FakeOutboxStore
        {
            ClaimAnswer = D2Result<IReadOnlyList<StagedOutboxMessage>>.ServiceUnavailable(),
        };

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        dispatcher.Calls.Should().Be(0);
    }

    [Fact]
    public async Task DrainAsync_AClaimTheStoreRefused_LogsTheFailure()
    {
        var relay = Relay(out var logger);
        var store = new FakeOutboxStore
        {
            ClaimAnswer = D2Result<IReadOnlyList<StagedOutboxMessage>>.ServiceUnavailable(),
        };

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        logger.Entries.Select(e => e.EventId.Id).Should().Contain(4);
    }

    [Fact]
    public async Task DrainAsync_ADispatcherThatConfirms_CountsThePublish()
    {
        var relay = Relay(out _, new FakeOutboxDispatcher(_KIND));
        var store = Seeded(out _);

        var published = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        published.Should().Be(1);
    }

    [Fact]
    public async Task DrainAsync_ADispatcherThatConfirms_RecordsThePublishedState()
    {
        var relay = Relay(out _, new FakeOutboxDispatcher(_KIND));
        var store = Seeded(out var staged);

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        store[staged.Id].Should().BeOfType<PublishedOutboxMessage>();
    }

    [Fact]
    public async Task DrainAsync_ADispatcherThatConfirms_StampsTheClockInstant()
    {
        var relay = Relay(out _, new FakeOutboxDispatcher(_KIND));
        var store = Seeded(out var staged);

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        ((PublishedOutboxMessage)store[staged.Id]).PublishedAt.Should().Be(sr_now);
    }

    [Fact]
    public async Task DrainAsync_HandsTheDispatcherTheWholeMessage()
    {
        // The dispatcher is the only party that knows the payload's type, so it
        // has to receive the payload, not a summary of it.
        var dispatcher = new FakeOutboxDispatcher(_KIND);
        var relay = Relay(out _, dispatcher);
        var store = Seeded(out var staged);

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        dispatcher.Received.Should().ContainSingle().Which.Should().Be(staged);
    }

    [Fact]
    public async Task DrainAsync_PassesItsCancellationTokenToTheDispatcher()
    {
        var dispatcher = new FakeOutboxDispatcher(_KIND);
        var relay = Relay(out _, dispatcher);
        var store = Seeded(out _);
        using var cts = new CancellationTokenSource();

        _ = await relay.DrainAsync(store, relay.BuildIndex(), cts.Token);

        dispatcher.LastToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task DrainAsync_AKindWithNoDispatcher_LeavesTheMessageStaged()
    {
        // A deployment short a component, not a bad message. It consumes the
        // retry window so a rolling deploy that adds the dispatcher delivers it.
        var relay = Relay(out _);
        var store = Seeded(out var staged);

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        store[staged.Id].Should().BeOfType<StagedOutboxMessage>();
    }

    [Fact]
    public async Task DrainAsync_AKindWithNoDispatcher_MovesTheNextAttemptOut()
    {
        var relay = Relay(out _);
        var store = Seeded(out var staged);

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        ((StagedOutboxMessage)store[staged.Id]).NextAttemptAt.Should()
            .Be(sr_now + Duration.FromSeconds(2));
    }

    [Fact]
    public async Task DrainAsync_ADispatcherThatRefuses_LeavesTheMessageStaged()
    {
        var relay = Relay(
            out _,
            new FakeOutboxDispatcher(_KIND) { Answer = _ => D2Result.ServiceUnavailable() });
        var store = Seeded(out var staged);

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        store[staged.Id].Should().BeOfType<StagedOutboxMessage>();
    }

    [Fact]
    public async Task DrainAsync_ADispatcherThatRefuses_CountsTheAttempt()
    {
        var relay = Relay(
            out _,
            new FakeOutboxDispatcher(_KIND) { Answer = _ => D2Result.ServiceUnavailable() });
        var store = Seeded(out var staged);

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        store[staged.Id].Attempts.Should().Be(1);
    }

    [Fact]
    public async Task DrainAsync_ADispatcherThatThrew_LeavesTheMessageStaged()
    {
        // A transport fault that arrives as a thrown exception rather than a
        // failed result. It must not stop the tick.
        var relay = Relay(
            out _,
            new FakeOutboxDispatcher(_KIND) { Throws = new InvalidOperationException("x") });
        var store = Seeded(out var staged);

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        store[staged.Id].Should().BeOfType<StagedOutboxMessage>();
    }

    [Fact]
    public async Task DrainAsync_ADispatcherThatThrew_StillDeliversTheRestOfTheBatch()
    {
        var thrower = new FakeOutboxDispatcher(_KIND)
        {
            Throws = new InvalidOperationException("x"),
        };
        var healthy = new FakeOutboxDispatcher("OtherThingHappened");
        var relay = Relay(out _, thrower, healthy);
        var store = new FakeOutboxStore();
        var first = Staged(_KIND, sr_now - Duration.FromMinutes(2));
        var second = Staged("OtherThingHappened", sr_now - Duration.FromMinutes(1));
        store.Seed(first);
        store.Seed(second);

        var published = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        published.Should().Be(1);
    }

    [Fact]
    public async Task DrainAsync_ADispatcherThatThrew_LogsTheFault()
    {
        var relay = Relay(
            out var logger,
            new FakeOutboxDispatcher(_KIND) { Throws = new InvalidOperationException("x") });
        var store = Seeded(out _);

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        logger.Entries.Select(e => e.EventId.Id).Should().Contain(3);
    }

    [Fact]
    public async Task DrainAsync_ADispatcherThatCanceled_PropagatesTheCancellation()
    {
        // A canceled dispatch is the host shutting down, not a delivery failure,
        // and re-staging it would count an attempt nothing actually made.
        var relay = Relay(
            out _,
            new FakeOutboxDispatcher(_KIND)
            {
                Throws = new OperationCanceledException(),
            });
        var store = Seeded(out _);

        var act = async () => await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DrainAsync_AMessageAtTheRetryLimit_AbandonsIt()
    {
        var relay = Relay(out _, options: o => o.RetryLimit = 3);
        var store = new FakeOutboxStore();
        var staged = Staged(_KIND, sr_now) with { Attempts = 2 };
        store.Seed(staged);

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        store[staged.Id].Should().BeOfType<AbandonedOutboxMessage>();
    }

    [Fact]
    public async Task DrainAsync_AMessageAtTheRetryLimit_RecordsWhatWentWrong()
    {
        var relay = Relay(out _, options: o => o.RetryLimit = 3);
        var store = new FakeOutboxStore();
        var staged = Staged(_KIND, sr_now) with { Attempts = 2 };
        store.Seed(staged);

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        ((AbandonedOutboxMessage)store[staged.Id]).LastFailureCode.Should()
            .Be(OutboxRelay<IOutboxProbeDbFixture>.NO_DISPATCHER_MARKER);
    }

    [Fact]
    public async Task DrainAsync_AMessageAtTheRetryLimit_LogsTheAbandonment()
    {
        var relay = Relay(out var logger, options: o => o.RetryLimit = 1);
        var store = Seeded(out _);

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        logger.Entries.Select(e => e.EventId.Id).Should().Contain(6);
    }

    [Fact]
    public async Task DrainAsync_AStateTheStoreCouldNotRecord_LogsIt()
    {
        // The message is dispatched again, which is the at-least-once contract -
        // but an operator has to be able to see why a duplicate arrived.
        var relay = Relay(out var logger, new FakeOutboxDispatcher(_KIND));
        var store = Seeded(out _);
        store.RecordAnswer = D2Result.ServiceUnavailable();

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        logger.Entries.Select(e => e.EventId.Id).Should().Contain(7);
    }

    [Fact]
    public async Task DrainAsync_AStateTheStoreCouldNotRecord_StillCountsThePublish()
    {
        var relay = Relay(out _, new FakeOutboxDispatcher(_KIND));
        var store = Seeded(out _);
        store.RecordAnswer = D2Result.ServiceUnavailable();

        var published = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        published.Should().Be(1);
    }

    [Fact]
    public async Task DrainAsync_ManyDueMessages_RecordsAStateForEveryOne()
    {
        var relay = Relay(out _, new FakeOutboxDispatcher(_KIND));
        var store = new FakeOutboxStore();
        store.Seed(Staged(_KIND, sr_now - Duration.FromMinutes(3)));
        store.Seed(Staged(_KIND, sr_now - Duration.FromMinutes(2)));
        store.Seed(Staged(_KIND, sr_now - Duration.FromMinutes(1)));

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        store.Recorded.Should().HaveCount(3);
    }

    /// <summary>
    /// The claim is what bounds at-least-once. A message whose outcome could not
    /// be written keeps the attempt the claim counted, so the retry limit is
    /// still reachable and the message is not attemptable again on the next tick.
    /// Counting the attempt on the record instead left it eligible immediately
    /// with its count unchanged, and the relay republished it every tick forever.
    /// </summary>
    [Fact]
    public async Task DrainAsync_AStateTheStoreCouldNotRecord_StillAdvancedTheAttempt()
    {
        var relay = Relay(out _, new FakeOutboxDispatcher(_KIND));
        var store = Seeded(out var staged);
        store.RecordAnswer = D2Result.ServiceUnavailable();

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        store[staged.Id].Attempts.Should().Be(1);
    }

    [Fact]
    public async Task DrainAsync_AStateTheStoreCouldNotRecord_LeavesItNotYetDue()
    {
        var relay = Relay(out _, new FakeOutboxDispatcher(_KIND));
        var store = Seeded(out var staged);
        store.RecordAnswer = D2Result.ServiceUnavailable();

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        ((StagedOutboxMessage)store[staged.Id]).NextAttemptAt
            .Should().Be(sr_now + Duration.FromSeconds(60));
    }

    [Fact]
    public async Task DrainAsync_AStateTheStoreCouldNotRecord_IsNotReclaimedOnTheNextTick()
    {
        var relay = Relay(out _, new FakeOutboxDispatcher(_KIND));
        var store = Seeded(out _);
        store.RecordAnswer = D2Result.ServiceUnavailable();

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);
        store.Recorded.Clear();
        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        store.Recorded.Should().BeEmpty();
    }

    [Fact]
    public async Task DrainAsync_AnyTick_LeasesTheClaimForTheBackoffCeiling()
    {
        var relay = Relay(
            out _,
            o => o.RetryBackoffCeiling = TimeSpan.FromSeconds(30),
            new FakeOutboxDispatcher(_KIND));
        var store = Seeded(out _);

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        store.ClaimedLeases.Should().ContainSingle()
            .Which.Should().Be(Duration.FromSeconds(30));
    }

    /// <summary>
    /// A message that is still retrying records why on every attempt rather than
    /// only on the one that abandons it. The row an operator queries during an
    /// outage otherwise shows an attempt count and a next-attempt instant with
    /// nothing saying what went wrong.
    /// </summary>
    [Fact]
    public async Task DrainAsync_AFailedAttemptWithRetriesLeft_RecordsWhatWentWrong()
    {
        var relay = Relay(out _, options: o => o.RetryLimit = 5);
        var store = Seeded(out var staged);

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        ((StagedOutboxMessage)store[staged.Id]).LastFailureCode.Should()
            .Be(OutboxRelay<IOutboxProbeDbFixture>.NO_DISPATCHER_MARKER);
    }

    /// <summary>
    /// The tick deletes the settled rows past their windows, measured back from
    /// the same instant the claim used, and bounded by the tick's own batch size.
    /// </summary>
    [Fact]
    public async Task DrainAsync_AsksTheStoreToDeleteSettledRowsPastTheirWindows()
    {
        var relay = Relay(
            out _,
            options: o =>
            {
                o.BatchSize = 11;
                o.PublishedRetention = TimeSpan.FromDays(7);
                o.AbandonedRetention = TimeSpan.FromDays(90);
            });
        var store = new FakeOutboxStore();

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        store.Pruned.Should().Equal(
            (sr_now - Duration.FromDays(7), sr_now - Duration.FromDays(90), 11));
    }

    /// <summary>
    /// A sweep that could not run is logged and stops nothing else: the rows
    /// stay, and the tick still returns the count it delivered.
    /// </summary>
    [Fact]
    public async Task DrainAsync_TheRetentionSweepFailed_LogsItAndStillReportsTheDeliveries()
    {
        var relay = Relay(out var logger, new FakeOutboxDispatcher(_KIND));
        var store = new FakeOutboxStore { PruneAnswer = D2Result<int>.ServiceUnavailable() };
        store.Seed(Staged(_KIND, sr_now - Duration.FromMinutes(1)));

        var published = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);

        published.Should().Be(1);
        logger.Entries.Should().Contain(e => e.Message.Contains(
            "retention sweep", StringComparison.Ordinal));
    }

    private static FakeOutboxStore Seeded(out StagedOutboxMessage staged)
    {
        var store = new FakeOutboxStore();
        staged = Staged(_KIND, sr_now - Duration.FromMinutes(1));
        store.Seed(staged);

        return store;
    }

    private static StagedOutboxMessage Staged(string kind, Instant at) => new()
    {
        Id = new OutboxMessageId(Guid.CreateVersion7()),
        Kind = kind,
        Payload = """{"detail":"a value"}""",
        StagedAt = at,
        Attempts = 0,
    };

    private static OutboxRelay<IOutboxProbeDbFixture> Relay(
        out TestLogger<OutboxRelay<IOutboxProbeDbFixture>> logger,
        params IOutboxDispatcher[] dispatchers)
        => Relay(out logger, null, dispatchers);

    private static OutboxRelay<IOutboxProbeDbFixture> Relay(
        out TestLogger<OutboxRelay<IOutboxProbeDbFixture>> logger,
        Action<OutboxOptions>? options,
        params IOutboxDispatcher[] dispatchers)
    {
        logger = new TestLogger<OutboxRelay<IOutboxProbeDbFixture>>();

        var bound = new OutboxOptionsFor<IOutboxProbeDbFixture>
        {
            RelayAdvisoryLockKey = 1,
            RelayConnectionString =
                "Host=outbox-probe.invalid;Database=d2;Username=u;Password=p",
        };

        options?.Invoke(bound);

        return new OutboxRelay<IOutboxProbeDbFixture>(
            new FakeSystemWorkScopeFactory(new EmptyServiceProviderStub()),
            FakeOutboxDispatcherBinding<IOutboxProbeDbFixture>.Bind(dispatchers),
            new TestClock(sr_now),
            new StaticOptionsMonitorFixture<OutboxOptionsFor<IOutboxProbeDbFixture>>(bound),
            logger,
            new FakeServiceProviderIsService(answer: true));
    }
}
