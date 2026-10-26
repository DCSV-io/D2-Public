// -----------------------------------------------------------------------
// <copyright file="RetryHelperTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.Retry;

using System.Net;
using System.Net.Sockets;
using AwesomeAssertions;
using DcsvIo.D2.Resilience.CircuitBreaker;
using DcsvIo.D2.Resilience.Retry;
using DcsvIo.D2.Result;
using Xunit;

public sealed class RetryHelperTests
{
    // ----------------------------------------------------------------------
    // IsTransientException - classifier matrix
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public void IsTransientException_KnownTransientHttpStatuses_AreTransient(HttpStatusCode status)
    {
        var ex = new HttpRequestException("transient", inner: null, statusCode: status);

        RetryHelper.IsTransientException(ex).Should().BeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    public void IsTransientException_NonTransientHttpStatuses_AreNotTransient(
        HttpStatusCode status)
    {
        var ex = new HttpRequestException("non-transient", inner: null, statusCode: status);

        RetryHelper.IsTransientException(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransientException_HttpRequestExceptionWithNullStatus_IsNotTransient()
    {
        var ex = new HttpRequestException("no status");

        RetryHelper.IsTransientException(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransientException_TaskCanceledException_IsTransient()
    {
        RetryHelper.IsTransientException(new TaskCanceledException()).Should().BeTrue();
    }

    [Fact]
    public void IsTransientException_TimeoutException_IsTransient()
    {
        RetryHelper.IsTransientException(new TimeoutException()).Should().BeTrue();
    }

    [Fact]
    public void IsTransientException_SocketException_IsTransient()
    {
        RetryHelper.IsTransientException(
            new SocketException((int)SocketError.ConnectionRefused))
            .Should().BeTrue();
    }

    [Fact]
    public void IsTransientException_CircuitOpenException_IsTransient()
    {
        // The retry-OUTSIDE-CB composition depends on this: a retry layer around a
        // CB must retry on CO (CircuitOpenException) and back off between attempts,
        // so an upstream restart can finish while the breaker cools down.
        RetryHelper.IsTransientException(new CircuitOpenException()).Should().BeTrue();
    }

    [Fact]
    public void IsTransientException_OtherException_IsNotTransient()
    {
        RetryHelper.IsTransientException(
            new InvalidOperationException("nope"))
            .Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // RetryAsync - happy path + exhaustion
    // ----------------------------------------------------------------------

    [Fact]
    public async Task RetryAsync_NullOptions_AppliesDefaults()
    {
        // Covers the `options ??= new RetryOptions<T>()` branch, which runs
        // when the caller passes null.
        var result = await RetryHelper.RetryAsync(
            (_, _) => ValueTask.FromResult(7),
            options: null,
            CancellationToken.None);

        result.Should().Be(7);
    }

    [Fact]
    public async Task RetryAsync_SuccessOnFirstAttempt_ReturnsImmediately()
    {
        var attemptsObserved = 0;

        var result = await RetryHelper.RetryAsync(
            (attempt, _) =>
            {
                attemptsObserved = attempt;
                return ValueTask.FromResult(42);
            },
            options: NoDelayOptions(),
            CancellationToken.None);

        result.Should().Be(42);
        attemptsObserved.Should().Be(1);
    }

    [Fact]
    public async Task RetryAsync_ThrowsTransientThenSucceeds_RetriesAndReturns()
    {
        var attempts = 0;

        var result = await RetryHelper.RetryAsync(
            (attempt, _) =>
            {
                attempts = attempt;
                if (attempt < 3)
                {
                    throw new TimeoutException($"try {attempt}");
                }

                return ValueTask.FromResult(99);
            },
            options: NoDelayOptions(maxAttempts: 5),
            CancellationToken.None);

        result.Should().Be(99);
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task RetryAsync_NonTransientException_ReThrowsImmediately()
    {
        var attempts = 0;

        var act = async () =>
        {
            await RetryHelper.RetryAsync(
                (attempt, _) =>
                {
                    attempts = attempt;
                    throw new InvalidOperationException("permanent");
                },
                options: NoDelayOptions(maxAttempts: 5),
                CancellationToken.None);
        };

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("permanent");
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task RetryAsync_TransientThrownEveryAttempt_ThrowsAfterExhaustion()
    {
        var attempts = 0;

        var act = async () =>
        {
            await RetryHelper.RetryAsync(
                (attempt, _) =>
                {
                    attempts = attempt;
                    throw new TimeoutException();
                },
                options: NoDelayOptions(maxAttempts: 3),
                CancellationToken.None);
        };

        await act.Should().ThrowAsync<TimeoutException>();
        attempts.Should().Be(3);
    }

    // ----------------------------------------------------------------------
    // RetryAsync - value-failure (ShouldRetry) path
    // ----------------------------------------------------------------------

    [Fact]
    public async Task RetryAsync_ShouldRetryReturnsTrueThenFalse_RetriesUntilAccepted()
    {
        var attempts = 0;

        var options = NoDelayOptions(maxAttempts: 5) with
        {
            ShouldRetry = r => r < 3,
        };

        var result = await RetryHelper.RetryAsync(
            (attempt, _) =>
            {
                attempts = attempt;
                return ValueTask.FromResult(attempt);
            },
            options,
            CancellationToken.None);

        result.Should().Be(3);
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task RetryAsync_ShouldRetryAlwaysTrue_ReturnsLastValueAfterExhaustion()
    {
        // Adversarial: ShouldRetry always asks for a retry, but the attempts run out.
        // The last returned value is the result (the final attempt returned; it did not throw).
        var options = NoDelayOptions(maxAttempts: 3) with
        {
            ShouldRetry = _ => true,
        };

        var result = await RetryHelper.RetryAsync(
            (attempt, _) => ValueTask.FromResult(attempt),
            options,
            CancellationToken.None);

        result.Should().Be(3); // 3rd attempt returned 3
    }

    [Fact]
    public async Task RetryAsync_ReturnedThenThrew_ThrowsLastException()
    {
        // Adversarial: the attempts return values until the last one, which throws.
        // The exception from the last attempt is what RetryAsync throws when the attempts run out.
        var options = NoDelayOptions(maxAttempts: 4) with
        {
            ShouldRetry = _ => true,
        };

        var act = async () => await RetryHelper.RetryAsync(
            (attempt, _) => attempt switch
            {
                4 => throw new TimeoutException("final"),
                _ => ValueTask.FromResult(attempt),
            },
            options,
            CancellationToken.None);

        await act.Should().ThrowAsync<TimeoutException>().WithMessage("final");
    }

    // ----------------------------------------------------------------------
    // RetryAsync - cancellation
    // ----------------------------------------------------------------------

    [Fact]
    public async Task RetryAsync_PreCanceled_ThrowsImmediately()
    {
        // Copy the token into a local before the lambda, so the lambda does not
        // capture the IDisposable cts itself (R# AccessToDisposedClosure).
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var token = cts.Token;

        var act = async () =>
        {
            await RetryHelper.RetryAsync(
                (_, _) => ValueTask.FromResult(1),
                NoDelayOptions(),
                token);
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RetryAsync_OperationCanceledExceptionFromCt_PropagatesNotAsTransient()
    {
        // Adversarial: when ct is the SOURCE of cancellation, the catch
        // filter lets the OCE propagate as a cancellation and does not treat it
        // as a transient failure to retry. The test awaits directly in a
        // try/catch instead of using a ThrowAsync lambda, so the cts capture
        // stays inside the using scope and AccessToDisposedClosure does not apply.
        using var cts = new CancellationTokenSource();
        Exception? captured = null;

        try
        {
            await RetryHelper.RetryAsync(
                (_, ct) =>
                {
                    cts.Cancel();
                    ct.ThrowIfCancellationRequested();
                    return ValueTask.FromResult(1);
                },
                NoDelayOptions(),
                cts.Token);
        }
        catch (OperationCanceledException ex)
        {
            captured = ex;
        }

        captured.Should().NotBeNull();
    }

    [Fact]
    public async Task RetryAsync_CtCanceledOce_BypassesCatch_EvenWhenClassifierSaysTransient()
    {
        // Pin: the catch filter `ex is not OperationCanceledException ||
        // !ct.IsCancellationRequested` ends the retry loop, NOT the IsTransient
        // classifier. The test overrides IsTransient to ALWAYS return true. Without
        // the catch-filter exclusion, RetryAsync would retry the OCE, which is
        // wrong because the caller asked to cancel.
        using var cts = new CancellationTokenSource();
        var attempts = 0;
        Exception? captured = null;

        try
        {
            await RetryHelper.RetryAsync(
                (_, ct) =>
                {
                    attempts++;
                    cts.Cancel();
                    ct.ThrowIfCancellationRequested();
                    return ValueTask.FromResult(1);
                },
                NoDelayOptions() with { IsTransient = _ => true },
                cts.Token);
        }
        catch (OperationCanceledException ex)
        {
            captured = ex;
        }

        captured.Should().NotBeNull();
        attempts.Should().Be(1, "the catch filter must bail out, not the classifier");
    }

    // ----------------------------------------------------------------------
    // RetryAsync - DelayFunc invoked between attempts
    // ----------------------------------------------------------------------

    [Fact]
    public async Task RetryAsync_DelayFunc_InvokedBetweenRetries()
    {
        var delayInvocations = 0;
        var observedDelays = new List<TimeSpan>();
        var options = new RetryOptions<int>
        {
            MaxAttempts = 3,
            BaseDelayMs = 10,
            BackoffMultiplier = 2.0,
            MaxDelayMs = 100,
            Jitter = false,
            DelayFunc = (delay, _) =>
            {
                Interlocked.Increment(ref delayInvocations);
                observedDelays.Add(delay);
                return Task.CompletedTask;
            },
        };

        try
        {
            await RetryHelper.RetryAsync(
                (_, _) => throw new TimeoutException(),
                options,
                CancellationToken.None);
        }
        catch (TimeoutException)
        {
            // Expected: RetryAsync throws when the attempts run out.
        }

        // 3 attempts -> 2 delays between them.
        delayInvocations.Should().Be(2);
        observedDelays.Should().HaveCount(2);
    }

    // ----------------------------------------------------------------------
    // RetryD2ResultAsync - D2Result-aware overload
    // ----------------------------------------------------------------------

    [Fact]
    public async Task RetryD2ResultAsync_DefaultPredicate_RetriesTransientRetryable()
    {
        var attempts = 0;

        var result = await RetryHelper.RetryD2ResultAsync(
            (attempt, _) =>
            {
                attempts = attempt;
                return attempt < 2
                    ? ValueTask.FromResult(D2Result<int>.ServiceUnavailable())
                    : ValueTask.FromResult(D2Result<int>.Ok(7));
            },
            options: NoDelayOptions<D2Result<int>>(maxAttempts: 5),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(7);
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task RetryD2ResultAsync_DefaultPredicate_DoesNotRetryNonTransientFailure()
    {
        // Adversarial: NotFound is a failure but NOT transient, so there are no retries.
        var attempts = 0;

        var result = await RetryHelper.RetryD2ResultAsync(
            (attempt, _) =>
            {
                attempts = attempt;
                return ValueTask.FromResult(D2Result<int>.NotFound());
            },
            options: NoDelayOptions<D2Result<int>>(maxAttempts: 5),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.IsNotFound.Should().BeTrue();
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task RetryD2ResultAsync_CallerShouldRetryOverride_TakesPrecedence()
    {
        var attempts = 0;

        var options = NoDelayOptions<D2Result<int>>(maxAttempts: 3) with
        {
            ShouldRetry = _ => false, // never retry, even if transient retryable
        };

        await RetryHelper.RetryD2ResultAsync(
            (attempt, _) =>
            {
                attempts = attempt;
                return ValueTask.FromResult(D2Result<int>.ServiceUnavailable());
            },
            options,
            CancellationToken.None);

        attempts.Should().Be(1);
    }

    [Fact]
    public async Task RetryD2ResultAsync_NullOptions_StillUsesDefaultPredicate()
    {
        // Adversarial: the options leave ShouldRetry at its default, so
        // RetryD2ResultAsync substitutes the D2Result-aware predicate
        // (RetryHelper.cs): a transient failure is retried and a non-transient
        // one is not. Two attempts, no delay.
        var transientAttempts = await CountAttemptsAsync(D2Result<int>.ServiceUnavailable());
        var permanentAttempts = await CountAttemptsAsync(D2Result<int>.NotFound());

        (transientAttempts, permanentAttempts).Should().Be((2, 1));

        static async Task<int> CountAttemptsAsync(D2Result<int> outcome)
        {
            var attempts = 0;
            await RetryHelper.RetryD2ResultAsync(
                (_, _) =>
                {
                    attempts++;
                    return ValueTask.FromResult(outcome);
                },
                new RetryOptions<D2Result<int>>
                {
                    MaxAttempts = 2,
                    BaseDelayMs = 0,
                    DelayFunc = (_, _) => Task.CompletedTask,
                },
                CancellationToken.None);
            return attempts;
        }
    }

    [Fact]
    public async Task RetryD2ResultAsync_DefaultOptions_AppliesPredicate()
    {
        // Exercises the `options is null -> defaults applied` branch in the helper.
        var result = await RetryHelper.RetryD2ResultAsync(
            (_, _) => ValueTask.FromResult(D2Result<int>.Ok(1)),
            options: null,
            CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // CalculateDelay - internal math
    // ----------------------------------------------------------------------

    [Fact]
    public void CalculateDelay_FirstRetry_NoJitter_EqualsBaseDelay()
    {
        var d = RetryHelper.CalculateDelay(
            retryIndex: 0,
            baseDelayMs: 100,
            backoffMultiplier: 2.0,
            maxDelayMs: 10_000,
            jitter: false);

        d.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void CalculateDelay_NoJitter_AppliesBackoffMultiplier()
    {
        var d = RetryHelper.CalculateDelay(
            retryIndex: 3,
            baseDelayMs: 100,
            backoffMultiplier: 2.0,
            maxDelayMs: 10_000,
            jitter: false);

        // 100 * 2^3 = 800ms
        d.Should().Be(TimeSpan.FromMilliseconds(800));
    }

    [Fact]
    public void CalculateDelay_NoJitter_ClampsToMaxDelay()
    {
        var d = RetryHelper.CalculateDelay(
            retryIndex: 20,
            baseDelayMs: 100,
            backoffMultiplier: 2.0,
            maxDelayMs: 5_000,
            jitter: false);

        d.Should().Be(TimeSpan.FromMilliseconds(5_000));
    }

    [Fact]
    public void CalculateDelay_LargeRetryIndex_ClampsExponentSafely()
    {
        // Adversarial: an int.MaxValue retryIndex would overflow Math.Pow and
        // produce +Infinity. The helper clamps the exponent to 63, and then
        // Math.Min(+Inf, max) -> max, so the result is always a valid TimeSpan.
        var d = RetryHelper.CalculateDelay(
            retryIndex: int.MaxValue,
            baseDelayMs: 100,
            backoffMultiplier: 2.0,
            maxDelayMs: 5_000,
            jitter: false);

        d.Should().Be(TimeSpan.FromMilliseconds(5_000));
    }

    // ----------------------------------------------------------------------
    // Boundary + adversarial edge cases
    // ----------------------------------------------------------------------

    [Fact]
    public async Task RetryAsync_MaxAttemptsZero_ExecutesOnce_ThenPropagates()
    {
        // Boundary: with MaxAttempts=0, `attempt < MaxAttempts` is false on
        // attempt 1 (attempt starts at 0 and increments to 1), so the
        // first attempt's outcome is the FINAL outcome and the loop never takes
        // the continue path. The behavior is identical to MaxAttempts=1.
        var attempts = 0;
        var act = async () => await RetryHelper.RetryAsync(
            (_, _) =>
            {
                Interlocked.Increment(ref attempts);
                throw new TimeoutException();
            },
            options: NoDelayOptions(maxAttempts: 0),
            CancellationToken.None);

        await act.Should().ThrowAsync<TimeoutException>();
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task RetryAsync_DelayFuncThrows_ExceptionPropagates()
    {
        // Adversarial: an exception thrown by DelayFunc (for example a test fake
        // that throws on a particular delay value, or a DelayFunc with an unrelated
        // bug) must propagate out of RetryAsync. RetryAsync does not suppress
        // exceptions from the delegate.
        var attempts = 0;
        var options = new RetryOptions<int>
        {
            MaxAttempts = 5,
            BaseDelayMs = 100,
            BackoffMultiplier = 1.0,
            MaxDelayMs = 100,
            Jitter = false,
            DelayFunc = (_, _) => throw new InvalidOperationException("delay func boom"),
        };

        var act = async () => await RetryHelper.RetryAsync(
            (_, _) =>
            {
                Interlocked.Increment(ref attempts);
                throw new TimeoutException("transient");
            },
            options,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("delay func boom");

        // Only the FIRST attempt ran; the exception from DelayFunc ended the retry loop.
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task RetryAsync_CtCanceledDuringDelayFunc_PropagatesOce()
    {
        // Adversarial: the caller cancels the CT during the backoff. DelayFunc
        // (Task.Delay or an override) honors the CT, and the OCE escapes RetryAsync
        // because the catch filter excludes "OCE when ct is canceled."
        //
        // The finally block disposes cts AFTER `act` is awaited, instead of a
        // `using` declaration, so the lambda that captures cts has finished
        // before the dispose.
        var cts = new CancellationTokenSource();
        try
        {
            // ReSharper disable AccessToDisposedClosure -- await act below
            // completes the lambda before the finally Dispose, which R# can't
            // prove statically.
            var attempts = 0;
            var options = new RetryOptions<int>
            {
                MaxAttempts = 5,
                BaseDelayMs = 0,
                MaxDelayMs = 0,
                Jitter = false,
                DelayFunc = async (_, ct) =>
                {
                    await cts.CancelAsync();
                    ct.ThrowIfCancellationRequested();
                },
            };

            var act = async () => await RetryHelper.RetryAsync(
                (_, _) =>
                {
                    Interlocked.Increment(ref attempts);
                    throw new TimeoutException("transient");
                },
                options,
                cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();

            // ReSharper restore AccessToDisposedClosure
            // The first attempt threw a transient exception, DelayFunc canceled
            // during the backoff, and the OCE escaped before the second attempt began.
            attempts.Should().Be(1);
        }
        finally
        {
            cts.Dispose();
        }
    }

    [Fact]
    public void CalculateDelay_WithJitter_StaysWithinZeroToCalculated()
    {
        // Property: a jittered delay is always in [0, calculated). The loop draws
        // many samples, because a single draw could pass by chance.
        const int retry_index = 2;
        const int base_delay_ms = 100;
        const double backoff = 2.0;
        const int max_delay_ms = 10_000;
        const double calculated_max_ms = base_delay_ms * (backoff * backoff); // 400

        for (var i = 0; i < 200; i++)
        {
            var d = RetryHelper.CalculateDelay(
                retry_index,
                base_delay_ms,
                backoff,
                max_delay_ms,
                jitter: true);

            d.TotalMilliseconds.Should().BeInRange(0, calculated_max_ms);
        }
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static RetryOptions<T> NoDelayOptions<T>(int maxAttempts = 3)
    {
        return new RetryOptions<T>
        {
            MaxAttempts = maxAttempts,
            BaseDelayMs = 0,
            BackoffMultiplier = 1.0,
            MaxDelayMs = 0,
            Jitter = false,
            DelayFunc = (_, _) => Task.CompletedTask,
        };
    }

    private static RetryOptions<int> NoDelayOptions(int maxAttempts = 3)
        => NoDelayOptions<int>(maxAttempts);
}
