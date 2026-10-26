// -----------------------------------------------------------------------
// <copyright file="OutboxRelayOutageTests.cs" company="DCSV">
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
/// What the retry defaults are for. A broker outage has to delay delivery
/// rather than end it, and a poisoned message still has to be abandoned at some
/// point. These tests pin the retry limit and backoff ceiling that satisfy both.
/// </summary>
/// <remarks>
/// Both walks run on virtual time. The relay reads every instant from the
/// injected clock, so a test advances that clock by the tick rather than
/// sleeping, and an hour of outage costs a few hundred loop iterations.
/// </remarks>
public sealed class OutboxRelayOutageTests
{
    private const string _KIND = "ThingHappened";

    private static readonly Instant sr_start = Instant.FromUtc(2026, 8, 17, 12, 0);

    /// <summary>
    /// The walk. It fails if the retry limit or the backoff ceiling is lowered
    /// far enough for an hour-long outage to start abandoning messages.
    /// </summary>
    [Fact]
    public async Task ABrokerOutageOfAnHour_ThenRecovery_PublishesRatherThanAbandons()
    {
        var options = Defaults();
        var clock = new TestClock(sr_start);
        var broker = new FakeOutboxDispatcher(_KIND)
        {
            Answer = _ => D2Result.ServiceUnavailable(),
        };
        var relay = Relay(clock, options, broker);
        var store = new FakeOutboxStore();
        var staged = Staged();
        store.Seed(staged);

        await TickUntilAsync(relay, store, clock, options, sr_start + Duration.FromHours(1));

        broker.Answer = _ => D2Result.Ok();

        await TickUntilAsync(
            relay,
            store,
            clock,
            options,
            sr_start + Duration.FromHours(1) + Duration.FromMinutes(5));

        store[staged.Id].Should().BeOfType<PublishedOutboxMessage>();
    }

    [Fact]
    public async Task ABrokerOutageOfAnHour_RetriesRatherThanGivingUpDuringIt()
    {
        // The other half of the same claim: the message must still be staged at
        // the end of the outage, not abandoned partway through it.
        var options = Defaults();
        var clock = new TestClock(sr_start);
        var broker = new FakeOutboxDispatcher(_KIND)
        {
            Answer = _ => D2Result.ServiceUnavailable(),
        };
        var relay = Relay(clock, options, broker);
        var store = new FakeOutboxStore();
        var staged = Staged();
        store.Seed(staged);

        await TickUntilAsync(relay, store, clock, options, sr_start + Duration.FromHours(1));

        store[staged.Id].Should().BeOfType<StagedOutboxMessage>();
    }

    /// <summary>
    /// The counterpart that shows the bound still exists. Without it the test above
    /// would be satisfied by a relay that never gives up at all, which would
    /// leave a poisoned message being dispatched forever.
    /// </summary>
    [Fact]
    public async Task ABrokerThatNeverRecovers_EventuallyAbandonsTheMessage()
    {
        var options = Defaults();
        var clock = new TestClock(sr_start);
        var relay = Relay(
            clock,
            options,
            new FakeOutboxDispatcher(_KIND) { Answer = _ => D2Result.ServiceUnavailable() });
        var store = new FakeOutboxStore();
        var staged = Staged();
        store.Seed(staged);

        await TickUntilAsync(relay, store, clock, options, sr_start + Duration.FromHours(4));

        store[staged.Id].Should().BeOfType<AbandonedOutboxMessage>();
    }

    [Fact]
    public async Task ABrokerThatNeverRecovers_KeepsThePayloadOnTheAbandonedMessage()
    {
        var options = Defaults();
        var clock = new TestClock(sr_start);
        var relay = Relay(
            clock,
            options,
            new FakeOutboxDispatcher(_KIND) { Answer = _ => D2Result.ServiceUnavailable() });
        var store = new FakeOutboxStore();
        var staged = Staged();
        store.Seed(staged);

        await TickUntilAsync(relay, store, clock, options, sr_start + Duration.FromHours(4));

        store[staged.Id].Payload.Should().Be(staged.Payload);
    }

    [Fact]
    public async Task ABrokerThatNeverRecovers_RecordsWhyOnTheAbandonedMessage()
    {
        var options = Defaults();
        var clock = new TestClock(sr_start);
        var relay = Relay(
            clock,
            options,
            new FakeOutboxDispatcher(_KIND) { Answer = _ => D2Result.ServiceUnavailable() });
        var store = new FakeOutboxStore();
        var staged = Staged();
        store.Seed(staged);

        await TickUntilAsync(relay, store, clock, options, sr_start + Duration.FromHours(4));

        ((AbandonedOutboxMessage)store[staged.Id]).LastFailureCode.Should()
            .Be("GEN_SERVICE_UNAVAILABLE");
    }

    [Fact]
    public async Task ABrokerThatNeverRecovers_AlertsOnceRatherThanEveryTick()
    {
        // An operator's alert reads the abandonment log line, and a line logged
        // again on every tick after the abandonment would fire that alert
        // repeatedly, so operators would stop reading it.
        var options = Defaults();
        var clock = new TestClock(sr_start);
        var relay = Relay(
            clock,
            options,
            new FakeOutboxDispatcher(_KIND) { Answer = _ => D2Result.ServiceUnavailable() },
            out var logger);
        var store = new FakeOutboxStore();
        store.Seed(Staged());

        await TickUntilAsync(relay, store, clock, options, sr_start + Duration.FromHours(4));

        logger.Entries.Count(e => e.EventId.Id == 6).Should().Be(1);
    }

    [Fact]
    public async Task ABrokerThatNeverRecovers_StopsDispatchingOnceAbandoned()
    {
        var options = Defaults();
        var clock = new TestClock(sr_start);
        var broker = new FakeOutboxDispatcher(_KIND)
        {
            Answer = _ => D2Result.ServiceUnavailable(),
        };
        var relay = Relay(clock, options, broker);
        var store = new FakeOutboxStore();
        store.Seed(Staged());

        await TickUntilAsync(relay, store, clock, options, sr_start + Duration.FromHours(3));
        var afterAbandon = broker.Calls;
        await TickUntilAsync(relay, store, clock, options, sr_start + Duration.FromHours(4));

        broker.Calls.Should().Be(afterAbandon);
    }

    [Fact]
    public async Task ABrokerThatNeverRecovers_AttemptsExactlyTheRetryLimit()
    {
        // The limit counts attempts. A relay that spent one more or one fewer
        // would still pass every test above.
        var options = Defaults();
        var clock = new TestClock(sr_start);
        var broker = new FakeOutboxDispatcher(_KIND)
        {
            Answer = _ => D2Result.ServiceUnavailable(),
        };
        var relay = Relay(clock, options, broker);
        var store = new FakeOutboxStore();
        store.Seed(Staged());

        await TickUntilAsync(relay, store, clock, options, sr_start + Duration.FromHours(4));

        broker.Calls.Should().Be(options.RetryLimit);
    }

    private static async Task TickUntilAsync(
        OutboxRelay<IOutboxProbeDbFixture> relay,
        FakeOutboxStore store,
        TestClock clock,
        OutboxOptions options,
        Instant until)
    {
        var index = relay.BuildIndex();
        var tick = Duration.FromTimeSpan(options.RelayTick);

        while (clock.GetCurrentInstant() < until)
        {
            _ = await relay.DrainAsync(store, index, TestContext.Current.CancellationToken);
            clock.Advance(tick);
        }
    }

    private static StagedOutboxMessage Staged() => new()
    {
        Id = new OutboxMessageId(Guid.CreateVersion7()),
        Kind = _KIND,
        Payload = """{"detail":"a value"}""",
        StagedAt = sr_start,
        Attempts = 0,
    };

    private static OutboxOptionsFor<IOutboxProbeDbFixture> Defaults() => new()
    {
        RelayAdvisoryLockKey = 1,
        RelayConnectionString = "Host=outbox-probe.invalid;Database=d2;Username=u;Password=p",
    };

    private static OutboxRelay<IOutboxProbeDbFixture> Relay(
        TestClock clock,
        OutboxOptionsFor<IOutboxProbeDbFixture> options,
        IOutboxDispatcher dispatcher)
        => Relay(clock, options, dispatcher, out _);

    private static OutboxRelay<IOutboxProbeDbFixture> Relay(
        TestClock clock,
        OutboxOptionsFor<IOutboxProbeDbFixture> options,
        IOutboxDispatcher dispatcher,
        out TestLogger<OutboxRelay<IOutboxProbeDbFixture>> logger)
    {
        logger = new TestLogger<OutboxRelay<IOutboxProbeDbFixture>>();

        return new OutboxRelay<IOutboxProbeDbFixture>(
            new FakeSystemWorkScopeFactory(new EmptyServiceProviderStub()),
            FakeOutboxDispatcherBinding<IOutboxProbeDbFixture>.Bind(dispatcher),
            clock,
            new StaticOptionsMonitorFixture<OutboxOptionsFor<IOutboxProbeDbFixture>>(options),
            logger,
            new FakeServiceProviderIsService(answer: true));
    }
}
