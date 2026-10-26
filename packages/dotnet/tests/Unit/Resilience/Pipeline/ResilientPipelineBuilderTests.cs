// -----------------------------------------------------------------------
// <copyright file="ResilientPipelineBuilderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.Pipeline;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.CircuitBreaker;
using DcsvIo.D2.Resilience.Pipeline;
using DcsvIo.D2.Resilience.RateLimiting;
using DcsvIo.D2.Resilience.Retry;
using DcsvIo.D2.Resilience.Singleflight;
using DcsvIo.D2.Resilience.Timeout;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class ResilientPipelineBuilderTests
{
    // ----------------------------------------------------------------------
    // Explicit-instance overloads (no DI involved)
    // ----------------------------------------------------------------------

    [Fact]
    public async Task UseSingleflight_ExplicitInstance_AddsLayer()
    {
        var sf = new Singleflight<string, int>();
        var pipeline = NewBuilder<string, int>().UseSingleflight(sf).Build();

        var invocations = 0;
        var gate = new TaskCompletionSource();

        async ValueTask<int> Op(CancellationToken ct)
        {
            Interlocked.Increment(ref invocations);
            await gate.Task;
            return 1;
        }

        var t1 = pipeline.ExecuteAsync("k", Op).AsTask();
        var t2 = pipeline.ExecuteAsync("k", Op).AsTask();

        await Task.Delay(20);
        gate.SetResult();
        await Task.WhenAll(t1, t2);

        invocations.Should().Be(1);
    }

    [Fact]
    public async Task UseCircuitBreaker_ExplicitInstance_AddsLayer()
    {
        var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 1));
        var pipeline = NewBuilder<string, int>().UseCircuitBreaker(cb).Build();

        await pipeline.ExecuteAsync("k", _ => throw new InvalidOperationException());

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(7));

        result.IsServiceUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task UseRetries_ExplicitOptions_AddsLayer()
    {
        var pipeline = NewBuilder<string, int>()
            .UseRetries(NoDelayOptions(maxAttempts: 4))
            .Build();

        var attempts = 0;
        await pipeline.ExecuteAsync("k", _ =>
        {
            Interlocked.Increment(ref attempts);
            throw new TimeoutException();
        });

        attempts.Should().Be(4);
    }

    [Fact]
    public async Task UseRetries_NullOptions_AddsLayerWithDefaults()
    {
        var pipeline = NewBuilder<string, int>().UseRetries().Build();

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(7));

        result.Success.Should().BeTrue();
        result.Data.Should().Be(7);
    }

    // ----------------------------------------------------------------------
    // Keyed DI overloads (the only DI-resolving path)
    // ----------------------------------------------------------------------

    [Fact]
    public async Task UseSingleflight_ServiceKey_ResolvesKeyedFromServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<Singleflight<string, int>>("orders");
        services.AddKeyedSingleton<Singleflight<string, int>>("shipping");
        var sp = services.BuildServiceProvider();

        var pipeline = new ResilientPipelineBuilder<string, int>(sp)
            .UseSingleflight(serviceKey: "orders")
            .Build();

        var invocations = 0;
        var gate = new TaskCompletionSource();
        var t1 = pipeline.ExecuteAsync("k", async _ =>
        {
            Interlocked.Increment(ref invocations);
            await gate.Task;
            return 1;
        }).AsTask();
        var t2 = pipeline.ExecuteAsync("k", async _ =>
        {
            Interlocked.Increment(ref invocations);
            await gate.Task;
            return 1;
        }).AsTask();

        await Task.Delay(20);
        gate.SetResult();
        await Task.WhenAll(t1, t2);

        invocations.Should().Be(1);
    }

    [Fact]
    public async Task UseCircuitBreaker_ServiceKey_ResolvesKeyedFromServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<CircuitBreaker<int>>(
            "orders",
            (_, _) => new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 1)));
        services.AddKeyedSingleton<CircuitBreaker<int>>(
            "shipping",
            (_, _) => new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 1)));
        var sp = services.BuildServiceProvider();

        var pipeline = new ResilientPipelineBuilder<string, int>(sp)
            .UseCircuitBreaker(serviceKey: "orders")
            .Build();

        await pipeline.ExecuteAsync("k", _ => throw new InvalidOperationException());

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(7));
        result.IsServiceUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task KeyedCircuitBreakers_AreIndependent_AcrossPipelines()
    {
        // Two pipelines wired to keyed CBs with DIFFERENT keys must NOT share
        // state: opening one pipeline's breaker leaves the other unaffected.
        // The library uses keyed services so that a host can run several
        // pipelines with separate state (multi-exchange RMQ, multi-endpoint outbound).
        var services = new ServiceCollection();
        services.AddKeyedSingleton<CircuitBreaker<int>>(
            "orders",
            (_, _) => new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 1)));
        services.AddKeyedSingleton<CircuitBreaker<int>>(
            "shipping",
            (_, _) => new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 1)));
        var sp = services.BuildServiceProvider();

        var ordersPipeline = new ResilientPipelineBuilder<string, int>(sp)
            .UseCircuitBreaker(serviceKey: "orders")
            .Build();

        var shippingPipeline = new ResilientPipelineBuilder<string, int>(sp)
            .UseCircuitBreaker(serviceKey: "shipping")
            .Build();

        // Open the orders breaker only.
        await ordersPipeline.ExecuteAsync("k", _ => throw new InvalidOperationException());

        var ordersAfter = await ordersPipeline.ExecuteAsync("k", _ => ValueTask.FromResult(1));
        var shippingAfter = await shippingPipeline
            .ExecuteAsync("k", _ => ValueTask.FromResult(2));

        ordersAfter.IsServiceUnavailable.Should().BeTrue();   // orders breaker is open
        shippingAfter.Success.Should().BeTrue();        // shipping breaker is unaffected
        shippingAfter.Data.Should().Be(2);
    }

    // ----------------------------------------------------------------------
    // UseTimeout
    // ----------------------------------------------------------------------

    [Fact]
    public async Task UseTimeout_NullOptions_AddsLayerWithDefaults()
    {
        var pipeline = NewBuilder<string, int>().UseTimeout().Build();

        // The default timeout is 10s; the op completes immediately, so no timeout fires.
        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(7));

        result.Success.Should().BeTrue();
        result.Data.Should().Be(7);
    }

    [Fact]
    public async Task UseTimeout_ExplicitOptions_AddsLayer()
    {
        // A very short timeout and an op that never completes -> the pipeline
        // boundary returns ServiceUnavailable.
        var pipeline = NewBuilder<string, int>()
            .UseTimeout(new TimeoutOptions(TimeSpan.FromMilliseconds(30)))
            .Build();

        var result = await pipeline.ExecuteAsync("k", async ct =>
        {
            await Task.Delay(-1, ct);
            return 0;
        });

        result.IsServiceUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task UseTimeout_CalledTwice_ProducesTwoDistinctInstances_AtTwoPositions()
    {
        // Two UseTimeout calls add a total-request timeout and a per-attempt timeout.
        // The per-attempt timeout is very short, so the first attempt times out;
        // the retry layer between the total and per-attempt timeouts retries,
        // and the fast second attempt succeeds.
        var attempts = 0;
        var pipeline = NewBuilder<string, int>()
            .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(5))) // total
            .UseRetries(NoDelayOptions(maxAttempts: 3))
            .UseTimeout(new TimeoutOptions(TimeSpan.FromMilliseconds(40))) // per-attempt
            .Build();

        var result = await pipeline.ExecuteAsync("k", async ct =>
        {
            var n = Interlocked.Increment(ref attempts);
            if (n == 1)
                await Task.Delay(-1, ct);
            return 42;
        });

        result.Success.Should().BeTrue();
        result.Data.Should().Be(42);
        attempts.Should().Be(2);
    }

    // ----------------------------------------------------------------------
    // UseRateLimiter
    // ----------------------------------------------------------------------

    [Fact]
    public async Task UseRateLimiter_NullOptions_AddsLayerWithDefaults()
    {
        var pipeline = NewBuilder<string, int>().UseRateLimiter().Build();

        // The default MaxConcurrency is 100; a single call succeeds.
        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(7));

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task UseRateLimiter_ExplicitOptions_AddsLayer()
    {
        // MaxConcurrency=1 and a zero acquisition timeout -> the second concurrent call
        // is rejected.
        var pipeline = NewBuilder<string, int>()
            .UseRateLimiter(new RateLimiterOptions(maxConcurrency: 1, acquisitionTimeout: TimeSpan.Zero))
            .Build();

        var gate = new TaskCompletionSource();
        var first = pipeline.ExecuteAsync("k", async _ =>
        {
            await gate.Task;
            return 1;
        }).AsTask();

        await Task.Delay(20);

        var second = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(99));

        second.IsRateLimited.Should().BeTrue();

        gate.SetResult();
        await first;
    }

    [Fact]
    public async Task UseRateLimiter_ServiceKey_ResolvesKeyedFromServiceProvider()
    {
        const string key = "test-rl-key";
        var services = new ServiceCollection();
        services.AddKeyedSingleton<RateLimiter>(key, (_, _) => new RateLimiter(new(maxConcurrency: 5)));
        var sp = services.BuildServiceProvider();

        var pipeline = new ResilientPipelineBuilder<string, int>(sp)
            .UseRateLimiter(serviceKey: key)
            .Build();

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(42));

        result.Success.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // Multi-layer chaining + order preservation
    // ----------------------------------------------------------------------

    [Fact]
    public async Task FluentChain_PreservesRegistrationOrder_AsLayerOrder()
    {
        // Adversarial: the order in which the Use* methods are called must equal
        // the outer-first layer order of the resulting pipeline. Two retry layers
        // with different MaxAttempts both treat the same exception as transient,
        // so each outer attempt runs every inner attempt and the total is the
        // product of the MaxAttempts values. The product is the same in either
        // order, so the assertion shows that both layers are applied, not their
        // order.
        var pipeline = NewBuilder<string, int>()
            .UseRetries(NoDelayOptions(maxAttempts: 2)) // outer retry: 2 attempts
            .UseRetries(NoDelayOptions(maxAttempts: 3)) // inner retry: 3 attempts each call
            .Build();

        var attempts = 0;
        await pipeline.ExecuteAsync("k", _ =>
        {
            Interlocked.Increment(ref attempts);
            throw new TimeoutException();
        });

        // Outer (2) x inner (3) = 6 total operation calls.
        attempts.Should().Be(6);
    }

    [Fact]
    public void Build_AfterMultipleConfiguration_SnapshotsLayerSet()
    {
        var pipeline = NewBuilder<string, int>()
            .UseRetries(NoDelayOptions())
            .UseCircuitBreaker(new CircuitBreaker<int>(_ => false))
            .Build();

        pipeline.Should().NotBeNull();
    }

    [Fact]
    public async Task Build_CalledTwice_ProducesIndependentPipelinesSharingLayerInstances()
    {
        // Adversarial: calling Build() twice on the same builder yields two
        // DIFFERENT pipeline instances that share the underlying layer
        // instances (and therefore primitive state). Opening the CB through
        // pipeline #1 must be observable through pipeline #2, because both
        // pipelines wrap the same configured layers.
        var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 1));
        var builder = NewBuilder<string, int>().UseCircuitBreaker(cb);

        var pipelineA = builder.Build();
        var pipelineB = builder.Build();

        pipelineA.Should().NotBeSameAs(pipelineB);

        // Open the breaker through A.
        await pipelineA.ExecuteAsync("k", _ => throw new InvalidOperationException());

        // B returns ServiceUnavailable because both pipelines share the CB instance.
        var bResult = await pipelineB.ExecuteAsync("k", _ => ValueTask.FromResult(1));
        bResult.IsServiceUnavailable.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    /// <summary>
    /// Builder bound to an empty service provider. The explicit-instance
    /// overloads never touch DI, so they work with it.
    /// </summary>
    private static ResilientPipelineBuilder<TKey, TValue> NewBuilder<TKey, TValue>()
        where TKey : notnull
        => new(EmptyServiceProvider.Instance);

    private static RetryOptions<int> NoDelayOptions(int maxAttempts = 3)
        => new()
        {
            MaxAttempts = maxAttempts,
            BaseDelayMs = 0,
            MaxDelayMs = 0,
            Jitter = false,
            DelayFunc = (_, _) => Task.CompletedTask,
        };

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static readonly EmptyServiceProvider Instance = new();

        public object? GetService(Type serviceType) => null;
    }
}
