// -----------------------------------------------------------------------
// <copyright file="ResilientPipelineServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.Pipeline;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.CircuitBreaker;
using DcsvIo.D2.Resilience.Pipeline;
using DcsvIo.D2.Resilience.RateLimiting;
using DcsvIo.D2.Resilience.Singleflight;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class ResilientPipelineServiceCollectionExtensionsTests
{
    [Fact]
    public void AddResilientPipeline_RegistersAsKeyedSingleton()
    {
        var services = new ServiceCollection();

        services.AddResilientPipeline<string, int>("orders", p => p.UseRetries());

        var sp = services.BuildServiceProvider();
        var first = sp.GetRequiredKeyedService<ResilientPipeline<string, int>>("orders");
        var second = sp.GetRequiredKeyedService<ResilientPipeline<string, int>>("orders");

        first.Should().BeSameAs(second);
    }

    [Fact]
    public async Task AddResilientPipeline_FullStack_ResolvesKeyedPrimitives()
    {
        // Canonical case: each layer passes the key of the keyed primitive it
        // resolves. A const holds the key, so a rename touches one line and a
        // text search finds every use.
        const string key = "ipinfo";
        var services = new ServiceCollection();
        services.AddKeyedSingleton<Singleflight<string, int>>(key);
        services.AddKeyedSingleton<CircuitBreaker<int>>(key, (_, _) => new(_ => false));
        services.AddResilientPipeline<string, int>(key, p => p
            .UseSingleflight(key)
            .UseCircuitBreaker(key));

        var sp = services.BuildServiceProvider();
        var pipeline = sp.GetRequiredKeyedService<ResilientPipeline<string, int>>(key);

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(42));

        result.Success.Should().BeTrue();
        result.Data.Should().Be(42);
    }

    [Fact]
    public async Task AddResilientPipeline_AllowsMultipleInstancesPerSameShape()
    {
        // Registration is keyed so a host can register N pipelines of the SAME
        // (TKey, TValue) shape under distinct keys, each with INDEPENDENT
        // primitive state. Opening one pipeline's CB leaves the others
        // unaffected. (Without keyed registration, MS DI's last-wins behavior
        // would silently replace earlier registrations.)
        var services = new ServiceCollection();
        services.AddKeyedSingleton<CircuitBreaker<int>>(
            "orders", (_, _) => new(_ => false, options: new(failureThreshold: 1)));
        services.AddKeyedSingleton<CircuitBreaker<int>>(
            "shipping", (_, _) => new(_ => false, options: new(failureThreshold: 1)));

        services.AddResilientPipeline<string, int>("orders", p => p.UseCircuitBreaker("orders"));
        services.AddResilientPipeline<string, int>(
            "shipping",
            p => p.UseCircuitBreaker("shipping"));

        var sp = services.BuildServiceProvider();
        var ordersPipeline = sp.GetRequiredKeyedService<ResilientPipeline<string, int>>("orders");
        var shippingPipeline = sp
            .GetRequiredKeyedService<ResilientPipeline<string, int>>("shipping");

        // Open the orders breaker only.
        await ordersPipeline.ExecuteAsync("k", _ => throw new InvalidOperationException());

        var ordersAfter = await ordersPipeline.ExecuteAsync("k", _ => ValueTask.FromResult(1));
        var shippingAfter = await shippingPipeline
            .ExecuteAsync("k", _ => ValueTask.FromResult(2));

        ordersAfter.IsServiceUnavailable.Should().BeTrue();
        shippingAfter.Success.Should().BeTrue();
        shippingAfter.Data.Should().Be(2);
    }

    [Fact]
    public void AddResilientPipeline_ReturnsServiceCollection_ForChaining()
    {
        var services = new ServiceCollection();

        var returned = services.AddResilientPipeline<string, int>("orders", p => p.UseRetries());

        returned.Should().BeSameAs(services);
    }

    // ----------------------------------------------------------------------
    // IDisposable: an inline-options pipeline disposes the RateLimiter it owns
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Dispose_InlineOptionsPipeline_DisposesOwnedRateLimiter()
    {
        // ResilientPipeline.Dispose() must propagate to IDisposable layers. The
        // inline-options UseRateLimiter path creates a RateLimiterLayer with
        // r_ownsLimiter=true. When the ServiceProvider is disposed, it disposes
        // the pipeline (a keyed singleton), the pipeline disposes the layer, and
        // the layer disposes the underlying SemaphoreSlim.
        //
        // Evidence: after ServiceProvider disposal, executing the pipeline makes
        // the disposed SemaphoreSlim raise an ObjectDisposedException inside the
        // layer (the gate inside RateLimiter.ExecuteAsync is a SemaphoreSlim),
        // and the pipeline maps it to UnhandledException instead of letting it
        // escape.
        // Without the IDisposable implementation, the SemaphoreSlim would NOT be
        // disposed and the call would succeed (returning Ok(2), the value of the
        // post-disposal operation below), so the assertion
        // would fail.
        const string key = "inline-rl-dispose-test";
        var services = new ServiceCollection();
        services.AddResilientPipeline<string, int>(
            key,
            p => p.UseRateLimiter(new RateLimiterOptions(maxConcurrency: 5)));

        var sp = services.BuildServiceProvider();
        var pipeline = sp.GetRequiredKeyedService<ResilientPipeline<string, int>>(key);

        // The pipeline works before disposal.
        var before = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(1));
        before.Success.Should().BeTrue();

        // Disposing the ServiceProvider calls Dispose() on the keyed singleton.
        await sp.DisposeAsync();

        // After disposal: RateLimiter.ExecuteAsync calls r_gate.WaitAsync on a
        // disposed SemaphoreSlim -> ObjectDisposedException. The pipeline catches
        // it as a non-transient, non-OCE exception -> UnhandledException.
        // (The pipeline's own catch-all converts any ObjectDisposedException that
        // escapes the layer to UnhandledException. The pipeline never throws.)
        var after = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(2));

        // Must NOT be Ok: the disposed SemaphoreSlim must have been used.
        after.Success.Should().BeFalse();

        // Must reach the non-transient catch-all (ObjectDisposedException is
        // not classified transient by RetryHelper.IsTransientException).
        after.IsUnhandledException.Should().BeTrue();
    }
}
