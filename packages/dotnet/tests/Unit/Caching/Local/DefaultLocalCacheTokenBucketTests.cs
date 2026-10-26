// -----------------------------------------------------------------------
// <copyright file="DefaultLocalCacheTokenBucketTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Caching.Local;

using AwesomeAssertions;
using DcsvIo.D2.Caching;
using DcsvIo.D2.Caching.Local.Default;
using DcsvIo.D2.Tests.Unit.Caching.Local.Fixtures;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

/// <summary>
/// The token-bucket primitive on the in-process flavor, driven by a fake clock
/// so the refill is observed rather than waited for. The Redis flavor runs the
/// same algorithm as a Lua script and is exercised against a real server in
/// <c>Integration/Caching/Distributed</c>.
/// </summary>
public sealed class DefaultLocalCacheTokenBucketTests
{
    /// <summary>The refill interval every case below measures its rate over.</summary>
    private static readonly TimeSpan sr_minute = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets the empty-key row. <see cref="string.Empty"/> is not a constant,
    /// so it cannot be an <c>InlineData</c> argument.
    /// </summary>
    public static TheoryData<string?, long, int, long, long, string> EmptyKeyRow =>
        new() { { string.Empty, 10, 60, 5, 1, "key" } };

    [Fact]
    public async Task ConsumeToken_ClockRecoversFromRollback_DoesNotCreditTimeTwice()
    {
        var clock = new SteppableClockFixture();
        clock.Advance(TimeSpan.FromMilliseconds(1000));
        using var cache = NewCache(clock);
        var interval = TimeSpan.FromMilliseconds(1000);
        (await cache.ConsumeTokenAsync("rollback", 1, interval, 1)).Data.Admitted
            .Should().BeTrue();

        clock.StepBack(TimeSpan.FromMilliseconds(1000));
        (await cache.ConsumeTokenAsync("rollback", 1, interval, 1)).Data.Admitted
            .Should().BeFalse();
        clock.Advance(TimeSpan.FromMilliseconds(1000));
        (await cache.ConsumeTokenAsync("rollback", 1, interval, 1)).Data.Admitted
            .Should().BeFalse();
        clock.Advance(TimeSpan.FromMilliseconds(999));
        (await cache.ConsumeTokenAsync("rollback", 1, interval, 1)).Data.RetryAfter
            .Should().Be(TimeSpan.FromMilliseconds(1));
        clock.Advance(TimeSpan.FromMilliseconds(1));
        (await cache.ConsumeTokenAsync("rollback", 1, interval, 1)).Data.Admitted
            .Should().BeTrue();
    }

    [Fact]
    public async Task ConsumeToken_ClockRollsBackWithAccrualRemainder_PreservesFraction()
    {
        var clock = new SteppableClockFixture();
        using var cache = NewCache(clock);
        var interval = TimeSpan.FromMilliseconds(3000);
        await cache.ConsumeTokenAsync("remainder", 1, interval, 1);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await cache.ConsumeTokenAsync("remainder", 1, interval, 1);
        clock.StepBack(TimeSpan.FromMilliseconds(1));
        await cache.ConsumeTokenAsync("remainder", 1, interval, 1);
        clock.Advance(TimeSpan.FromMilliseconds(2999));

        var before = await cache.ConsumeTokenAsync("remainder", 1, interval, 1);

        before.Data.Admitted.Should().BeFalse();
        before.Data.RetryAfter.Should().Be(TimeSpan.FromMilliseconds(1));
        clock.Advance(TimeSpan.FromMilliseconds(1));
        (await cache.ConsumeTokenAsync("remainder", 1, interval, 1)).Data.Admitted
            .Should().BeTrue();
    }

    [Fact]
    public async Task ConsumeToken_ABucketNobodyUsed_AdmitsItsWholeBurst()
    {
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock);
        var admitted = 0;

        for (var i = 0; i < 6; i++)
        {
            var decision = await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 5);

            if (decision.Data.Admitted)
                admitted++;
        }

        admitted.Should().Be(5, "a bucket is created full and holds its capacity");
    }

    [Fact]
    public async Task ConsumeToken_PastTheBurst_ReportsTheWaitForOneToken()
    {
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock);

        for (var i = 0; i < 5; i++)
            await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 5);

        var refused = await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 5);

        refused.Data.Admitted.Should().BeFalse();
        refused.Data.RetryAfter.Should().Be(
            TimeSpan.FromSeconds(6),
            "ten tokens a minute is one token every six seconds, and the wait is "
            + "the time until the bucket holds what the caller asked for");
    }

    [Fact]
    public async Task ConsumeToken_AfterTheWaitItReported_AdmitsAgain()
    {
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock);

        for (var i = 0; i < 5; i++)
            await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 5);

        var refused = await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 5);
        clock.Advance(refused.Data.RetryAfter);

        var next = await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 5);

        next.Data.Admitted.Should().BeTrue(
            "a caller that honors the reported wait is admitted when it returns, "
            + "or the wait told it to come back too early");
    }

    /// <summary>
    /// The property a fixed window does not have. A window of one minute
    /// admitting ten requests admits ten at the end of one window and ten more a
    /// second later at the start of the next; a bucket that refills by elapsed
    /// time has only what the elapsed seconds added.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task ConsumeToken_TwoSecondsAfterTheBurstIsSpent_AdmitsNothing()
    {
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock);

        for (var i = 0; i < 10; i++)
            await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 10);

        clock.Advance(TimeSpan.FromSeconds(2));
        var admitted = 0;

        for (var i = 0; i < 10; i++)
        {
            var decision = await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 10);

            if (decision.Data.Admitted)
                admitted++;
        }

        admitted.Should().Be(
            0, "two seconds of a ten-a-minute rate is a third of one token");
    }

    [Fact]
    public async Task ConsumeToken_AFullIntervalAfterTheBurstIsSpent_HasRefilledTheBurst()
    {
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock);

        for (var i = 0; i < 10; i++)
            await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 10);

        clock.Advance(sr_minute);
        var admitted = 0;

        for (var i = 0; i < 11; i++)
        {
            var decision = await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 10);

            if (decision.Data.Admitted)
                admitted++;
        }

        admitted.Should().Be(10, "the refill is clamped to the bucket's capacity");
    }

    [Fact]
    public async Task ConsumeToken_ARefillPastTheCapacity_IsClampedToIt()
    {
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock);

        await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 3);
        clock.Advance(TimeSpan.FromHours(1));
        var admitted = 0;

        for (var i = 0; i < 4; i++)
        {
            var decision = await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 3);

            if (decision.Data.Admitted)
                admitted++;
        }

        admitted.Should().Be(
            3, "an hour of idling does not bank an hour's worth of requests");
    }

    [Fact]
    public async Task ConsumeToken_AClockSteppedBackwards_RefillsNothing()
    {
        var clock = new SteppableClockFixture();
        using var cache = NewCache(clock);

        for (var i = 0; i < 3; i++)
            await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 3);

        clock.StepBack(TimeSpan.FromMinutes(10));
        var decision = await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 3);

        decision.Data.Admitted.Should().BeFalse(
            "an instant later than the clock's own now is a step backwards, and a "
            + "negative elapsed span would drain the bucket rather than refill it");
    }

    [Fact]
    public async Task ConsumeToken_ACostOverOne_ConsumesThatMuch()
    {
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock);

        var first = await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 5, cost: 3);
        var second = await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 5, cost: 3);

        first.Data.Admitted.Should().BeTrue();
        second.Data.Admitted.Should().BeFalse("three and three do not fit in five");
    }

    [Fact]
    public async Task ConsumeToken_ABucketLeftAlone_ExpiresAfterARefillFromEmpty()
    {
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock);

        await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 10);

        (await cache.GetTtlAsync("bucket")).Data.Should().Be(
            TimeSpan.FromMinutes(2),
            "ten tokens a minute refills a ten-token bucket in a minute, and the "
            + "lifetime carries one interval of slack past it");
    }

    [Fact]
    public async Task ConsumeToken_ManyCallersOnOneBucket_AdmitNoMoreThanTheBurst()
    {
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock);
        var admitted = 0;

        await Parallel.ForAsync(
            0,
            200,
            async (_, ct) =>
            {
                var decision = await cache.ConsumeTokenAsync(
                    "contended", 10, sr_minute, burst: 50, cost: 1, ct);

                if (decision.Data.Admitted)
                    Interlocked.Increment(ref admitted);
            });

        admitted.Should().Be(
            50,
            "the refill and the consume are one step, so two callers cannot both "
            + "spend the last token; the clock does not advance, so nothing refills");
    }

    /// <summary>
    /// The clock hands every read its own instant, so the callers of one
    /// contended bucket read a spread of instants rather than one. A bucket may
    /// admit its capacity plus what the run's own span added and no more; a
    /// stored instant that moved backwards would let the next caller measure
    /// part of that span a second time.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task ConsumeToken_ManyCallersAgainstAMovingClock_AdmitNoMoreThanTheSpanAdded()
    {
        var clock = new AdvancingClockFixture(TimeSpan.FromSeconds(1));
        using var cache = NewCache(clock);
        var interval = TimeSpan.FromSeconds(10);
        var admitted = 0;

        await Parallel.ForAsync(
            0,
            128,
            async (_, ct) =>
            {
                var decision = await cache.ConsumeTokenAsync(
                    "moving", 1, interval, burst: 10, cost: 1, ct);

                if (decision.Data.Admitted)
                    Interlocked.Increment(ref admitted);
            });

        var accrued = (int)Math.Floor(clock.Span / interval);

        admitted.Should().BeLessThanOrEqualTo(
            10 + accrued,
            "a bucket holds its capacity plus one token per interval the run "
            + "spanned, and no interleaving of its callers may add more than that");
    }

    [Fact]
    public async Task ConsumeToken_AKeyHoldingSomethingElse_AnswersConflict()
    {
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock);
        await cache.SetAsync("bucket", "not a bucket");

        var result = await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 5);

        result.IsConflict.Should().BeTrue(
            "the bucket shares the value namespace, so a key holding something "
            + "else answers as an increment on a non-numeric key does");
    }

    [Theory]
    [InlineData(null, 10, 60, 5, 1, "key")]
    [MemberData(nameof(EmptyKeyRow))]
    [InlineData("   ", 10, 60, 5, 1, "key")]
    [InlineData("k", 0, 60, 5, 1, "refillTokens")]
    [InlineData("k", -1, 60, 5, 1, "refillTokens")]
    [InlineData("k", 10, 0, 5, 1, "refillInterval")]
    [InlineData("k", 10, 60, 0, 1, "burst")]
    [InlineData("k", 10, 60, 5, 0, "cost")]
    [InlineData("k", 10, 60, 5, 6, "cost")]
    public async Task ConsumeToken_AnArgumentTheBucketCannotUse_IsRefusedByName(
        string? key,
        long refillTokens,
        int intervalSeconds,
        long burst,
        long cost,
        string field)
    {
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock);

        var result = await cache.ConsumeTokenAsync(
            key!, refillTokens, TimeSpan.FromSeconds(intervalSeconds), burst, cost);

        result.IsValidationFailed.Should().BeTrue();
        result.InputErrors.Should().ContainSingle(e => e.Field == field);
    }

    [Fact]
    public async Task ConsumeToken_EveryArgumentAtItsBound_IsAdmitted()
    {
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock);

        var result = await cache.ConsumeTokenAsync(
            "k", refillTokens: 1, TimeSpan.FromMilliseconds(1), burst: 1, cost: 1);

        result.IsOk.Should().BeTrue(
            "the smallest value each bound admits is admitted, so a bound placed "
            + "one out would refuse a request the contract accepts");
        result.Data.Admitted.Should().BeTrue();
    }

    [Fact]
    public async Task ConsumeToken_TwoKeys_AreTwoBuckets()
    {
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock);

        await cache.ConsumeTokenAsync("one", 10, sr_minute, burst: 1);
        var other = await cache.ConsumeTokenAsync("two", 10, sr_minute, burst: 1);

        other.Data.Admitted.Should().BeTrue("a bucket is per key");
    }

    /// <summary>
    /// Ten tokens a minute is one thousandth of a token every six milliseconds,
    /// so a caller arriving every millisecond arrives twice inside every step the
    /// refill can take. The rate over the whole run must still be the configured
    /// one: twelve seconds of a ten-a-minute rate is two tokens.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task ConsumeToken_ArrivalsFasterThanOneThousandthAccrues_KeepsTheRate()
    {
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock);

        for (var i = 0; i < 5; i++)
            await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 5);

        var admitted = 0;

        for (var ms = 0; ms < 12_000; ms++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(1));
            var decision = await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 5);

            if (decision.Data.Admitted)
                admitted++;
        }

        admitted.Should().Be(
            2,
            "the accrual a refill could not spend on a whole thousandth is carried "
            + "to the next one, so arriving inside a step neither loses the elapsed "
            + "time nor banks it twice");
    }

    [Fact]
    public async Task ConsumeToken_ArrivalsFasterThanOneThousandthAccrues_ReportsAShrinkingWait()
    {
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock);

        for (var i = 0; i < 5; i++)
            await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 5);

        var first = await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 5);

        for (var ms = 0; ms < 3_000; ms++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(1));
            await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 5);
        }

        var later = await cache.ConsumeTokenAsync("bucket", 10, sr_minute, burst: 5);

        later.Data.RetryAfter.Should().Be(
            first.Data.RetryAfter - TimeSpan.FromSeconds(3),
            "the wait a refusal quotes is only usable if the arrivals that took "
            + "place during it did not push the bucket's own instant forward");
    }

    [Fact]
    public async Task ConsumeToken_AnIntervalUnderOneMillisecond_IsRefusedByName()
    {
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock);

        // Half a millisecond. Every flavor measures the interval in whole
        // milliseconds, so an accepted value under one would divide by zero.
        var result = await cache.ConsumeTokenAsync(
            "k", 10, TimeSpan.FromTicks(5_000), burst: 5);

        result.IsValidationFailed.Should().BeTrue();
        result.InputErrors.Should().ContainSingle(e => e.Field == "refillInterval");
    }

    [Fact]
    public async Task ConsumeToken_AnIntervalThatIsNotAWholeMillisecond_IsRefusedByName()
    {
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock);

        // One and a half milliseconds. Every flavor casts the interval to whole
        // milliseconds, so an accepted fractional value refills the bucket at a
        // rate the caller did not ask for - here one and a half times it.
        var result = await cache.ConsumeTokenAsync(
            "k", 10, TimeSpan.FromMilliseconds(1.5), burst: 5);

        result.IsValidationFailed.Should().BeTrue(
            "the TypeScript twin refuses the same argument, and one call cannot be "
            + "a validation failure on one runtime and a rescaled bucket on the "
            + "other");
        result.InputErrors.Should().ContainSingle(e => e.Field == "refillInterval");
    }

    private static DefaultLocalCache NewCache(TimeProvider clock)
        => new(Options.Create(new LocalCacheOptions()), clock);
}
