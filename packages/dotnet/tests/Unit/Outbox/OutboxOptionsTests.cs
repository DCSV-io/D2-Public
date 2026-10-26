// -----------------------------------------------------------------------
// <copyright file="OutboxOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox;

using System.ComponentModel.DataAnnotations;
using AwesomeAssertions;
using DcsvIo.D2.Outbox;
using DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Tests.Unit.Outbox.Fixtures;
using NodaTime;
using Xunit;

/// <summary>
/// The defaults, and the arithmetic they were chosen for.
/// </summary>
/// <remarks>
/// These values change behavior. The retry limit, the base and the ceiling
/// together decide how long a broker outage is survived, and lowering any of
/// them silently makes an outage abandon messages. The window assertion below
/// states the surviving duration in full, so a lowered value fails there.
/// </remarks>
public sealed class OutboxOptionsTests
{
    [Fact]
    public void Section_IsThePinnedConfigurationSection()
        => OutboxOptions.SECTION.Should().Be("LIB_OUTBOX");

    [Fact]
    public void Defaults_RelayTick_IsFiveSeconds()
        => New().RelayTick.Should().Be(TimeSpan.FromSeconds(5));

    [Fact]
    public void Defaults_BatchSize_IsOneHundred()
        => New().BatchSize.Should().Be(100);

    [Fact]
    public void Defaults_RetryLimit_IsSeventy()
        => New().RetryLimit.Should().Be(70);

    [Fact]
    public void Defaults_RetryBackoffBase_IsTwoSeconds()
        => New().RetryBackoffBase.Should().Be(TimeSpan.FromSeconds(2));

    [Fact]
    public void Defaults_RetryBackoffCeiling_IsOneMinute()
        => New().RetryBackoffCeiling.Should().Be(TimeSpan.FromSeconds(60));

    /// <summary>
    /// The window the defaults give, computed from them rather than restated.
    /// The limit counts attempts, so there is one fewer wait: the waits after
    /// attempts 1 to 5 double from two seconds (2, 4, 8, 16 and 32 seconds) and
    /// the rest sit at the ceiling. Lower the limit or the ceiling and this
    /// figure drops below the hour an outage takes.
    /// </summary>
    [Fact]
    public void Defaults_TheRetryWindow_OutlastsAnHourLongOutage()
    {
        // Summed over the shipped curve rather than a copy of it, so a change
        // that shortened the real window fails here instead of leaving this
        // test green while it claims to protect that window.
        var options = New();
        var total = Duration.Zero;

        for (var attemptsAlreadyMade = 1; attemptsAlreadyMade < options.RetryLimit;
            attemptsAlreadyMade++)
        {
            total += OutboxRelay<IOutboxProbeDbFixture>.ComputeBackoff(
                options, attemptsAlreadyMade);
        }

        total.ToTimeSpan().Should().BeGreaterThan(
            TimeSpan.FromHours(1),
            "a message must not be abandoned inside the hour-long broker outage the "
            + "walk this package was designed against describes");
    }

    /// <summary>
    /// The shipped defaults have to survive the annotations, or every consumer
    /// that validates on start refuses to boot on a configuration nobody
    /// changed. This is also the admitting half of every rejection below.
    /// </summary>
    [Fact]
    public void TheShippedDefaults_PassValidation()
        => Validate(static _ => { }).Should().BeEmpty();

    [Fact]
    public void ABatchSizeOfZero_IsRejected()
        => Validate(static o => o.BatchSize = 0)
            .Should().ContainSingle()
            .Which.MemberNames.Should().Contain(nameof(OutboxOptions.BatchSize));

    [Fact]
    public void ARetryLimitOfZero_IsRejected()
        => Validate(static o => o.RetryLimit = 0)
            .Should().ContainSingle()
            .Which.MemberNames.Should().Contain(nameof(OutboxOptions.RetryLimit));

    [Fact]
    public void ARelayTickOfZero_IsRejected()
        => Validate(static o => o.RelayTick = TimeSpan.Zero)
            .Should().ContainSingle()
            .Which.MemberNames.Should().Contain(nameof(OutboxOptions.RelayTick));

    [Fact]
    public void ANegativeRetryBackoffBase_IsRejected()
        => Validate(static o => o.RetryBackoffBase = TimeSpan.FromSeconds(-1))
            .Should().ContainSingle().Which.MemberNames.Should()
            .Contain(nameof(OutboxOptions.RetryBackoffBase));

    [Fact]
    public void ANegativeRetryBackoffCeiling_IsRejected()
        => Validate(static o => o.RetryBackoffCeiling = TimeSpan.FromSeconds(-1))
            .Should().ContainSingle().Which.MemberNames.Should()
            .Contain(nameof(OutboxOptions.RetryBackoffCeiling));

    /// <summary>
    /// <c>required</c> is a compile-time construct the configuration binder does
    /// not apply, so a host that binds the section and never calls
    /// <c>AddD2Outbox</c> reaches the relay with no connection string at all.
    /// </summary>
    [Fact]
    public void AnUnsetRelayConnectionString_IsRejected()
        => Validate(static o => o.RelayConnectionString = string.Empty)
            .Should().ContainSingle().Which.MemberNames.Should()
            .Contain(nameof(OutboxOptions.RelayConnectionString));

    /// <summary>
    /// The binder leaves zero when nothing set the key, and two modules that both
    /// default to it share one lock - so one relay's tick is skipped every time,
    /// silently.
    /// </summary>
    [Fact]
    public void AnUnsetRelayAdvisoryLockKey_IsRejected()
        => Validate(static o => o.RelayAdvisoryLockKey = 0)
            .Should().ContainSingle().Which.MemberNames.Should()
            .Contain(nameof(OutboxOptions.RelayAdvisoryLockKey));

    /// <summary>
    /// Exactly what <c>ValidateDataAnnotations()</c> runs at a consumer's start
    /// gate - <c>Validator.TryValidateObject</c> over every property.
    /// </summary>
    /// <param name="configure">What to make wrong about an otherwise valid set.</param>
    /// <returns>Every violation found.</returns>
    private static IReadOnlyList<ValidationResult> Validate(Action<OutboxOptions> configure)
    {
        var options = New();
        configure(options);

        var results = new List<ValidationResult>();
        _ = Validator.TryValidateObject(
            options, new ValidationContext(options), results, validateAllProperties: true);

        return results;
    }

    private static OutboxOptions New() => new()
    {
        RelayAdvisoryLockKey = 1,
        RelayConnectionString = "Host=localhost;Database=d2;Username=u;Password=p",
    };
}
