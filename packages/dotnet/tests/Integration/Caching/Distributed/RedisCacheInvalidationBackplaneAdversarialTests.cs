// -----------------------------------------------------------------------
// <copyright file="RedisCacheInvalidationBackplaneAdversarialTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Caching.Distributed;

using AwesomeAssertions;
using DcsvIo.D2.Caching.Distributed.Redis;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

/// <summary>
/// Adversarial / stress tests for <c>RedisCacheInvalidationBackplane</c>.
/// Probes the dispatch loop, subscription lifetime, error isolation, and
/// rapid-fire publish patterns.
/// </summary>
[Collection("Redis")]
public sealed class RedisCacheInvalidationBackplaneAdversarialTests
{
    private readonly RedisFixture r_fixture;

    public RedisCacheInvalidationBackplaneAdversarialTests(RedisFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
    public async Task Subscribe_ManySubscribers_AllReceiveAllMessages()
    {
        // 50 simultaneous subscribers on the same backplane. One publish ->
        // every subscriber's handler must fire.
        var channel = $"chaos-{Guid.NewGuid():N}";
        await using var backplane = NewBackplane(channel);

        const int subscribers = 50;
        var receivedCount = 0;
        var gate = new TaskCompletionSource();
        var subs = new List<IAsyncDisposable>(subscribers);

        for (var i = 0; i < subscribers; i++)
        {
            subs.Add(backplane.Subscribe((_, _) =>
            {
                if (Interlocked.Increment(ref receivedCount) >= subscribers)
                    gate.TrySetResult();
                return ValueTask.CompletedTask;
            }));
        }

        await backplane.PublishInvalidationAsync("k");
        await gate.Task.WaitAsync(TimeSpan.FromSeconds(5));

        receivedCount.Should().Be(subscribers);

        foreach (var sub in subs)
            await sub.DisposeAsync();
    }

    [Fact]
    public async Task Subscribe_SubscribeThenImmediatelyDispose_NoLeak()
    {
        // Pathological pattern: subscribe + immediately dispose. Repeat 200x.
        // No leaks, no exceptions.
        var channel = $"chaos-{Guid.NewGuid():N}";
        await using var backplane = NewBackplane(channel);

        for (var i = 0; i < 200; i++)
        {
            var sub = backplane.Subscribe((_, _) => ValueTask.CompletedTask);
            await sub.DisposeAsync();
        }

        // Final state: backplane has zero subscribers; can still publish without error.
        var publish = await backplane.PublishInvalidationAsync("k");
        publish.IsOk.Should().BeTrue();
    }

    [Fact]
    public async Task Subscribe_ConcurrentSubscribeAndUnsubscribe_NoCrash()
    {
        // Four workers: two subscribe and dispose in a loop, and two publish in
        // a loop. Verifies the ConcurrentDictionary-backed subscription
        // store does not fail under concurrent mutation.
        var channel = $"chaos-{Guid.NewGuid():N}";
        await using var backplane = NewBackplane(channel);

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 4),
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            async (worker, ct) =>
            {
                if (worker < 2)
                {
                    for (var i = 0; i < 50; i++)
                    {
                        var sub = backplane.Subscribe((_, _) => ValueTask.CompletedTask);
                        await Task.Delay(1, ct);
                        await sub.DisposeAsync();
                    }
                }
                else
                {
                    for (var i = 0; i < 200; i++)
                    {
                        await backplane.PublishInvalidationAsync($"k{i}", ct);
                        await Task.Delay(1, ct);
                    }
                }
            });
    }

    [Fact]
    public async Task Subscribe_SlowHandler_DoesNotBlockOtherSubscribers()
    {
        // One handler sleeps 200ms; another should still receive messages
        // without being blocked by the slow one. Verifies dispatch fans out
        // (handlers do not run sequentially on a single thread).
        var channel = $"chaos-{Guid.NewGuid():N}";
        await using var backplane = NewBackplane(channel);

        var fastReceived = 0;
        var slowReceived = 0;

        await using var slow = backplane.Subscribe(async (_, ct) =>
        {
            await Task.Delay(200, ct);
            Interlocked.Increment(ref slowReceived);
        });

        await using var fast = backplane.Subscribe((_, _) =>
        {
            Interlocked.Increment(ref fastReceived);
            return ValueTask.CompletedTask;
        });

        // Fire 5 messages rapidly.
        for (var i = 0; i < 5; i++)
            await backplane.PublishInvalidationAsync($"k{i}");

        // Within 500ms, the fast handler should have caught all 5.
        await Task.Delay(500);

        fastReceived.Should().Be(5);

        // The slow handler may not have received every message yet (each takes 200ms).
    }

    [Fact]
    public async Task Subscribe_HandlerThrows_OtherHandlersStillReceiveSubsequentMessages()
    {
        // One handler throws every time; another tracks count. After 3 publishes,
        // the good handler should have received exactly 3.
        var channel = $"chaos-{Guid.NewGuid():N}";
        await using var backplane = NewBackplane(channel);

        var goodReceived = 0;
        var gate = new TaskCompletionSource();

        await using var bad = backplane.Subscribe(
            (_, _) => throw new InvalidOperationException("nope"));
        await using var good = backplane.Subscribe((_, _) =>
        {
            if (Interlocked.Increment(ref goodReceived) >= 3)
                gate.TrySetResult();
            return ValueTask.CompletedTask;
        });

        await backplane.PublishInvalidationAsync("k1");
        await backplane.PublishInvalidationAsync("k2");
        await backplane.PublishInvalidationAsync("k3");

        await gate.Task.WaitAsync(TimeSpan.FromSeconds(5));
        goodReceived.Should().Be(3);
    }

    [Fact]
    public async Task PublishMany_ManyKeys_AllReceived()
    {
        // 1000 keys in a single PublishInvalidationManyAsync - all delivered.
        var channel = $"chaos-{Guid.NewGuid():N}";
        await using var backplane = NewBackplane(channel);

        var received = 0;
        var gate = new TaskCompletionSource();
        await using var sub = backplane.Subscribe((_, _) =>
        {
            if (Interlocked.Increment(ref received) >= 1000)
                gate.TrySetResult();
            return ValueTask.CompletedTask;
        });

        var keys = Enumerable.Range(0, 1000).Select(i => $"k{i}").ToArray();
        await backplane.PublishInvalidationManyAsync(keys);

        await gate.Task.WaitAsync(TimeSpan.FromSeconds(10));
        received.Should().Be(1000);
    }

    [Fact]
    public async Task Subscribe_AcrossManyInstances_AllReceiveCrossPublishes()
    {
        // Multi-instance scenario: 5 backplane instances on the same channel
        // (the replicas of a cluster). Instance 0 publishes; every instance,
        // including instance 0, receives the message. Verifies pub/sub
        // fan-out works at a realistic scale.
        var channel = $"chaos-{Guid.NewGuid():N}";
        var instances = new List<RedisCacheInvalidationBackplane>();
        var subs = new List<IAsyncDisposable>();
        var receivedPerInstance = new int[5];
        var gate = new TaskCompletionSource();

        for (var i = 0; i < 5; i++)
        {
            instances.Add(NewBackplane(channel));
        }

        try
        {
            for (var i = 0; i < 5; i++)
            {
                var idx = i;
                subs.Add(instances[i].Subscribe((_, _) =>
                {
                    Interlocked.Increment(ref receivedPerInstance[idx]);
                    if (receivedPerInstance.Sum() >= 5)
                        gate.TrySetResult();
                    return ValueTask.CompletedTask;
                }));
            }

            // Instance 0 publishes; all 5 (incl. instance 0 per "everyone acts" rule) receive.
            await instances[0].PublishInvalidationAsync("k");

            await gate.Task.WaitAsync(TimeSpan.FromSeconds(5));
            receivedPerInstance.Sum().Should().BeGreaterThanOrEqualTo(5);
        }
        finally
        {
            foreach (var sub in subs)
                await sub.DisposeAsync();
            foreach (var instance in instances)
                await instance.DisposeAsync();
        }
    }

    [Fact]
    public async Task Subscribe_DisposedTwice_IsIdempotent()
    {
        var channel = $"chaos-{Guid.NewGuid():N}";
        await using var backplane = NewBackplane(channel);

        var sub = backplane.Subscribe((_, _) => ValueTask.CompletedTask);
        await sub.DisposeAsync();
        await sub.DisposeAsync();  // should not throw
    }

    [Fact]
    public async Task PublishInvalidation_AfterDispose_ReturnsServiceUnavailable()
    {
        // The multiplexer stays usable after the backplane is disposed, so without
        // the disposed check the publish would go through and return Ok.
        await using var redis = await ConnectAsync();
        var backplane = NewBackplane(redis, $"chaos-{Guid.NewGuid():N}");
        await backplane.DisposeAsync();

        var result = await backplane.PublishInvalidationAsync("k");

        result.IsServiceUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task PublishInvalidationMany_AfterDispose_ReturnsServiceUnavailable()
    {
        await using var redis = await ConnectAsync();
        var backplane = NewBackplane(redis, $"chaos-{Guid.NewGuid():N}");
        await backplane.DisposeAsync();

        var result = await backplane.PublishInvalidationManyAsync(["k1", "k2"]);

        result.IsServiceUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task PublishInvalidation_BeforeDispose_ReturnsOk()
    {
        // The admitting counterpart over the same multiplexer shape.
        await using var redis = await ConnectAsync();
        await using var backplane = NewBackplane(redis, $"chaos-{Guid.NewGuid():N}");

        var result = await backplane.PublishInvalidationAsync("k");

        result.IsOk.Should().BeTrue();
    }

    [MustDisposeResource(false)]
    private RedisCacheInvalidationBackplane NewBackplane(string channel)
    {
        var redis = ConnectionMultiplexer.Connect(r_fixture.ConnectionString);
        var opts = Options.Create(new RedisCacheOptions
        {
            ConnectionString = r_fixture.ConnectionString,
            InvalidationChannel = channel,
        });
        return new RedisCacheInvalidationBackplane(
            redis,
            opts,
            NullLogger<RedisCacheInvalidationBackplane>.Instance);
    }

    [MustDisposeResource(false)]
    private RedisCacheInvalidationBackplane NewBackplane(
        IConnectionMultiplexer redis,
        string channel)
        => new(
            redis,
            Options.Create(new RedisCacheOptions
            {
                ConnectionString = r_fixture.ConnectionString,
                InvalidationChannel = channel,
            }),
            NullLogger<RedisCacheInvalidationBackplane>.Instance);

    private Task<ConnectionMultiplexer> ConnectAsync()
        => ConnectionMultiplexer.ConnectAsync(r_fixture.ConnectionString);
}
