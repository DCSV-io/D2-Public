// -----------------------------------------------------------------------
// <copyright file="OutboxTelemetryTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox;

using AwesomeAssertions;
using DcsvIo.D2.Outbox;
using DcsvIo.D2.Outbox.EntityFrameworkCore;
using DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Result;
using DcsvIo.D2.Tests.Unit.Handler;
using DcsvIo.D2.Tests.Unit.Outbox.Fixtures;
using DcsvIo.D2.Time;
using NodaTime;
using Xunit;

/// <summary>
/// The relay's instruments, and the tag every one of them has. An operator's
/// alert reads the abandoned counter, so a counter that never increments means
/// an alert that never fires.
/// </summary>
/// <remarks>
/// Each test stages under its own kind name and reads the counters filtered on
/// that tag, so a parallel test class emitting on the same process-wide meter
/// cannot change the counts these assertions read.
/// </remarks>
public sealed class OutboxTelemetryTests
{
    private static readonly Instant sr_now = Instant.FromUtc(2026, 8, 17, 13, 0);

    [Fact]
    public void MeterName_IsThePinnedWireValue()
        => OutboxTelemetry.METER_NAME.Should().Be("DcsvIo.D2.Outbox");

    [Fact]
    public void TagKind_IsThePinnedWireValue()
        => OutboxTelemetry.TAG_KIND.Should().Be("kind");

    [Fact]
    public void TagOutcome_IsThePinnedWireValue()
        => OutboxTelemetry.TAG_OUTCOME.Should().Be("outcome");

    [Fact]
    public void OutcomeNoDispatcher_IsThePinnedWireValue()
        => OutboxTelemetry.Outcome.NO_DISPATCHER.Should().Be("no_dispatcher");

    [Fact]
    public void OutcomeDispatcherThrew_IsThePinnedWireValue()
        => OutboxTelemetry.Outcome.DISPATCHER_THREW.Should().Be("dispatcher_threw");

    [Fact]
    public void OutcomeDispatcherRefused_IsThePinnedWireValue()
        => OutboxTelemetry.Outcome.DISPATCHER_REFUSED.Should().Be("dispatcher_refused");

    [Fact]
    public void Meter_CarriesTheDeclaredName()
        => OutboxTelemetry.SR_Meter.Name.Should().Be(OutboxTelemetry.METER_NAME);

    [Fact]
    public void PublishedTotal_CarriesThePinnedInstrumentName()
        => OutboxTelemetry.SR_PublishedTotal.Name.Should().Be("d2.outbox.published");

    [Fact]
    public void DispatchFailuresTotal_CarriesThePinnedInstrumentName()
        => OutboxTelemetry.SR_DispatchFailuresTotal.Name.Should()
            .Be("d2.outbox.dispatch_failures");

    [Fact]
    public void AbandonedTotal_CarriesThePinnedInstrumentName()
        => OutboxTelemetry.SR_AbandonedTotal.Name.Should().Be("d2.outbox.abandoned");

    [Fact]
    public void PublishedTotal_CarriesTheDeclaredUnit()
        => OutboxTelemetry.SR_PublishedTotal.Unit.Should().Be("{message}");

    [Fact]
    public void DispatchFailuresTotal_CarriesTheDeclaredUnit()
        => OutboxTelemetry.SR_DispatchFailuresTotal.Unit.Should().Be("{failure}");

    [Fact]
    public void AbandonedTotal_CarriesTheDeclaredUnit()
        => OutboxTelemetry.SR_AbandonedTotal.Unit.Should().Be("{message}");

    [Fact]
    public void PrunedTotal_CarriesThePinnedInstrumentName()
        => OutboxTelemetry.SR_PrunedTotal.Name.Should().Be("d2.outbox.pruned");

    [Fact]
    public void PrunedTotal_CarriesTheDeclaredUnit()
        => OutboxTelemetry.SR_PrunedTotal.Unit.Should().Be("{row}");

    [Fact]
    public void DispatchDuration_CarriesThePinnedInstrumentName()
        => OutboxTelemetry.SR_DispatchDurationSeconds.Name.Should()
            .Be("d2.outbox.dispatch_duration");

    [Fact]
    public void DispatchDuration_CarriesTheDeclaredUnit()
        => OutboxTelemetry.SR_DispatchDurationSeconds.Unit.Should().Be("s");

    /// <summary>
    /// A dispatcher that slows down shows up on the counters only as a published
    /// rate that fell, with nothing to attribute it to. The tick is a fixed
    /// cadence rather than a queue, so the backlog grows silently until the
    /// abandonment alert fires an hour later.
    /// </summary>
    [Fact]
    public async Task AConfirmedDelivery_RecordsHowLongTheDispatcherTook()
    {
        const string kind = "TelemetryDurationSample";
        using var taken = new OutboxMeasurementCollectorFixture();

        await DrainOneAsync(kind, _ => D2Result.Ok(), retryLimit: 70);

        taken.RecordingsFor("d2.outbox.dispatch_duration", kind).Should().Be(1);
    }

    [Fact]
    public async Task ADispatcherThatThrew_StillRecordsHowLongItTook()
    {
        // The slow-then-failing dispatcher is the one an operator most needs
        // timed: a transport that hangs and then faults costs the whole tick.
        const string kind = "TelemetryDurationThrewSample";
        using var taken = new OutboxMeasurementCollectorFixture();

        await DrainOneAsync(
            kind,
            _ => throw new InvalidOperationException("the transport is down"),
            retryLimit: 70);

        taken.RecordingsFor("d2.outbox.dispatch_duration", kind).Should().Be(1);
    }

    [Fact]
    public async Task AStagedKindWithNoDispatcher_RecordsNoDuration()
    {
        // Nothing was called, so there is nothing to time - a zero-second
        // recording here would drag every latency percentile down.
        const string kind = "TelemetryDurationNoDispatcherSample";
        using var taken = new OutboxMeasurementCollectorFixture();

        await DrainOneAsync(
            kind, _ => D2Result.Ok(), retryLimit: 70, registerDispatcher: false);

        taken.RecordingsFor("d2.outbox.dispatch_duration", kind).Should().Be(0);
    }

    [Fact]
    public async Task AConfirmedDelivery_CountsOnThePublishedInstrument()
    {
        const string kind = "TelemetryPublishedSample";
        using var taken = new OutboxMeasurementCollectorFixture();

        await DrainOneAsync(kind, _ => D2Result.Ok(), retryLimit: 70);

        taken.CountFor("d2.outbox.published", kind).Should().Be(1);
    }

    [Fact]
    public async Task AFailedAttempt_CountsOnTheDispatchFailureInstrument()
    {
        const string kind = "TelemetryFailedSample";
        using var taken = new OutboxMeasurementCollectorFixture();

        await DrainOneAsync(kind, _ => D2Result.ServiceUnavailable(), retryLimit: 70);

        taken.CountFor("d2.outbox.dispatch_failures", kind).Should().Be(1);
    }

    [Fact]
    public async Task AFailedAttempt_DoesNotCountAsAbandoned()
    {
        // The admitting counterpart of the abandonment test below. Without it, an
        // instrument that counted every failure would satisfy that one too, and
        // the alert would fire on every blip.
        const string kind = "TelemetryNotAbandonedSample";
        using var taken = new OutboxMeasurementCollectorFixture();

        await DrainOneAsync(kind, _ => D2Result.ServiceUnavailable(), retryLimit: 70);

        taken.CountFor("d2.outbox.abandoned", kind).Should().Be(0);
    }

    [Fact]
    public async Task AMessageOutOfAttempts_CountsOnTheAbandonedInstrument()
    {
        const string kind = "TelemetryAbandonedSample";
        using var taken = new OutboxMeasurementCollectorFixture();

        await DrainOneAsync(kind, _ => D2Result.ServiceUnavailable(), retryLimit: 1);

        taken.CountFor("d2.outbox.abandoned", kind).Should().Be(1);
    }

    [Fact]
    public async Task AMessageOutOfAttempts_DoesNotAlsoCountAsAnOrdinaryFailure()
    {
        // Abandonment is its own event. Counting it on both instruments would
        // make the failure rate rise at the moment delivery stopped being tried.
        const string kind = "TelemetryAbandonOnlySample";
        using var taken = new OutboxMeasurementCollectorFixture();

        await DrainOneAsync(kind, _ => D2Result.ServiceUnavailable(), retryLimit: 1);

        taken.CountFor("d2.outbox.dispatch_failures", kind).Should().Be(0);
    }

    [Fact]
    public async Task ADispatcherThatRefused_NamesThatFailureClassOnTheCounter()
    {
        // A message reaches this path when its dispatcher refuses it, when its
        // dispatcher throws, or when no dispatcher is registered for its kind,
        // and these call for different operator responses. A counter that says only
        // "a delivery failed" cannot tell a deployment short a component from a
        // broker outage.
        const string kind = "TelemetryRefusedOutcomeSample";
        using var taken = new OutboxMeasurementCollectorFixture();

        await DrainOneAsync(kind, _ => D2Result.ServiceUnavailable(), retryLimit: 70);

        taken.CountFor(
                "d2.outbox.dispatch_failures", kind, OutboxTelemetry.Outcome.DISPATCHER_REFUSED)
            .Should().Be(1);
    }

    [Fact]
    public async Task ADispatcherThatRefused_IsNotCountedAsAnyOtherFailureClass()
    {
        const string kind = "TelemetryRefusedOnlySample";
        using var taken = new OutboxMeasurementCollectorFixture();

        await DrainOneAsync(kind, _ => D2Result.ServiceUnavailable(), retryLimit: 70);

        taken.CountFor(
                "d2.outbox.dispatch_failures", kind, OutboxTelemetry.Outcome.NO_DISPATCHER)
            .Should().Be(0);
        taken.CountFor(
                "d2.outbox.dispatch_failures", kind, OutboxTelemetry.Outcome.DISPATCHER_THREW)
            .Should().Be(0);
    }

    [Fact]
    public async Task ADispatcherThatThrew_NamesThatFailureClassOnTheCounter()
    {
        const string kind = "TelemetryThrewOutcomeSample";
        using var taken = new OutboxMeasurementCollectorFixture();

        await DrainOneAsync(
            kind,
            _ => throw new InvalidOperationException("the transport is down"),
            retryLimit: 70);

        taken.CountFor(
                "d2.outbox.dispatch_failures", kind, OutboxTelemetry.Outcome.DISPATCHER_THREW)
            .Should().Be(1);
    }

    [Fact]
    public async Task AStagedKindWithNoDispatcher_NamesThatFailureClassOnTheCounter()
    {
        // Nothing is registered for this kind, so the relay never reaches a
        // dispatcher at all - the case an alert most needs to distinguish,
        // because a rolling deploy fixes it and a retry does not.
        const string kind = "TelemetryNoDispatcherOutcomeSample";
        using var taken = new OutboxMeasurementCollectorFixture();

        await DrainOneAsync(
            kind, _ => D2Result.Ok(), retryLimit: 70, registerDispatcher: false);

        taken.CountFor(
                "d2.outbox.dispatch_failures", kind, OutboxTelemetry.Outcome.NO_DISPATCHER)
            .Should().Be(1);
    }

    [Fact]
    public async Task AnAbandonedMessage_CarriesTheFailureClassTheAlertNeeds()
    {
        // An operator's alert reads the abandonment counter, so that counter
        // most needs to state the reason.
        const string kind = "TelemetryAbandonedOutcomeSample";
        using var taken = new OutboxMeasurementCollectorFixture();

        await DrainOneAsync(
            kind, _ => D2Result.Ok(), retryLimit: 1, registerDispatcher: false);

        taken.CountFor("d2.outbox.abandoned", kind, OutboxTelemetry.Outcome.NO_DISPATCHER)
            .Should().Be(1);
    }

    private static async Task DrainOneAsync(
        string kind,
        Func<OutboxMessage, D2Result> answer,
        int retryLimit,
        bool registerDispatcher = true)
    {
        var options = new OutboxOptionsFor<IOutboxProbeDbFixture>
        {
            RelayAdvisoryLockKey = 1,
            RelayConnectionString =
                "Host=outbox-probe.invalid;Database=d2;Username=u;Password=p",
            RetryLimit = retryLimit,
        };

        var relay = new OutboxRelay<IOutboxProbeDbFixture>(
            new FakeSystemWorkScopeFactory(new EmptyServiceProviderStub()),
            registerDispatcher
                ? FakeOutboxDispatcherBinding<IOutboxProbeDbFixture>.Bind(
                    new FakeOutboxDispatcher(kind) { Answer = answer })
                : [],
            new TestClock(sr_now),
            new StaticOptionsMonitorFixture<OutboxOptionsFor<IOutboxProbeDbFixture>>(options),
            new TestLogger<OutboxRelay<IOutboxProbeDbFixture>>(),
            new FakeServiceProviderIsService(answer: true));

        var store = new FakeOutboxStore();
        store.Seed(new StagedOutboxMessage
        {
            Id = new OutboxMessageId(Guid.CreateVersion7()),
            Kind = kind,
            Payload = """{"detail":"a value"}""",
            StagedAt = sr_now,
            Attempts = 0,
        });

        _ = await relay.DrainAsync(
            store, relay.BuildIndex(), TestContext.Current.CancellationToken);
    }
}
