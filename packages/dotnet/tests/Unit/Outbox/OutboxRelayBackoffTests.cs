// -----------------------------------------------------------------------
// <copyright file="OutboxRelayBackoffTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox;

using AwesomeAssertions;
using DcsvIo.D2.Outbox;
using DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Tests.Unit.Outbox.Fixtures;
using NodaTime;
using Xunit;

/// <summary>
/// The retry curve. It decides how long an outage is survived and how often a
/// broker that is already down receives an attempt while it recovers.
/// </summary>
public sealed class OutboxRelayBackoffTests
{
    [Fact]
    public void ComputeBackoff_AfterTheFirstAttempt_WaitsTheBase()
        => Backoff(1).Should().Be(Duration.FromSeconds(2));

    [Fact]
    public void ComputeBackoff_AfterTheSecondAttempt_DoublesTheBase()
        => Backoff(2).Should().Be(Duration.FromSeconds(4));

    [Fact]
    public void ComputeBackoff_AfterTheThirdAttempt_DoublesAgain()
        => Backoff(3).Should().Be(Duration.FromSeconds(8));

    [Fact]
    public void ComputeBackoff_OnceTheCurveWouldPassTheCeiling_ReturnsTheCeiling()
    {
        // Two seconds doubled five times is 64s, past the 60s ceiling. Past a
        // minute a longer wait does not help and only delays recovery.
        Backoff(6).Should().Be(Duration.FromSeconds(60));
    }

    [Fact]
    public void ComputeBackoff_FarPastTheCeiling_StaysAtTheCeiling()
        => Backoff(40).Should().Be(Duration.FromSeconds(60));

    [Fact]
    public void ComputeBackoff_WithNoAttemptsYetMade_WaitsTheBase()
    {
        // Nothing calls it this way today. It returns the base rather than
        // shifting by a negative count, which is undefined.
        Backoff(0).Should().Be(Duration.FromSeconds(2));
    }

    [Fact]
    public void ComputeBackoff_WithANegativeAttemptCount_WaitsTheBase()
        => Backoff(-5).Should().Be(Duration.FromSeconds(2));

    /// <summary>
    /// The overflow guard. Without the bound on the shift, a retry limit an
    /// operator set high enough multiplies past <c>long</c> and produces a
    /// NEGATIVE wait, which reads as attemptable-now and turns the backoff into a
    /// hot loop against a broker that is already down.
    /// </summary>
    [Fact]
    public void ComputeBackoff_AtAnAttemptCountThatWouldOverflowTheShift_StaysPositive()
    {
        var wait = Backoff(64);

        wait.Should().BeGreaterThan(Duration.Zero);
    }

    [Fact]
    public void ComputeBackoff_AtAnAttemptCountThatWouldOverflowTheShift_IsTheCeiling()
        => Backoff(64).Should().Be(Duration.FromSeconds(60));

    [Fact]
    public void ComputeBackoff_AtTheLastAttemptCountTheShiftStillHandles_IsTheCeiling()
    {
        // Thirty doublings is the last the guard lets through, and the product is
        // already far past the ceiling - so the shift arm and the ceiling clamp agree
        // at the boundary.
        Backoff(31).Should().Be(Duration.FromSeconds(60));
    }

    [Fact]
    public void ComputeBackoff_WithABaseAlreadyPastTheCeiling_ReturnsTheCeiling()
    {
        // A misconfiguration rather than a curve. The ceiling is the upper bound
        // on a wait, so it takes precedence over the base.
        var options = Options();
        options.RetryBackoffBase = TimeSpan.FromMinutes(5);

        OutboxRelay<IOutboxProbeDbFixture>.ComputeBackoff(options, 1)
            .Should().Be(Duration.FromSeconds(60));
    }

    [Fact]
    public void ComputeBackoff_WithAZeroBase_NeverWaits()
        => OutboxRelay<IOutboxProbeDbFixture>.ComputeBackoff(ZeroBase(), 4)
            .Should().Be(Duration.Zero);

    private static OutboxOptions ZeroBase()
    {
        var options = Options();
        options.RetryBackoffBase = TimeSpan.Zero;

        return options;
    }

    private static Duration Backoff(int attemptsAlreadyMade)
        => OutboxRelay<IOutboxProbeDbFixture>.ComputeBackoff(Options(), attemptsAlreadyMade);

    private static OutboxOptions Options() => new()
    {
        RelayAdvisoryLockKey = 1,
        RelayConnectionString = "Host=outbox-probe.invalid;Database=d2;Username=u;Password=p",
    };
}
