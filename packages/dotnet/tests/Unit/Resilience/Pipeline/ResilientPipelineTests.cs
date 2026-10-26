// -----------------------------------------------------------------------
// <copyright file="ResilientPipelineTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.Pipeline;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.CircuitBreaker;
using DcsvIo.D2.Resilience.Pipeline;
using DcsvIo.D2.Resilience.RateLimiting;
using Xunit;
using SingleflightT = DcsvIo.D2.Resilience.Singleflight.Singleflight<string, int>;

public sealed class ResilientPipelineTests
{
    // ----------------------------------------------------------------------
    // Layer composition + ordering
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_NoLayers_RunsOperationDirectly()
    {
        var pipeline = new ResilientPipeline<string, int>();

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(42));

        result.Success.Should().BeTrue();
        result.Data.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_LayersInvokedInOuterFirstOrder()
    {
        // Adversarial: layers wrap each other outer-first. A shared list records
        // each layer's enter and exit order. The outer layer encloses the inner
        // layer, which encloses the op.
        var trace = new List<string>();
        var pipeline = new ResilientPipeline<string, int>(
            new TracingLayer("outer", trace),
            new TracingLayer("inner", trace));

        await pipeline.ExecuteAsync("k", _ =>
        {
            trace.Add("op");
            return ValueTask.FromResult(1);
        });

        trace.Should().Equal("outer-enter", "inner-enter", "op", "inner-exit", "outer-exit");
    }

    // ----------------------------------------------------------------------
    // Exception -> D2Result mapping
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_OperationSucceeds_ReturnsOkWithData()
    {
        var pipeline = new ResilientPipeline<string, int>();

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(7));

        result.Success.Should().BeTrue();
        result.Data.Should().Be(7);
    }

    [Fact]
    public async Task ExecuteAsync_CircuitOpenException_MapsToServiceUnavailable()
    {
        var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 1));

        // Open the breaker.
        try
        {
            await cb.ExecuteAsync(_ => throw new InvalidOperationException());
        }
        catch (InvalidOperationException)
        {
            // Expected: this failure opens the breaker.
        }

        var pipeline = new ResilientPipeline<string, int>(
            new CircuitBreakerLayer<string, int>(cb));

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(1));

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_OperationCanceled_MapsToCanceled()
    {
        // Caller-canceled token: the OCE comes from `ct`, so the pipeline
        // maps it to Canceled (not UnhandledException).
        using var cts = new CancellationTokenSource();
        var pipeline = new ResilientPipeline<string, int>();

        var result = await pipeline.ExecuteAsync(
            "k",
            ct =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return ValueTask.FromResult(1);
            },
            cts.Token);

        result.Success.Should().BeFalse();
        result.IsCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_TransientExceptionSlipsThrough_MapsToServiceUnavailable()
    {
        // No Retry layer is configured and the op throws a transient exception
        // (TimeoutException). The IsTransientException catch clause catches it and
        // converts it to ServiceUnavailable. This covers the branch for a
        // transient exception that no layer handled.
        var pipeline = new ResilientPipeline<string, int>();

        var result = await pipeline.ExecuteAsync(
            "k",
            _ => throw new TimeoutException("upstream slow"));

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_UnknownException_MapsToUnhandledException()
    {
        // Non-transient, non-CB, non-cancellation exception -> caught by the
        // final catch-all and converted to UnhandledException.
        var pipeline = new ResilientPipeline<string, int>();

        var result = await pipeline.ExecuteAsync(
            "k",
            _ => throw new InvalidOperationException("programmer error"));

        result.Success.Should().BeFalse();
        result.IsUnhandledException.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_OceWithoutCanceledToken_MapsToUnhandledException()
    {
        // Adversarial: the operation throws an OCE although the supplied ct
        // was NOT canceled. The `when` filter does not match, so the OCE
        // reaches the catch-all -> UnhandledException.
        var pipeline = new ResilientPipeline<string, int>();

        var result = await pipeline.ExecuteAsync(
            "k",
            _ => throw new OperationCanceledException("not from our ct"));

        result.Success.Should().BeFalse();
        result.IsUnhandledException.Should().BeTrue();
    }

    [Fact]
    public async Task
    ExecuteAsync_TaskCanceledExceptionWithoutCanceledToken_MapsToServiceUnavailable()
    {
        // Pins the DIVERGENT classification: `TaskCanceledException` IS in
        // `IsTransientException`'s switch arms (RetryHelper.cs),
        // unlike its base `OperationCanceledException`. So a
        // `TaskCanceledException` that does not come from `ct` skips the
        // OCE-when-ct catch and reaches the IsTransientException catch
        // instead -> ServiceUnavailable.
        // The case matters because many HttpClient timeouts inside a
        // downstream call surface as a TCE that does not come from `ct`.
        var pipeline = new ResilientPipeline<string, int>();

        var result = await pipeline.ExecuteAsync(
            "k",
            _ => throw new TaskCanceledException("inner timeout, not our ct"));

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();
        result.IsUnhandledException.Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // Realistic full-stack composition
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_FullStack_RetriesThroughCircuitBreaker_ReturnsOk()
    {
        // Composition: CB -> Retry. The operation throws a transient exception on
        // attempts 1 and 2 and succeeds on attempt 3. The pipeline returns Ok
        // with the result.
        var pipeline = new ResilientPipeline<string, int>(
            new CircuitBreakerLayer<string, int>(
                new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 100))),
            NoDelayOptions(maxAttempts: 5));

        var attempts = 0;
        var result = await pipeline.ExecuteAsync("k", _ =>
        {
            Interlocked.Increment(ref attempts);
            if (attempts < 3)
            {
                throw new TimeoutException();
            }

            return ValueTask.FromResult(42);
        });

        result.Success.Should().BeTrue();
        result.Data.Should().Be(42);
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_RetryOutsideCb_RecoversAcrossSimulatedUpstreamRestart()
    {
        // The "service restart" scenario, with retry OUTSIDE the CB:
        //   retry -> CB -> upstream
        // Sequence:
        //   1. The operation throws on attempt 1, the CB threshold is reached
        //      and the CB opens.
        //   2. The retry backoff runs a fake DelayFunc that advances the
        //      fake clock past the CB cooldown.
        //   3. Attempt 2 finds the CB Half-Open and probes the upstream. The
        //      operation succeeds and the CB closes.
        // The pipeline returns Ok. This shows that the retry layer's default
        // classifier treats CircuitOpenException as transient.
        var clock = new FakeClock();
        var cb = new CircuitBreaker<int>(
            isFailure: _ => false,
            options: new(
                failureThreshold: 1,
                cooldownDuration: TimeSpan.FromSeconds(1),
                nowFunc: clock.Now));

        var attempts = 0;

        // Layer order: Retry OUTER, CircuitBreaker INNER.
        var pipeline = new ResilientPipeline<string, int>(
            new RetryLayer<string, int>(new()
            {
                MaxAttempts = 3,
                BaseDelayMs = 0,
                MaxDelayMs = 0,
                Jitter = false,
                DelayFunc = (_, _) =>
                {
                    // Each retry wait advances the fake clock past the cooldown,
                    // so the CB is Half-Open by the next attempt.
                    clock.Advance(TimeSpan.FromSeconds(2));
                    return Task.CompletedTask;
                },
            }),
            new CircuitBreakerLayer<string, int>(cb));

        var result = await pipeline.ExecuteAsync("k", _ =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new TimeoutException("upstream is restarting");
            }

            return ValueTask.FromResult(42);
        });

        result.Success.Should().BeTrue();
        result.Data.Should().Be(42);
        attempts.Should().Be(2);
        cb.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task ExecuteAsync_HighConcurrency_FullStack_AllSeeSameDedupedResult()
    {
        // End-to-end stress: 100 concurrent callers through SF + CB + Retry,
        // same key; the operation fails transiently twice, then succeeds. Checks:
        //  - the operation runs once per retry attempt, 3 times in total: SF
        //    runs the retry sequence once and every caller awaits one shared task
        //  - every caller receives the same Ok(7) result
        //  - CB stays Closed (one logical execution = one CB call, which succeeded)
        //
        // The FIRST attempt awaits a gate so the SF in-flight window stays open
        // until every caller has joined it. Without the gate the whole
        // pipeline runs synchronously on the first caller, SF removes the key
        // before late callers arrive, and each of them starts a fresh sequence
        // (observable as attempts >> 3). The test raises the minimum thread-pool
        // size because the default growth rate cannot supply enough ready workers
        // for 100 concurrent callers in time.
        ThreadPool.GetMinThreads(out var origWorker, out var origIo);
        ThreadPool.SetMinThreads(Math.Max(origWorker, 200), Math.Max(origIo, 200));
        try
        {
            var sf = new SingleflightT();
            var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 100));
            var pipeline = new ResilientPipeline<string, int>(
                new SingleflightLayer<string, int>(sf),
                new CircuitBreakerLayer<string, int>(cb),
                NoDelayOptions(maxAttempts: 5));

            var attempts = 0;
            var gate = new TaskCompletionSource();
            const int concurrent_callers = 100;

            // ResilientPipeline.ExecuteAsync is async, but its first synchronous
            // path builds the layer chain and calls wrapped(ct), which invokes
            // SingleflightLayer.WrapAsync -> Singleflight.ExecuteAsync
            // -> r_inflight.GetOrAdd synchronously, all before the first await.
            // Capturing the ValueTask<D2Result<int>> (without awaiting) therefore
            // shows the caller has joined the singleflight entry. Releasing the
            // semaphore AFTER the capture means that waiting for all N releases
            // guarantees every caller has joined before the gate opens, with no
            // SpinWait or Yield.
            var allJoined = new SemaphoreSlim(0, concurrent_callers);

            var tasks = Enumerable.Range(0, concurrent_callers)
                .Select(_ => Task.Run(async () =>
                {
                    var vt = pipeline.ExecuteAsync("k", async _ =>
                    {
                        var n = Interlocked.Increment(ref attempts);

                        if (n == 1)
                        {
                            // Hold the in-flight task open until every caller joined it.
                            await gate.Task;
                        }

                        if (n < 3)
                            throw new TimeoutException();

                        return 7;
                    });
                    allJoined.Release(); // release AFTER the join, not before
                    return await vt;
                }))
                .ToArray();

            // Wait for every caller to join. The wait is on the semaphore, not a poll.
            for (var i = 0; i < concurrent_callers; i++)
                await allJoined.WaitAsync();

            // Every caller has joined the single in-flight entry.
            sf.Size.Should().Be(1);
            gate.SetResult();

            var results = await Task.WhenAll(tasks);

            attempts.Should().Be(3);                   // SF dedupes: only one retry sequence ran
            results.Should().AllSatisfy(r =>
            {
                r.Success.Should().BeTrue();
                r.Data.Should().Be(7);                 // every caller got the same result
            });
            cb.State.Should().Be(CircuitState.Closed); // CB recorded one successful execution
        }
        finally
        {
            ThreadPool.SetMinThreads(origWorker, origIo);
        }
    }

    [Fact]
    public async Task
        ExecuteAsync_SingleflightInPipeline_OperationFails_AllConcurrentCallersGetSameFailure()
    {
        // Adversarial: when SF deduplicates a failing operation, EVERY waiter
        // receives the same exception and so the same D2Result mapping. No caller
        // gets a different outcome (some Ok, some failure) for the shared run.
        var pipeline = new ResilientPipeline<string, int>(
            new SingleflightLayer<string, int>(new SingleflightT()));

        const int concurrent_callers = 50;
        var barrier = new Barrier(concurrent_callers);
        try
        {
            // ReSharper disable AccessToDisposedClosure -- await Task.WhenAll
            // synchronizes all closures before the finally Dispose, which R#
            // can't prove statically.
            var tasks = Enumerable.Range(0, concurrent_callers).Select(_ => Task.Run(async () =>
            {
                barrier.SignalAndWait();
                return await pipeline.ExecuteAsync(
                    "k",
                    _ => throw new InvalidOperationException("upstream broke"));
            })).ToArray();

            var results = await Task.WhenAll(tasks);

            // ReSharper restore AccessToDisposedClosure
            results.Should().AllSatisfy(r =>
            {
                r.Success.Should().BeFalse();
                r.IsUnhandledException.Should().BeTrue();
            });
        }
        finally
        {
            barrier.Dispose();
        }
    }

    [Fact]
    public async Task ExecuteAsync_RetryOutsideCb_ExhaustsBeforeCooldown_ReturnsServiceUnavailable()
    {
        // Adversarial counterpart to the recovery test: the retry budget ends
        // BEFORE the CB cooldown does, so every retry attempt finds the
        // breaker still open. CO (CircuitOpenException) is transient, so the
        // retry loop runs until the attempts run out, and the final CO reaches
        // the pipeline, which maps it to ServiceUnavailable.
        var clock = new FakeClock();
        var cb = new CircuitBreaker<int>(
            isFailure: _ => false,
            options: new(
                failureThreshold: 1,
                cooldownDuration: TimeSpan.FromSeconds(60),
                nowFunc: clock.Now));

        // Open the CB BEFORE running through the pipeline, so the
        // pipeline starts with a breaker that is already open.
        try
        {
            await cb.ExecuteAsync(_ => throw new InvalidOperationException());
        }
        catch (InvalidOperationException)
        {
            // Expected: this failure opens the circuit.
        }

        cb.State.Should().Be(CircuitState.Open);

        var pipeline = new ResilientPipeline<string, int>(
            NoDelayOptions(maxAttempts: 3),
            new CircuitBreakerLayer<string, int>(cb));

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(1));

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // Mappings for TimeoutException and RateLimitRejectedException
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_TimeoutException_MapsToServiceUnavailable()
    {
        // TimeoutException is classified transient by IsTransientException ->
        // maps to ServiceUnavailable (503, IsTransientRetryable=true).
        var pipeline = new ResilientPipeline<string, int>();

        var result = await pipeline.ExecuteAsync("k", _ => throw new TimeoutException("slow"));

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();
        result.IsTransientRetryable.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_RateLimitRejectedException_MapsToTooManyRequests()
    {
        // Covers the RateLimitRejectedException -> TooManyRequests catch arm.
        var pipeline = new ResilientPipeline<string, int>();

        var result = await pipeline.ExecuteAsync("k", _ => throw new RateLimitRejectedException());

        result.Success.Should().BeFalse();
        result.IsRateLimited.Should().BeTrue();
        result.IsTransientRetryable.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // PassThrough sentinel
    // ----------------------------------------------------------------------

    [Fact]
    public async Task PassThrough_IsZeroLayerPipeline_MapsExceptionsToD2Result()
    {
        // PassThrough maps exceptions to D2Result because it IS a pipeline with no layers.
        var result = await ResilientPipeline<string, int>.PassThrough.ExecuteAsync(
            "k",
            _ => throw new InvalidOperationException("programmer error"));

        result.Success.Should().BeFalse();
        result.IsUnhandledException.Should().BeTrue();
    }

    [Fact]
    public async Task PassThrough_SuccessfulOp_ReturnsOk()
    {
        var result = await ResilientPipeline<string, int>.PassThrough.ExecuteAsync(
            "k",
            _ => ValueTask.FromResult(99));

        result.Success.Should().BeTrue();
        result.Data.Should().Be(99);
    }

    [Fact]
    public void PassThrough_IsSingletonInstance()
    {
        var a = ResilientPipeline<string, int>.PassThrough;
        var b = ResilientPipeline<string, int>.PassThrough;

        a.Should().BeSameAs(b);
    }

    [Fact]
    public async Task PassThrough_DoesNotRetry_OperationCalledOnce()
    {
        // With no layers there is no retry, even if the exception is transient.
        var calls = 0;

        var result = await ResilientPipeline<string, int>.PassThrough.ExecuteAsync("k", _ =>
        {
            Interlocked.Increment(ref calls);
            throw new TimeoutException();
        });

        result.IsServiceUnavailable.Should().BeTrue();
        calls.Should().Be(1);
    }

    // ----------------------------------------------------------------------
    // Canonical full-stack ordering: RateLimiter -> Timeout(total) -> Retry -> CB ->
    // Timeout(per-attempt)
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_CanonicalFullStack_LayersAppliedInOrder()
    {
        // Verifies outer-first ordering: each TracingLayer stands for one layer type.
        // The canonical order: RateLimiter -> TotalTimeout -> Retry -> CB -> PerAttemptTimeout.
        var trace = new List<string>();
        var pipeline = new ResilientPipeline<string, int>(
            new TracingLayer("rate-limiter", trace),
            new TracingLayer("total-timeout", trace),
            new TracingLayer("retry", trace),
            new TracingLayer("cb", trace),
            new TracingLayer("per-attempt-timeout", trace));

        await pipeline.ExecuteAsync("k", _ =>
        {
            trace.Add("op");
            return ValueTask.FromResult(1);
        });

        trace.Should().Equal(
            "rate-limiter-enter",
            "total-timeout-enter",
            "retry-enter",
            "cb-enter",
            "per-attempt-timeout-enter",
            "op",
            "per-attempt-timeout-exit",
            "cb-exit",
            "retry-exit",
            "total-timeout-exit",
            "rate-limiter-exit");
    }

    private static RetryLayer<string, int> NoDelayOptions(int maxAttempts = 3)
        => new(new()
        {
            MaxAttempts = maxAttempts,
            BaseDelayMs = 0,
            MaxDelayMs = 0,
            Jitter = false,
            DelayFunc = (_, _) => Task.CompletedTask,
        });

    private sealed class FakeClock
    {
        private long _now;

        public long Now() => Volatile.Read(ref _now);

        public void Advance(TimeSpan delta)
            => Interlocked.Add(ref _now, (long)delta.TotalMilliseconds);
    }

    /// <summary>
    /// Test-only layer that records enter/exit markers in a shared list so
    /// composition order can be asserted.
    /// </summary>
    private sealed class TracingLayer(string name, List<string> trace)
        : IResilientLayer<string, int>
    {
        public async ValueTask<int> WrapAsync(
            string key,
            Func<CancellationToken, ValueTask<int>> next,
            CancellationToken ct)
        {
            trace.Add($"{name}-enter");
            try
            {
                return await next(ct);
            }
            finally
            {
                trace.Add($"{name}-exit");
            }
        }
    }
}
