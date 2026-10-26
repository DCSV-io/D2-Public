// -----------------------------------------------------------------------
// <copyright file="DlqRepublisherTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Subscribing;

using AwesomeAssertions;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Subscribing;
using DcsvIo.D2.Tests.Unit.Messaging.Subscribing.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using Xunit;

/// <summary>Checks operation outcomes and retained lifetime without network allocation.</summary>
public sealed class DlqRepublisherTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public async Task Terminate_CleanupFailure_PreservesRetainedResourceOwnership(
        bool cancellationFails, bool disposalFails, bool loggerThrows)
    {
        var (connection, fixture) = DelayedDlqConnectionFixture.Create();
        fixture.FailCancellation = cancellationFails;
        fixture.FailDisposal = disposalFails;
        var logger = new DlqCleanupLoggerFixture(loggerThrows);
        await using var generation = new DlqTransportGeneration(
            1, "amqp://localhost", "cleanup-fixture", logger, fixture.Acquire);
        await using var operation = new DlqRepublishOperation(
            1,
            Request(),
            Duration.FromSeconds(5),
            new ManualTimeProviderFixture(),
            CancellationToken.None,
            g => g.Terminate());
        operation.Own(generation);
        generation.Start(operation, () => { }).Should().BeTrue();
        await fixture.Started;
        operation.Complete(DlqRepublishOutcome.Stopping);
        fixture.Complete(connection);
        var cleanup = generation.CleanupWork;
        cleanup.Should().NotBeNull();
        try
        {
            await cleanup;
            fixture.Disposals.Should().Be(1);
            fixture.ChannelAcquisitions.Should().Be(0);
            generation.ResourceCompletion.IsCompletedSuccessfully.Should().BeTrue();
        }
        finally
        {
            await generation.WorkCompletion;
            if (fixture.Disposals == 0)
            {
                fixture.FailDisposal = false;
                await connection.DisposeAsync();
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Terminate_CleanupError_OmitsExceptionMessage(bool cancellationFails)
    {
        var (connection, fixture) = DelayedDlqConnectionFixture.Create();
        fixture.FailCancellation = cancellationFails;
        fixture.FailDisposal = !cancellationFails;
        var logger = new DlqCleanupLoggerFixture(false);
        await using var generation = new DlqTransportGeneration(
            1, "amqp://localhost", "cleanup-fixture", logger, fixture.Acquire);
        await using var operation = new DlqRepublishOperation(
            1,
            Request(),
            Duration.FromSeconds(5),
            new ManualTimeProviderFixture(),
            CancellationToken.None,
            g => g.Terminate());
        operation.Own(generation);
        generation.Start(operation, () => { }).Should().BeTrue();
        await fixture.Started;
        operation.Complete(DlqRepublishOutcome.Stopping);
        fixture.Complete(connection);
        var cleanup = generation.CleanupWork;
        cleanup.Should().NotBeNull();
        await cleanup;
        logger.Messages.Should().ContainSingle();
        logger.Messages[0].Should().Contain(cancellationFails ? "AggregateException" : "InvalidOperationException");
        logger.Messages[0].Should().NotContain("fixture-private-");
    }

    [Fact]
    public async Task Republish_AdmissionAtCapacity_RejectsOnlyExcessOperation()
    {
        var (connection, fixture) = DelayedDlqConnectionFixture.Create();
        var allocations = 0;
        DlqTransportGeneration? retained = null;
        using var cancellation = new CancellationTokenSource();
        await using var republisher = new DlqRepublisher(
            new RabbitMqConnectionOptions(),
            new RabbitMqSubscriberOptions(),
            2,
            NullLogger.Instance,
            generationFactory: id =>
            {
                Interlocked.Increment(ref allocations);
                return retained = new DlqTransportGeneration(
                    id, "amqp://localhost", "admission-fixture", NullLogger.Instance, fixture.Acquire);
            });
        var first = republisher.RepublishAsync(Request(), cancellation.Token).AsTask();
        Task<DlqRepublishOutcome>? second = null;
        try
        {
            await fixture.Started;
            second = republisher.RepublishAsync(Request(), cancellation.Token).AsTask();
            second.IsCompleted.Should().BeFalse();
            (await republisher.RepublishAsync(Request(), CancellationToken.None))
                .Should().Be(DlqRepublishOutcome.TransportFailure);
            allocations.Should().Be(1);
            retained.Should().NotBeNull();
            retained.IsRetired.Should().BeFalse();
        }
        finally
        {
            await cancellation.CancelAsync();
            fixture.Complete(connection);
            await first;
            if (second is not null) await second;
        }
    }

    [Fact]
    public async Task Republish_GenerationAllocationThrows_ReturnsTransportFailure()
    {
        await using var republisher = new DlqRepublisher(
            new RabbitMqConnectionOptions(),
            new RabbitMqSubscriberOptions(),
            1,
            NullLogger.Instance,
            generationFactory: _ => throw new InvalidOperationException("Allocation failed."));
        (await republisher.RepublishAsync(Request(), CancellationToken.None))
            .Should().Be(DlqRepublishOutcome.TransportFailure);
        republisher.State.Should().BeOfType<DlqRepublisherState.Empty>();
    }

    [Fact]
    public async Task Complete_SharedScenarios_PreservesExpectedOutcome()
    {
        using var fixture = DlqLifetimeContractFixture.Load();
        foreach (var row in fixture.RootElement.GetProperty("outcomes").EnumerateArray())
        {
            await using var operation = new DlqRepublishOperation(
                1,
                Request(),
                Duration.FromSeconds(5),
                new ManualTimeProviderFixture(),
                CancellationToken.None,
                _ => { });
            operation.Complete(ParseOutcome(row.GetProperty("first").GetString()!));
            operation.Complete(ParseOutcome(row.GetProperty("later").GetString()!));
            (await operation.Completion).Should().Be(ParseOutcome(row.GetProperty("expected").GetString()!));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Settle_SettlementThrows_DoesNotPropagateToRepublish(bool confirmed)
    {
        var created = SettlementChannelFixture.Create();
        await using var channel = created.Channel;
        created.Fixture.FailSettlement = true;
        await SubscriberChannel.SettleDlqDeliveryAsync(
            channel, confirmed, 1, "fixture.queue", "fixture-id", NullLogger.Instance);
        created.Fixture.Acknowledgments.Should().Be(confirmed ? 1 : 0);
        created.Fixture.Rejections.Should().Be(confirmed ? 0 : 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Settle_DiagnosticThrows_DoesNotPropagateToRepublish(bool confirmed)
    {
        var created = SettlementChannelFixture.Create();
        await using var channel = created.Channel;
        created.Fixture.FailSettlement = true;
        var logger = new DlqCleanupLoggerFixture(true);
        await SubscriberChannel.SettleDlqDeliveryAsync(
            channel, confirmed, 1, "fixture.queue", "fixture-id", logger);
        created.Fixture.Acknowledgments.Should().Be(confirmed ? 1 : 0);
        created.Fixture.Rejections.Should().Be(confirmed ? 0 : 1);
        logger.Messages.Should().ContainSingle();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Settle_OpenChannel_AppliesOutcomeOnce(bool confirmed)
    {
        var created = SettlementChannelFixture.Create();
        await using var channel = created.Channel;
        await SubscriberChannel.SettleDlqDeliveryAsync(
            channel, confirmed, 1, "fixture.queue", "fixture-id", NullLogger.Instance);
        created.Fixture.Acknowledgments.Should().Be(confirmed ? 1 : 0);
        created.Fixture.Rejections.Should().Be(confirmed ? 0 : 1);
    }

    [Fact]
    public async Task Settle_ClosedOriginalChannel_DoesNotAttemptSettlement()
    {
        var created = SettlementChannelFixture.Create();
        await using var channel = created.Channel;
        created.Fixture.Open = false;
        await SubscriberChannel.SettleDlqDeliveryAsync(
            channel, true, 1, "fixture.queue", "fixture-id", NullLogger.Instance);
        created.Fixture.Acknowledgments.Should().Be(0);
        created.Fixture.Rejections.Should().Be(0);
    }

    [Fact]
    public async Task Republish_RetiringAcquisition_RejectsReplacementUntilCleanupCompletes()
    {
        var (connection, fixture) = DelayedDlqConnectionFixture.Create();
        var allocations = 0;
        DlqTransportGeneration? retained = null;

        // The generationFactory argument lets the test supply the transport
        // generation that retains the acquisition. Without that argument, this
        // retained-acquisition test fails to compile.
        await using var republisher = new DlqRepublisher(
            new RabbitMqConnectionOptions(),
            new RabbitMqSubscriberOptions(
                Duration.FromMilliseconds(100), Duration.FromMilliseconds(1)),
            1,
            NullLogger.Instance,
            generationFactory: id =>
            {
                Interlocked.Increment(ref allocations);
                return retained = new DlqTransportGeneration(
                    id, "amqp://localhost", "late-fixture", NullLogger.Instance, fixture.Acquire);
            });
        try
        {
            var first = republisher.RepublishAsync(Request(), CancellationToken.None).AsTask();
            await fixture.Started;
            (await first).Should().Be(DlqRepublishOutcome.DeadlineExceeded);
            (await republisher.RepublishAsync(Request(), CancellationToken.None))
                .Should().Be(DlqRepublishOutcome.CleanupPending);
            allocations.Should().Be(1);
        }
        finally
        {
            fixture.Complete(connection);
            if (retained is not null) await retained.ResourceCompletion;
        }

        fixture.Disposals.Should().Be(1);
        fixture.ChannelAcquisitions.Should().Be(0);
    }

    [Fact]
    public async Task Terminate_DuringAcquisition_DisposesLateConnectionWithoutUsingIt()
    {
        var (connection, fixture) = DelayedDlqConnectionFixture.Create();

        // The constructor takes the acquisition delegate; without it, this test
        // fails to compile. A generation whose cleanup was discarded would never
        // complete.
        await using var generation = new DlqTransportGeneration(
            1, "amqp://localhost", "late-fixture", NullLogger.Instance, fixture.Acquire);
        await using var operation = new DlqRepublishOperation(
            1,
            Request(),
            Duration.FromSeconds(5),
            new ManualTimeProviderFixture(),
            CancellationToken.None,
            g => g.Terminate());
        operation.Own(generation);
        generation.Start(operation, () => { }).Should().BeTrue();
        await fixture.Started;
        operation.Complete(DlqRepublishOutcome.DeadlineExceeded);
        generation.ResourceCompletion.IsCompleted.Should().BeFalse();
        var cleanup = generation.CleanupWork;
        cleanup.Should().NotBeNull();
        cleanup.IsCompleted.Should().BeFalse();
        fixture.Complete(connection);
        await generation.ResourceCompletion;
        await cleanup;
        fixture.Disposals.Should().Be(1);
        fixture.ChannelAcquisitions.Should().Be(0);
        generation.ChannelAcquisition.Should().BeNull();
        generation.Send.Should().BeNull();
    }

    [Theory]
    [InlineData("DeadlineExceeded")]
    [InlineData("Stopping")]
    [InlineData("TransportFailure")]
    [InlineData("Unroutable")]
    [InlineData("CleanupPending")]
    public async Task Complete_AfterTerminalFailure_PreservesFirstOutcome(string value)
    {
        var outcome = Enum.Parse<DlqRepublishOutcome>(value);
        var time = new ManualTimeProviderFixture();
        await using var operation = new DlqRepublishOperation(
            1, Request(), Duration.FromSeconds(5), time, CancellationToken.None, _ => { });
        operation.Complete(outcome);
        operation.Complete(DlqRepublishOutcome.Confirmed);
        time.FireTimers();
        (await operation.Completion).Should().Be(outcome);
        operation.State.Should().Be(new DlqRepublishOperation.Terminal(outcome));
    }

    [Fact]
    public async Task Confirm_AfterDeadlineWithoutTimerDelivery_Expires()
    {
        var time = new ManualTimeProviderFixture();
        await using var generation = Generation();
        await using var operation = new DlqRepublishOperation(
            1,
            Request(),
            Duration.FromSeconds(5),
            time,
            CancellationToken.None,
            g => g.Terminate());
        operation.Own(generation);
        time.AdvanceWithoutTimers(5000);
        operation.Confirm();
        (await operation.Completion).Should().Be(DlqRepublishOutcome.DeadlineExceeded);
        generation.IsRetired.Should().BeTrue();
    }

    [Fact]
    public async Task Confirm_BeforeDeadline_RemainsConfirmedAfterCancellation()
    {
        var time = new ManualTimeProviderFixture();
        using var cancellation = new CancellationTokenSource();
        await using var generation = Generation();
        await using var operation = new DlqRepublishOperation(
            1, Request(), Duration.FromSeconds(5), time, cancellation.Token, g => g.Terminate());
        operation.Own(generation);
        time.AdvanceWithoutTimers(4999);
        operation.Confirm();
        await cancellation.CancelAsync();
        time.FireTimers();
        (await operation.Completion).Should().Be(DlqRepublishOutcome.Confirmed);
        generation.IsRetired.Should().BeFalse();
    }

    [Fact]
    public async Task Own_AfterCancellation_RetiresLateGeneration()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await using var generation = Generation();
        await using var operation = new DlqRepublishOperation(
            1,
            Request(),
            Duration.FromSeconds(5),
            new ManualTimeProviderFixture(),
            cancellation.Token,
            g => g.Terminate());
        operation.Own(generation);
        generation.IsRetired.Should().BeTrue();
        generation.ConnectionAcquisition.Should().BeNull();
        (await operation.Completion).Should().Be(DlqRepublishOutcome.Stopping);
    }

    [Fact]
    public async Task Dispose_BeforeFirstDelivery_ClosesAdmissionWithoutTransport()
    {
        var republisher = new DlqRepublisher(
            new RabbitMqConnectionOptions(),
            new RabbitMqSubscriberOptions(),
            1,
            NullLogger.Instance);
        var first = republisher.DisposeAsync().AsTask();
        var second = republisher.DisposeAsync().AsTask();
        second.Should().BeSameAs(first);
        await first;
        var closed = republisher.State.Should().BeOfType<DlqRepublisherState.Closed>().Subject;
        closed.Generation.Should().BeNull();
        await closed.ResourceCompletion;
        var resourceCleanup = republisher.ResourceCleanup;
        var closeWork = republisher.CloseWork;
        resourceCleanup.Should().NotBeNull();
        closeWork.Should().NotBeNull();
        await resourceCleanup;
        await closeWork;
        (await republisher.RepublishAsync(Request(), CancellationToken.None))
            .Should().Be(DlqRepublishOutcome.Stopping);
    }

    private static DlqRepublishRequest Request() => new(
        "test.dlx", new byte[] { 1 }, new Dictionary<string, object?>(), null, null, null, null);

    private static DlqRepublishOutcome ParseOutcome(string value) =>
        Enum.Parse<DlqRepublishOutcome>(
            value.Replace("-", string.Empty, StringComparison.Ordinal), true);

    private static DlqTransportGeneration Generation() =>
        new(1, "amqp://localhost", "unit-fixture", NullLogger.Instance);
}
