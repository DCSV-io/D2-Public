// -----------------------------------------------------------------------
// <copyright file="TimeoutLayerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.Pipeline;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.Pipeline;
using Xunit;

public sealed class TimeoutLayerTests
{
    // ----------------------------------------------------------------------
    // Happy path
    // ----------------------------------------------------------------------

    [Fact]
    public async Task WrapAsync_OpCompletesBeforeTimeout_ReturnsValue()
    {
        var layer = new TimeoutLayer<string, int>(new(TimeSpan.FromSeconds(5)));

        var result = await layer.WrapAsync("k", _ => ValueTask.FromResult(42), default);

        result.Should().Be(42);
    }

    // ----------------------------------------------------------------------
    // Timeout fires
    // ----------------------------------------------------------------------

    [Fact]
    public async Task WrapAsync_TimeoutFires_ThrowsTimeoutException()
    {
        // A very short timeout; the op waits until the linked CT is canceled.
        var layer = new TimeoutLayer<string, int>(new(TimeSpan.FromMilliseconds(20)));

        var act = () => layer.WrapAsync(
            "k",
            async ct =>
            {
                await Task.Delay(-1, ct);
                return 0;
            },
            default).AsTask();

        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task WrapAsync_TimeoutFires_MessageContainsDuration()
    {
        var duration = TimeSpan.FromMilliseconds(20);
        var layer = new TimeoutLayer<string, int>(new(duration));

        var ex = await Assert.ThrowsAsync<TimeoutException>(() => layer.WrapAsync(
            "k",
            async ct =>
            {
                await Task.Delay(-1, ct);
                return 0;
            },
            default).AsTask());

        ex.Message.Should().Contain(duration.ToString());
    }

    [Fact]
    public async Task WrapAsync_TimeoutFires_NotOperationCanceledException()
    {
        // The layer must convert the OCE to a TimeoutException (NOT rethrow it as an OCE),
        // so the caller can tell a timeout from a caller cancellation.
        var layer = new TimeoutLayer<string, int>(new(TimeSpan.FromMilliseconds(20)));

        var act = () => layer.WrapAsync(
            "k",
            async ct =>
            {
                await Task.Delay(-1, ct);
                return 0;
            },
            default).AsTask();

        await act.Should().ThrowAsync<TimeoutException>();
        await act.Should().NotThrowAsync<OperationCanceledException>();
    }

    // ----------------------------------------------------------------------
    // Pass-through (Duration <= Zero)
    // ----------------------------------------------------------------------

    [Fact]
    public async Task WrapAsync_DurationZero_PassesThrough_NoTimeout()
    {
        var layer = new TimeoutLayer<string, int>(new(TimeSpan.Zero));

        var result = await layer.WrapAsync("k", _ => ValueTask.FromResult(7), default);

        result.Should().Be(7);
    }

    [Fact]
    public async Task WrapAsync_DurationNegative_PassesThrough_NoTimeout()
    {
        var layer = new TimeoutLayer<string, int>(new(TimeSpan.FromSeconds(-1)));

        var result = await layer.WrapAsync("k", _ => ValueTask.FromResult(7), default);

        result.Should().Be(7);
    }

    [Fact]
    public async Task WrapAsync_NullOptions_UsesDefault10SecondTimeout()
    {
        var layer = new TimeoutLayer<string, int>();

        // The op completes immediately, so the default 10s timeout does not fire.
        var result = await layer.WrapAsync("k", _ => ValueTask.FromResult(99), default);

        result.Should().Be(99);
    }

    // ----------------------------------------------------------------------
    // Caller cancellation is NOT converted to a timeout
    // ----------------------------------------------------------------------

    [Fact]
    public async Task WrapAsync_CallerCtCanceled_PropagatesOce_NotTimeout()
    {
        var layer = new TimeoutLayer<string, int>(new(TimeSpan.FromSeconds(10)));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var canceledToken = cts.Token;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => layer.WrapAsync(
                "k",
                ct =>
                {
                    ct.ThrowIfCancellationRequested();
                    return ValueTask.FromResult(0);
                },
                canceledToken).AsTask());

        // cts is already canceled; a second call must NOT surface as TimeoutException.
        var ex = await Record.ExceptionAsync(
            () => layer.WrapAsync(
                "k",
                ct =>
                {
                    ct.ThrowIfCancellationRequested();
                    return ValueTask.FromResult(0);
                },
                canceledToken).AsTask());
        ex.Should().NotBeOfType<TimeoutException>();
    }

    [Fact]
    public async Task WrapAsync_CallerCtCanceled_NotMaskedEvenIfTimeoutAlsoArmed()
    {
        // Both tokens may cancel. The CALLER's token canceling must NOT produce
        // a TimeoutException; the when-guard keeps the caller's OCE from being
        // converted.
        var layer = new TimeoutLayer<string, int>(new(TimeSpan.FromMilliseconds(50)));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var canceledToken = cts.Token;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => layer.WrapAsync(
                "k",
                async ct =>
                {
                    await Task.Delay(1000, ct);
                    return 0;
                },
                canceledToken).AsTask());

        // The caller canceled before the timeout; a second call must NOT surface as
        // TimeoutException.
        var ex = await Record.ExceptionAsync(
            () => layer.WrapAsync(
                "k",
                async ct =>
                {
                    await Task.Delay(1000, ct);
                    return 0;
                },
                canceledToken).AsTask());
        ex.Should().NotBeOfType<TimeoutException>();
    }

    // ----------------------------------------------------------------------
    // Pipeline-level: per-attempt timeout + outer Retry
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Pipeline_PerAttemptTimeout_InsideRetry_Retried_ThenSucceeds()
    {
        // Composition: outer RetryLayer -> inner TimeoutLayer.
        // Attempt 1: the op waits -> TimeoutException (transient) -> Retry runs again.
        // Attempt 2: the op completes fast -> Ok.
        var attempts = 0;
        var pipeline = new ResilientPipeline<string, int>(
            NoDelayOptions(maxAttempts: 3),
            new TimeoutLayer<string, int>(new(TimeSpan.FromMilliseconds(50))));

        var result = await pipeline.ExecuteAsync("k", async ct =>
        {
            var n = Interlocked.Increment(ref attempts);
            if (n == 1)
            {
                // The first attempt waits until the linked timeout CT is canceled.
                await Task.Delay(-1, ct);
            }

            return 42;
        });

        result.Success.Should().BeTrue();
        result.Data.Should().Be(42);
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task Pipeline_PerAttemptTimeoutExhausted_MapsToServiceUnavailable()
    {
        // ALL attempts time out -> the pipeline boundary returns ServiceUnavailable.
        var pipeline = new ResilientPipeline<string, int>(
            NoDelayOptions(maxAttempts: 2),
            new TimeoutLayer<string, int>(new(TimeSpan.FromMilliseconds(30))));

        var result = await pipeline.ExecuteAsync("k", async ct =>
        {
            await Task.Delay(-1, ct);
            return 0;
        });

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // Pipeline-level: timeout at pipeline boundary -> ServiceUnavailable
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Pipeline_TotalTimeout_MapsToServiceUnavailable()
    {
        var pipeline = new ResilientPipeline<string, int>(
            new TimeoutLayer<string, int>(new(TimeSpan.FromMilliseconds(30))));

        var result = await pipeline.ExecuteAsync("k", async ct =>
        {
            await Task.Delay(-1, ct);
            return 0;
        });

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();
        result.IsTransientRetryable.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static RetryLayer<string, int> NoDelayOptions(int maxAttempts)
        => new(new()
        {
            MaxAttempts = maxAttempts,
            BaseDelayMs = 0,
            MaxDelayMs = 0,
            Jitter = false,
            DelayFunc = (_, _) => Task.CompletedTask,
        });
}
