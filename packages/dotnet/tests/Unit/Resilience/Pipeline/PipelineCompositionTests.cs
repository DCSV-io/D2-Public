// -----------------------------------------------------------------------
// <copyright file="PipelineCompositionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.Pipeline;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.CircuitBreaker;
using DcsvIo.D2.Resilience.Pipeline;
using DcsvIo.D2.Resilience.RateLimiting;
using DcsvIo.D2.Resilience.Timeout;
using Xunit;
using SingleflightT = DcsvIo.D2.Resilience.Singleflight.Singleflight<string, int>;

/// <summary>
/// Adversarial nesting tests that combine the pipeline layers in varied
/// orderings. The tests assert the effect of the order, not only that the
/// pipeline returns a result: each layer's semantics depend on where it sits.
/// <para>
/// Layers under test and their abbreviations:
/// SF = Singleflight (outermost when used),
/// RL = RateLimiter, Tt = Total-Timeout, R = Retry,
/// CB = CircuitBreaker, Ta = Per-Attempt-Timeout.
/// </para>
/// </summary>
public sealed class PipelineCompositionTests
{
    // ----------------------------------------------------------------
    // 1. Canonical full stack: RL -> Tt -> R -> CB -> Ta
    //    Flaky op (throws a transient exception, then succeeds) -> succeeds via R.
    //    Permanently down op -> CB opens, R runs out of attempts -> ServiceUnavailable.
    // ----------------------------------------------------------------

    [Fact]
    public async Task FullStack_RL_Tt_R_CB_Ta_FlakyOp_RecoversViaRetry()
    {
        // RL -> Tt -> R -> CB -> Ta. The op throws a transient exception twice, then succeeds.
        // Assertions: Success=true, call-count == 3 (1 initial + 2 retries),
        // CB stays Closed (each attempt is a separate CB call; 2 failures
        // raise its failure count to 2, which is below threshold=10).
        var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 10, nowFunc: static () => 0L));
        using var pipeline = new ResilientPipeline<string, int>(
            new RateLimiterLayer<string, int>(new RateLimiterOptions(maxConcurrency: 10)),
            new TimeoutLayer<string, int>(new TimeoutOptions(TimeSpan.FromSeconds(30))),
            NoDelayOptions(maxAttempts: 5),
            new CircuitBreakerLayer<string, int>(cb),
            new TimeoutLayer<string, int>(new TimeoutOptions(TimeSpan.FromSeconds(5))));

        var calls = 0;
        var result = await pipeline.ExecuteAsync("k", _ =>
        {
            var n = Interlocked.Increment(ref calls);
            if (n < 3)
                throw new TimeoutException("slow upstream");
            return ValueTask.FromResult(42);
        });

        result.Success.Should().BeTrue();
        result.Data.Should().Be(42);
        calls.Should().Be(3);
        cb.State.Should().Be(CircuitState.Closed);

        // CB ran 3 separate executions; 2 failures stay below threshold=10, and
        // the final success resets the failure count to 0.
        cb.FailureCount.Should().Be(0);
    }

    [Fact]
    public async Task FullStack_RL_Tt_R_CB_Ta_PermanentlyDown_CbTrips_RExhausts_ServiceUnavailable()
    {
        // CB threshold=2; Retry budget=4. Op always throws TimeoutException (transient).
        // CB opens after 2 real-op failures; the 3rd and 4th attempts find CB
        // open and get CircuitOpenException (also transient). R retries, CB
        // remains open, and the attempts run out. The last exception is
        // CircuitOpenException, which the pipeline boundary maps to ServiceUnavailable.
        var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 2, nowFunc: static () => 0L));
        using var pipeline = new ResilientPipeline<string, int>(
            new RateLimiterLayer<string, int>(new RateLimiterOptions(maxConcurrency: 10)),
            new TimeoutLayer<string, int>(new TimeoutOptions(TimeSpan.Zero)),
            NoDelayOptions(maxAttempts: 4),
            new CircuitBreakerLayer<string, int>(cb),
            new TimeoutLayer<string, int>(new TimeoutOptions(TimeSpan.Zero)));

        var realOpCalls = 0;
        var result = await pipeline.ExecuteAsync("k", _ =>
        {
            Interlocked.Increment(ref realOpCalls);

            // TimeoutException is transient, so R retries it; CB also records each failure.
            throw new TimeoutException("permanently slow");
        });

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();

        // CB is open: 2 real failures reached the threshold.
        cb.State.Should().Be(CircuitState.Open);

        // The real op ran exactly 2 times. After CB opened, later retries got
        // CircuitOpenException without reaching the real op.
        realOpCalls.Should().Be(2);
    }

    // ----------------------------------------------------------------
    // 2. CB<->R order sensitivity: the order changes the result.
    //
    //    R outside CB  (R -> CB):  each attempt is a separate CB execution.
    //    CB outside R  (CB -> R):  one full retry sequence = one CB execution.
    //
    //    CO = CircuitOpenException, SU = ServiceUnavailable.
    //    With threshold=1 (R -> CB uses a 4-attempt retry budget, CB -> R uses 3):
    //    - R->CB:  the very first failure opens CB; subsequent retries get CO
    //             (transient) -> retry keeps going -> attempts run out -> SU.
    //    - CB->R:  the inner R retries twice more, all within one CB execution;
    //             CB counts them as a single sequence and counts 0 successes
    //             + 1 sequence failure -> CB opens only after the full sequence
    //             fails. The final exception leaves R and maps to SU.
    //
    //    The difference to observe: how many times the real op runs
    //    before the pipeline returns ServiceUnavailable.
    // ----------------------------------------------------------------

    [Fact]
    public async Task RetryOutsideCb_EachAttemptIsSeparateCbExecution_CbOpensEarly()
    {
        // R -> CB (Retry outermost, CB innermost).
        // Op always throws. CB threshold=1, so CB opens on the very first real-op
        // failure. Subsequent retries get CircuitOpenException (transient)
        // without reaching the real op at all.
        // Real op call-count == 1; every later retry gets CO.
        var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 1, nowFunc: static () => 0L));
        using var pipeline = new ResilientPipeline<string, int>(
            NoDelayOptions(maxAttempts: 4),
            new CircuitBreakerLayer<string, int>(cb));

        var realOpCalls = 0;
        var result = await pipeline.ExecuteAsync("k", _ =>
        {
            Interlocked.Increment(ref realOpCalls);

            // TimeoutException is transient, so R retries it; CB records each as a failure.
            // After threshold=1 failures, CB opens; subsequent retries get
            // CircuitOpenException (also transient). R retries, CB stays open
            // and the attempts run out -> ServiceUnavailable.
            throw new TimeoutException("down");
        });

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();

        // CB records one real-op execution and opens on its failure; the remaining
        // retry attempts never reach the real op.
        realOpCalls.Should().Be(1);
        cb.State.Should().Be(CircuitState.Open);
    }

    [Fact]
    public async Task CbOutsideRetry_FullRetryBudgetIsOneCbExecution_CbOpensAfterFullSequence()
    {
        // CB -> R (CB outermost, Retry innermost).
        // CB threshold=1, so CB opens only after ONE full CB-level execution fails.
        // Inner R budget=3 gives 3 real-op calls, all within that single CB execution.
        // CB opens AFTER the full retry sequence runs out of attempts, because CB
        // counts the sequence as one execution that threw.
        var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 1, nowFunc: static () => 0L));
        using var pipeline = new ResilientPipeline<string, int>(
            new CircuitBreakerLayer<string, int>(cb),
            NoDelayOptions(maxAttempts: 3));

        var realOpCalls = 0;
        var result = await pipeline.ExecuteAsync("k", _ =>
        {
            Interlocked.Increment(ref realOpCalls);

            // TimeoutException is transient, so the inner R retries every attempt;
            // the attempts run INSIDE CB, so CB counts them as ONE execution.
            // When R runs out of attempts, it rethrows TimeoutException (transient) to CB;
            // CB records 1 execution failure and opens, then rethrows, and the
            // pipeline boundary maps the exception to SU.
            throw new TimeoutException("down");
        });

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();

        // Every retry attempt reached the real op (they run INSIDE the CB): the
        // call count equals the retry budget of 3.
        realOpCalls.Should().Be(3);

        // CB opened only after the full sequence failed (it recorded 1 execution).
        cb.State.Should().Be(CircuitState.Open);
    }

    // ----------------------------------------------------------------
    // 3. Total-timeout OUTSIDE retry vs per-attempt-timeout INSIDE retry.
    //
    //    Tt -> R: total-timeout fires across the retry loop -> final SU.
    //    R -> Ta: a slow attempt times out -> R RETRIES it -> later success.
    // ----------------------------------------------------------------

    [Fact]
    public async Task TotalTimeoutOutsideRetry_FiresAcrossRetryLoop_TerminalServiceUnavailable()
    {
        // Tt -> R. Total-timeout fires before the retry loop completes.
        // TimeoutLayer converts the OCE -> TimeoutException when the timeout
        // fired and caller ct was NOT canceled. The pipeline's
        // IsTransientException arm catches TimeoutException -> ServiceUnavailable.
        // A short total budget (50 ms) fires during the first slow operation,
        // which shows that the total-timeout ends the retry loop.
        using var pipeline = new ResilientPipeline<string, int>(
            new TimeoutLayer<string, int>(new TimeoutOptions(TimeSpan.FromMilliseconds(50))),
            NoDelayOptions(maxAttempts: 5));

        var calls = 0;
        var result = await pipeline.ExecuteAsync("k", async ct =>
        {
            Interlocked.Increment(ref calls);

            // Infinite delay (-1 ms): the operation blocks until the timeout CTS fires.
            // A finite delay of 500 ms would race the timeout. -1 never completes on
            // its own, so the timeout always fires first, regardless of scheduler
            // pressure or CPU contention. `Timeout` is ambiguous here
            // (DcsvIo.D2.Tests.Unit.Resilience.Timeout namespace shadows System.Threading.Timeout),
            // so the literal -1 is used directly. It equals Timeout.Infinite.
            await Task.Delay(-1, ct);
            return 1;
        });

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();

        // Only 1 call: the total-timeout fires during the first attempt and ends
        // the retry loop before it can schedule a second attempt.
        calls.Should().Be(1);
    }

    [Fact]
    public async Task PerAttemptTimeoutInsideRetry_TimedOutAttemptIsRetried_EventualSuccess()
    {
        // R -> Ta. Per-attempt-timeout fires on the first slow attempt.
        // TimeoutLayer converts the OCE -> TimeoutException (transient).
        // The outer RetryLayer retries it (TimeoutException IS in IsTransientException).
        // The second attempt is fast and succeeds.
        // Call-count > 1 shows the retry happened after the per-attempt timeout.
        using var pipeline = new ResilientPipeline<string, int>(
            NoDelayOptions(maxAttempts: 3),
            new TimeoutLayer<string, int>(new TimeoutOptions(TimeSpan.FromMilliseconds(50))));

        var calls = 0;
        var result = await pipeline.ExecuteAsync("k", async ct =>
        {
            var n = Interlocked.Increment(ref calls);

            if (n == 1)
            {
                // Infinite delay (-1 ms): blocks until the per-attempt timeout CTS fires.
                // A finite delay of 500 ms would race the 50 ms timeout. -1 never
                // completes on its own, so the timeout always fires first, regardless
                // of scheduler jitter. `Timeout` is ambiguous here
                // (DcsvIo.D2.Tests.Unit.Resilience.Timeout namespace shadows System.Threading.Timeout),
                // so the literal -1 is used directly. It equals Timeout.Infinite.
                await Task.Delay(-1, ct);
            }

            return 42; // Second attempt returns immediately.
        });

        result.Success.Should().BeTrue();
        result.Data.Should().Be(42);

        // First attempt timed out and was retried; second attempt succeeded.
        calls.Should().Be(2);
    }

    // ----------------------------------------------------------------
    // 4. RateLimiter outermost rejects before the inner layers execute.
    //    RL (full) -> R -> CB: the rejected caller gets TooManyRequests;
    //    the inner op and CB never execute for the rejected call.
    // ----------------------------------------------------------------

    [Fact]
    public async Task RateLimiter_Outermost_GateFull_ShortCircuits_InnerLayersNeverExecute()
    {
        // RL (maxConcurrency=1) -> R -> CB. The first concurrent call holds the
        // gate. The second call cannot acquire a permit (zero acquisition timeout),
        // so RateLimitRejectedException maps to TooManyRequests.
        // The inner R+CB layers never execute for the rejected call.
        var cb = new CircuitBreaker<int>(
            _ => false,
            options: new(failureThreshold: 100));

        using var rateLimiter = new DcsvIo.D2.Resilience.RateLimiting.RateLimiter(
            new RateLimiterOptions(maxConcurrency: 1, acquisitionTimeout: TimeSpan.Zero));

        using var pipeline = new ResilientPipeline<string, int>(
            new RateLimiterLayer<string, int>(rateLimiter),
            NoDelayOptions(maxAttempts: 3),
            new CircuitBreakerLayer<string, int>(cb));

        var innerOpCalls = 0;
        var gate = new TaskCompletionSource();
        var acquired = new TaskCompletionSource(); // completes when the permit is held

        // Hold the gate with the first caller.
        var first = pipeline.ExecuteAsync("k", async _ =>
        {
            acquired.SetResult();
            await gate.Task;
            Interlocked.Increment(ref innerOpCalls);
            return 1;
        }).AsTask();

        // Wait until the permit is held before the rejected call.
        await acquired.Task;

        // Second caller: cannot acquire a permit, so it gets TooManyRequests immediately.
        var rejected = await pipeline.ExecuteAsync("k", _ =>
        {
            Interlocked.Increment(ref innerOpCalls);
            return ValueTask.FromResult(99);
        });

        gate.SetResult();
        await first;

        rejected.Success.Should().BeFalse();
        rejected.IsRateLimited.Should().BeTrue();

        // Only the first caller's op ran; the rejected call's inner op was never reached.
        innerOpCalls.Should().Be(1);
        cb.FailureCount.Should().Be(0); // CB recorded no failure for the rejection.
    }

    // ----------------------------------------------------------------
    // 5. RateLimiter rejection is NOT retried as a transient failure.
    //    R -> RL (RL inside Retry): RateLimitRejectedException is not in
    //    IsTransientException -> final TooManyRequests after ONE inner attempt.
    // ----------------------------------------------------------------

    [Fact]
    public async Task RetryOutsideRateLimiter_Rejection_IsNotRetried_TerminalTooManyRequests()
    {
        // R -> RL (Retry outermost, RateLimiter innermost).
        // The gate stays full (maxConcurrency=1, held by a background op,
        // acquisitionTimeout=Zero). RateLimitRejectedException is NOT in
        // IsTransientException, so R does NOT retry.
        // Result: TooManyRequests after a single attempt.
        using var rateLimiter = new DcsvIo.D2.Resilience.RateLimiting.RateLimiter(
            new RateLimiterOptions(maxConcurrency: 1, acquisitionTimeout: TimeSpan.Zero));

        using var pipeline = new ResilientPipeline<string, int>(
            NoDelayOptions(maxAttempts: 5),
            new RateLimiterLayer<string, int>(rateLimiter));

        var gate = new TaskCompletionSource();
        var acquired = new TaskCompletionSource(); // completes when the permit is held

        // Hold the only permit.
        var holder = rateLimiter.ExecuteAsync(
            async _ =>
            {
                acquired.SetResult();
                await gate.Task;
                return 0;
            },
            CancellationToken.None).AsTask();

        // Wait until the permit is held before the pipeline call.
        await acquired.Task;

        var innerCalls = 0;
        var result = await pipeline.ExecuteAsync("k", _ =>
        {
            Interlocked.Increment(ref innerCalls);
            return ValueTask.FromResult(1);
        });

        gate.SetResult();
        await holder;

        result.Success.Should().BeFalse();
        result.IsRateLimited.Should().BeTrue();

        // RateLimitRejectedException is not transient, so R does not retry and the
        // inner op is never reached.
        innerCalls.Should().Be(0);
    }

    // ----------------------------------------------------------------
    // 6. Singleflight outermost deduplicates calls across the full stack.
    //    N concurrent same-key callers -> the stack executes ONCE and all callers
    //    share the result. Distinct keys -> distinct executions.
    // ----------------------------------------------------------------

    [Fact]
    public async Task Singleflight_Outermost_DedupsAcrossFullStack_ConcurrentSameKey()
    {
        // SF -> RL -> R -> CB -> Ta. 10 concurrent callers with the same key.
        // The full inner stack (RL+R+CB+Ta) should execute ONCE for the shared key.
        var sf = new SingleflightT();
        var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 100));
        var pipeline = new ResilientPipeline<string, int>(
            new SingleflightLayer<string, int>(sf),
            new RateLimiterLayer<string, int>(new RateLimiterOptions(maxConcurrency: 10)),
            NoDelayOptions(maxAttempts: 3),
            new CircuitBreakerLayer<string, int>(cb),
            new TimeoutLayer<string, int>(new TimeoutOptions(TimeSpan.FromSeconds(5))));

        ThreadPool.GetMinThreads(out var origWorker, out var origIo);
        ThreadPool.SetMinThreads(Math.Max(origWorker, 30), Math.Max(origIo, 30));
        try
        {
            var innerOpCalls = 0;
            var gate = new TaskCompletionSource();

            // ResilientPipeline.ExecuteAsync is async, but its first synchronous
            // path builds the layer chain and calls wrapped(ct), which invokes
            // SingleflightLayer.WrapAsync -> Singleflight.ExecuteAsync
            // -> r_inflight.GetOrAdd synchronously, all before the first await.
            // Capturing the ValueTask<D2Result<int>> (without awaiting) therefore
            // shows the caller has joined the singleflight entry. Releasing the
            // semaphore AFTER the capture means that waiting for all N releases
            // guarantees every caller has joined before the gate opens, with no
            // SpinWait or Yield.
            const int concurrent_callers = 10;
            var allJoined = new SemaphoreSlim(0, concurrent_callers);

            var tasks = Enumerable.Range(0, concurrent_callers)
                .Select(_ => Task.Run(async () =>
                {
                    var vt = pipeline.ExecuteAsync("same-key", async _ =>
                    {
                        Interlocked.Increment(ref innerOpCalls);
                        await gate.Task;
                        return 77;
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

            // The inner stack ran exactly once.
            innerOpCalls.Should().Be(1);

            // Every caller receives the same result.
            results.Should().AllSatisfy(r =>
            {
                r.Success.Should().BeTrue();
                r.Data.Should().Be(77);
            });
        }
        finally
        {
            ThreadPool.SetMinThreads(origWorker, origIo);
        }
    }

    [Fact]
    public async Task Singleflight_Outermost_DistinctKeys_ExecuteDistinctly()
    {
        // SF outermost. Two distinct keys -> two independent executions.
        var sf = new SingleflightT();
        using var pipeline = new ResilientPipeline<string, int>(
            new SingleflightLayer<string, int>(sf),
            NoDelayOptions(maxAttempts: 2));

        var calls = 0;
        var r1 = await pipeline.ExecuteAsync("key-A", _ =>
        {
            Interlocked.Increment(ref calls);
            return ValueTask.FromResult(1);
        });
        var r2 = await pipeline.ExecuteAsync("key-B", _ =>
        {
            Interlocked.Increment(ref calls);
            return ValueTask.FromResult(2);
        });

        r1.Success.Should().BeTrue();
        r2.Success.Should().BeTrue();
        calls.Should().Be(2); // Each key ran its own op.
    }

    // ----------------------------------------------------------------
    // 7. Caller cancellation through a deep stack is NOT mapped to another result.
    //    RL -> Tt -> R -> CB -> Ta: the caller cancels mid-op -> Canceled.
    // ----------------------------------------------------------------

    [Fact]
    public async Task CallerCancellation_ThroughDeepStack_MapsToCancel_NotServiceUnavailable()
    {
        // RL -> Tt -> R -> CB -> Ta. The caller cancels its CancellationToken while
        // the inner op is in progress. The OCE propagates out through every layer:
        // TimeoutLayer's when-guard (!ct.IsCancellationRequested) is false, so the
        // OCE passes through; RetryHelper catches the OCE from ct and rethrows it;
        // the pipeline boundary catches the OCE when ct.IsCancellationRequested
        // and returns Canceled.
        var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 100));
        using var pipeline = new ResilientPipeline<string, int>(
            new RateLimiterLayer<string, int>(new RateLimiterOptions(maxConcurrency: 10)),
            new TimeoutLayer<string, int>(new TimeoutOptions(TimeSpan.FromSeconds(10))),
            NoDelayOptions(maxAttempts: 3),
            new CircuitBreakerLayer<string, int>(cb),
            new TimeoutLayer<string, int>(new TimeoutOptions(TimeSpan.FromSeconds(5))));

        using var cts = new CancellationTokenSource();
        var result = await pipeline.ExecuteAsync(
            "k",
            async ct =>
            {
                await cts.CancelAsync();
                ct.ThrowIfCancellationRequested();
                return 1;
            },
            cts.Token);

        result.Success.Should().BeFalse();
        result.IsCanceled.Should().BeTrue();
        result.IsServiceUnavailable.Should().BeFalse();
        result.IsRateLimited.Should().BeFalse();
    }

    // ----------------------------------------------------------------
    // 8. PassThrough (zero-layer) maps exceptions to D2Result, applies no
    //    retry/break/limit/timeout.
    // ----------------------------------------------------------------

    [Fact]
    public async Task PassThrough_ZeroLayers_MapsExceptionNoRetry_CallCountOne()
    {
        // Zero layers -> no retry, no CB, no timeout, no RL.
        // A transient exception (TimeoutException) is mapped to ServiceUnavailable
        // with exactly ONE op invocation.
        var calls = 0;

        var result = await ResilientPipeline<string, int>.PassThrough.ExecuteAsync("k", _ =>
        {
            Interlocked.Increment(ref calls);
            throw new TimeoutException("no retry in PassThrough");
        });

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();
        calls.Should().Be(1); // No retry layer, so only one attempt.
    }

    // ----------------------------------------------------------------
    // Helper factories
    // ----------------------------------------------------------------

    private static RetryLayer<string, int> NoDelayOptions(int maxAttempts = 3)
        => new(new()
        {
            MaxAttempts = maxAttempts,
            BaseDelayMs = 0,
            MaxDelayMs = 0,
            Jitter = false,
            DelayFunc = (_, _) => Task.CompletedTask,
        });
}
