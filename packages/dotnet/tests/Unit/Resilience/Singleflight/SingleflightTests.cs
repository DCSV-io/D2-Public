// -----------------------------------------------------------------------
// <copyright file="SingleflightTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.Singleflight;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.Singleflight;
using Xunit;

public sealed class SingleflightTests
{
    // ----------------------------------------------------------------------
    // Single call
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_SingleCall_ReturnsOperationResult()
    {
        var sf = new Singleflight<string, int>();

        var result = await sf.ExecuteAsync("k", _ => ValueTask.FromResult(42));

        result.Should().Be(42);
        sf.Size.Should().Be(0); // key removed after completion
    }

    // ----------------------------------------------------------------------
    // Deduplication: same key -> operation runs once
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ConcurrentCallersSameKey_OperationRunsOnce()
    {
        // The gate keeps the operation pending, so the callers are concurrent before
        // it completes. This shows deduplication, not sequential reuse.
        var sf = new Singleflight<string, int>();
        var operationInvocations = 0;
        var gate = new TaskCompletionSource();

        async ValueTask<int> Operation(CancellationToken ct)
        {
            Interlocked.Increment(ref operationInvocations);
            await gate.Task;
            return 7;
        }

        var t1 = sf.ExecuteAsync("k", Operation).AsTask();
        var t2 = sf.ExecuteAsync("k", Operation).AsTask();
        var t3 = sf.ExecuteAsync("k", Operation).AsTask();

        // Let the tasks enter the operation; the gate is still closed, so they
        // wait. Size shows the in-flight entry during this time.
        await Task.Delay(20);
        sf.Size.Should().Be(1);

        gate.SetResult();
        var results = await Task.WhenAll(t1, t2, t3);

        operationInvocations.Should().Be(1);
        results.Should().Equal(7, 7, 7);
        sf.Size.Should().Be(0);
    }

    // ----------------------------------------------------------------------
    // Different keys -> independent operations
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ConcurrentCallersDifferentKeys_BothOperationsRun()
    {
        var sf = new Singleflight<string, int>();
        var invocations = 0;
        var gate = new TaskCompletionSource();

        async ValueTask<int> OperationA(CancellationToken ct)
        {
            Interlocked.Increment(ref invocations);
            await gate.Task;
            return 1;
        }

        async ValueTask<int> OperationB(CancellationToken ct)
        {
            Interlocked.Increment(ref invocations);
            await gate.Task;
            return 2;
        }

        var ta = sf.ExecuteAsync("a", OperationA).AsTask();
        var tb = sf.ExecuteAsync("b", OperationB).AsTask();

        await Task.Delay(20);
        sf.Size.Should().Be(2);

        gate.SetResult();
        var results = await Task.WhenAll(ta, tb);

        invocations.Should().Be(2);
        results.Should().BeEquivalentTo([1, 2]);
    }

    // ----------------------------------------------------------------------
    // Key removal after completion
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_SequentialCallsSameKey_OperationRunsTwice()
    {
        // Adversarial: Singleflight is NOT a cache. After the first call
        // completes, the second call must run the operation again.
        var sf = new Singleflight<string, int>();
        var invocations = 0;

        async ValueTask<int> Operation(CancellationToken ct)
        {
            Interlocked.Increment(ref invocations);
            await Task.Yield();
            return 1;
        }

        await sf.ExecuteAsync("k", Operation);
        await sf.ExecuteAsync("k", Operation);

        invocations.Should().Be(2);
        sf.Size.Should().Be(0);
    }

    // ----------------------------------------------------------------------
    // Exception propagation
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_OperationThrows_ExceptionPropagatesToAllWaiters()
    {
        // Concurrent waiters share the same Task, so each receives the same
        // exception. The finally block removes the key, so later calls can try again.
        var sf = new Singleflight<string, int>();
        var gate = new TaskCompletionSource();

        async ValueTask<int> Operation(CancellationToken ct)
        {
            await gate.Task;
            throw new InvalidOperationException("operation failed");
        }

        var t1 = sf.ExecuteAsync("k", Operation).AsTask();
        var t2 = sf.ExecuteAsync("k", Operation).AsTask();

        await Task.Delay(20);
        gate.SetResult();

        var act1 = async () => await t1;
        var act2 = async () => await t2;

        await act1.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("operation failed");
        await act2.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("operation failed");

        sf.Size.Should().Be(0); // key removed even on failure
    }

    [Fact]
    public async Task ExecuteAsync_AfterException_NextCallStartsFreshOperation()
    {
        var sf = new Singleflight<string, int>();
        var invocations = 0;

        async ValueTask<int> Operation(CancellationToken ct)
        {
            await Task.Yield();
            Interlocked.Increment(ref invocations);
            throw new InvalidOperationException();
        }

        for (var i = 0; i < 2; i++)
        {
            try
            {
                await sf.ExecuteAsync("k", Operation);
            }
            catch (InvalidOperationException)
            {
                // Expected: each failed call throws.
            }
        }

        invocations.Should().Be(2); // the key was cleared after each failure
    }

    // ----------------------------------------------------------------------
    // Per-caller cancellation
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_PerCallerCancellation_DoesNotAffectSiblings()
    {
        // Adversarial property: when caller A cancels, the shared operation
        // and caller B keep running. This is the main guarantee of the
        // singleflight design.
        var sf = new Singleflight<string, int>();
        var gate = new TaskCompletionSource();
        var operationCanceledFlag = false;

        async ValueTask<int> Operation(CancellationToken ct)
        {
            await gate.Task;

            // The shared operation receives CancellationToken.None, so it
            // does not see per-caller cancellations.
            operationCanceledFlag = ct.IsCancellationRequested;
            return 7;
        }

        using var ctsA = new CancellationTokenSource();
        var taskA = sf.ExecuteAsync("k", Operation, ctsA.Token).AsTask();
        var taskB = sf.ExecuteAsync("k", Operation, CancellationToken.None).AsTask();

        await Task.Delay(20);
        await ctsA.CancelAsync();

        var actA = async () => await taskA;
        await actA.Should().ThrowAsync<OperationCanceledException>();

        // Caller B is still waiting; release the gate.
        gate.SetResult();
        var resultB = await taskB;

        resultB.Should().Be(7);
        operationCanceledFlag.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_NoCancelableToken_TakesFastPath()
    {
        // Covers the ct.CanBeCanceled == false branch, which awaits the
        // shared task itself with no WaitAsync wrapper.
        var sf = new Singleflight<string, int>();

        var result = await sf.ExecuteAsync(
            "k",
            _ => ValueTask.FromResult(11),
            CancellationToken.None);

        result.Should().Be(11);
    }

    // ----------------------------------------------------------------------
    // Generic key types - TKey: notnull constraint
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_NonStringKey_DedupesByEquatableValue()
    {
        // Adversarial: TKey is not limited to string. (Guid keys are hashable
        // and equatable, like strings.)
        var sf = new Singleflight<Guid, int>();
        var key = Guid.NewGuid();
        var invocations = 0;
        var gate = new TaskCompletionSource();

        async ValueTask<int> Operation(CancellationToken ct)
        {
            Interlocked.Increment(ref invocations);
            await gate.Task;
            return 5;
        }

        var t1 = sf.ExecuteAsync(key, Operation).AsTask();
        var t2 = sf.ExecuteAsync(key, Operation).AsTask();

        await Task.Delay(20);
        gate.SetResult();
        await Task.WhenAll(t1, t2);

        invocations.Should().Be(1);
    }

    // ----------------------------------------------------------------------
    // High-concurrency stress and cleanup of finished keys
    // ----------------------------------------------------------------------

    [Fact]
    public async Task HighConcurrency_ManyCallersSameKey_OperationRunsExactlyOnce()
    {
        // 200 concurrent waiters race to add the same key. Lazy<>'s
        // ExecutionAndPublication mode and ConcurrentDictionary.GetOrAdd
        // together must guarantee exactly ONE operation invocation. Every one of the
        // 200 callers must receive the same returned value.
        //
        // Singleflight.ExecuteAsync is an async method whose first synchronous
        // operation is r_inflight.GetOrAdd (the join into the shared in-flight
        // entry). No await precedes GetOrAdd, so a caller that has called
        // ExecuteAsync and captured the returned ValueTask, without awaiting it,
        // has already joined. Releasing the semaphore AFTER capturing the
        // ValueTask therefore guarantees that every caller has joined before the
        // gate opens, with no SpinWait or Yield timing guess. The test raises
        // the minimum thread-pool size because the default growth rate
        // (one worker per ~0.5s) would let late callers arrive AFTER the
        // operation already returned, and each of them would start its own invocation.
        ThreadPool.GetMinThreads(out var origWorker, out var origIo);
        ThreadPool.SetMinThreads(Math.Max(origWorker, 256), Math.Max(origIo, 256));
        try
        {
            var sf = new Singleflight<string, int>();
            var invocations = 0;
            var gate = new TaskCompletionSource<int>();
            const int concurrent_threads = 200;

            // Release AFTER capturing the ValueTask from ExecuteAsync (not before
            // calling it). GetOrAdd runs synchronously as the first operation
            // inside ExecuteAsync, so once the ValueTask is captured the join has
            // already happened. Each release means that one caller has joined.
            var allJoined = new SemaphoreSlim(0, concurrent_threads);

            async ValueTask<int> Operation(CancellationToken ct)
            {
                Interlocked.Increment(ref invocations);
                return await gate.Task;
            }

            var tasks = Enumerable.Range(0, concurrent_threads)
                .Select(_ => Task.Run(async () =>
                {
                    var vt = sf.ExecuteAsync("k", Operation); // the join is synchronous: GetOrAdd ran
                    allJoined.Release();                       // release AFTER the join, not before
                    return await vt;
                }))
                .ToArray();

            // Wait for every caller to join. The wait is on the semaphore, not a poll.
            for (var i = 0; i < concurrent_threads; i++)
                await allJoined.WaitAsync();

            // Every caller has joined the single in-flight entry.
            sf.Size.Should().Be(1);
            gate.SetResult(42);

            var results = await Task.WhenAll(tasks);

            invocations.Should().Be(1);
            results.Should().AllBeEquivalentTo(42);
            sf.Size.Should().Be(0);
        }
        finally
        {
            ThreadPool.SetMinThreads(origWorker, origIo);
        }
    }

    [Fact]
    public async Task ManyUniqueKeys_AfterAllComplete_SizeReturnsToZero()
    {
        // Adversarial: 500 distinct keys execute concurrently. After all
        // complete, the in-flight dictionary must be empty, which shows that the
        // RunAsync finally block removes every key, also under load.
        var sf = new Singleflight<string, int>();
        const int unique_keys = 500;

        var tasks = Enumerable.Range(0, unique_keys).Select(i => Task.Run(async () =>
        {
            return await sf.ExecuteAsync($"key-{i}", async _ =>
            {
                await Task.Yield();
                return i;
            });
        })).ToArray();

        await Task.WhenAll(tasks);

        sf.Size.Should().Be(0);
    }

    [Fact]
    public async Task Reentrancy_OperationCallsBackWithDifferentKey_Works()
    {
        // Adversarial property: an operation may call back into the same
        // Singleflight instance with a DIFFERENT key. (Same-key reentrancy
        // would deadlock: Lazy.Value blocks waiting for the outer call to
        // complete, and the outer call is waiting for the inner one. The
        // deadlock case has no test, because such a test is too risky;
        // this test covers the safe case.)
        var sf = new Singleflight<string, int>();

        var result = await sf.ExecuteAsync("outer", async outerCt =>
        {
            var inner = await sf.ExecuteAsync(
                "inner",
                _ => ValueTask.FromResult(7),
                outerCt);
            return inner * 2;
        });

        result.Should().Be(14);
        sf.Size.Should().Be(0);
    }
}
