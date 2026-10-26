// -----------------------------------------------------------------------
// <copyright file="DefaultTieredCacheRedisTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Caching.Tiered;

using AwesomeAssertions;
using DcsvIo.D2.Caching;
using DcsvIo.D2.Caching.Distributed.Redis;
using DcsvIo.D2.Caching.Local.Default;
using DcsvIo.D2.Caching.Tiered;
using DcsvIo.D2.Tests.Integration.Caching.Distributed;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

/// <summary>
/// End-to-end tests for <see cref="DefaultTieredCache"/> against real
/// Redis + real local IMemoryCache. Verifies the cross-instance flows
/// the abstraction promises: cluster invalidation, L2-first writes, L1
/// populate on L2 hit.
/// </summary>
[Collection("Redis")]
public sealed class DefaultTieredCacheRedisTests
{
    // A Redis pub/sub round trip lands on the first or second read of a healthy
    // run; the budget is sized so a passing run never reaches it.
    private const int _PROPAGATION_POLL_ATTEMPTS = 50;
    private const int _PROPAGATION_POLL_GAP_MS = 20;

    private readonly RedisFixture r_fixture;

    public DefaultTieredCacheRedisTests(RedisFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
    public async Task Get_L1Miss_PopulatesFromL2()
    {
        // Write to L2 directly (bypassing tiered) -> tiered Get should
        // miss L1, hit L2, populate L1, return.
        await using var ctx = NewTieredContext();
        await ctx.L2.SetAsync("k", "from-L2");

        var get1 = await ctx.Tiered.GetAsync<string>("k");
        get1.IsOk.Should().BeTrue();
        get1.Data.Should().Be("from-L2");

        // L1 should now have it (no extra L2 round-trip on next call).
        var l1Direct = await ctx.L1.GetAsync<string>("k");
        l1Direct.IsOk.Should().BeTrue();
        l1Direct.Data.Should().Be("from-L2");
    }

    [Fact]
    public async Task Set_WritesToBoth()
    {
        await using var ctx = NewTieredContext();
        await ctx.Tiered.SetAsync("k", "v");

        var l1 = await ctx.L1.GetAsync<string>("k");
        var l2 = await ctx.L2.GetAsync<string>("k");
        l1.Data.Should().Be("v");
        l2.Data.Should().Be("v");
    }

    [Fact]
    public async Task Remove_RemovesFromBoth()
    {
        await using var ctx = NewTieredContext();
        await ctx.Tiered.SetAsync("k", "v");
        await ctx.Tiered.RemoveAsync("k");

        (await ctx.L1.ExistsAsync("k")).Data.Should().BeFalse();
        (await ctx.L2.ExistsAsync("k")).Data.Should().BeFalse();
    }

    [Fact]
    public async Task SetAndBroadcast_OtherInstanceL1Drops()
    {
        // Two tiered instances sharing one Redis backplane = two "replicas".
        // Instance A writes K with broadcast -> instance B's L1 must drop
        // its (potentially stale) copy of K so its next read re-fetches
        // the fresh value from L2.
        var channel = $"test-channel-{Guid.NewGuid():N}";
        var keyPrefix = $"test-{Guid.NewGuid():N}:";

        await using var instanceA = NewTieredContext(channel, keyPrefix);
        await using var instanceB = NewTieredContext(channel, keyPrefix);

        // Pre-load instance B's L1 with stale value via direct L1 access.
        await instanceB.L1.SetAsync("k", "stale-on-B");

        // Instance A writes the new value with broadcast.
        var setResult = await instanceA.Tiered.SetAndBroadcastAsync("k", "fresh-from-A");
        setResult.IsOk.Should().BeTrue();

        // Instance B's L1 should be empty for K (dropped by backplane handler).
        var dropped = await WaitUntilAsync(
            async () => (await instanceB.L1.GetAsync<string>("k")).IsNotFound);

        dropped.Should().BeTrue("the broadcast reaches B within the budget");

        // Instance B's tiered Get should now fetch the fresh value from L2.
        var bTiered = await instanceB.Tiered.GetAsync<string>("k");
        bTiered.Data.Should().Be("fresh-from-A");
    }

    [Fact]
    public async Task SetAndBroadcast_OwnInstanceL1AlsoDrops()
    {
        // Universal "everyone acts" rule: the publishing instance also
        // receives its own message and drops its L1 entry. Next read
        // re-populates from L2 (one extra round-trip - bounded cost).
        var channel = $"test-channel-{Guid.NewGuid():N}";
        var keyPrefix = $"test-{Guid.NewGuid():N}:";

        await using var instance = NewTieredContext(channel, keyPrefix);

        await instance.Tiered.SetAndBroadcastAsync("k", "v");

        // L1 should be empty (self-receive dropped it).
        var dropped = await WaitUntilAsync(
            async () => (await instance.L1.GetAsync<string>("k")).IsNotFound);

        dropped.Should().BeTrue("the publisher receives its own broadcast within the budget");

        // Tiered Get re-populates from L2 (which still has the value).
        var tiered = await instance.Tiered.GetAsync<string>("k");
        tiered.Data.Should().Be("v");

        // L1 now populated again from L2.
        var l1AfterRead = await instance.L1.GetAsync<string>("k");
        l1AfterRead.Data.Should().Be("v");
    }

    [Fact]
    public async Task IncrementAsync_RoutesThroughL2_InvalidatesL1()
    {
        await using var ctx = NewTieredContext();

        // Pre-load L1 with a stale counter value.
        await ctx.L1.SetAsync("counter", 100L);

        // Increment via tiered -> routes to L2 (which doesn't have the key
        // yet, starts at 0+1=1) and invalidates L1.
        var inc = await ctx.Tiered.IncrementAsync("counter");
        inc.IsOk.Should().BeTrue();
        inc.Data.Should().Be(1);  // L2 was empty; counter starts at 0 + 1

        // L1 should have been invalidated.
        var l1 = await ctx.L1.GetAsync<long>("counter");
        l1.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task ConsumeTokenAsync_RoutesThroughL2_AndTouchesNoL1Entry()
    {
        // The bucket state belongs to the shared store: a local copy would
        // reflect one node rather than the cluster, so the tiered flavor
        // delegates and leaves L1 out of it entirely.
        await using var ctx = NewTieredContext();
        var admitted = 0;

        for (var i = 0; i < 4; i++)
        {
            var decision = await ctx.Tiered.ConsumeTokenAsync(
                "bucket", 10, TimeSpan.FromMinutes(1), burst: 3);

            if (decision.Data.Admitted)
                admitted++;
        }

        admitted.Should().Be(3, "L2 holds the bucket and answers for every node");
        (await ctx.L1.GetAsync<object>("bucket")).IsNotFound.Should().BeTrue(
            "no local copy of a shared budget is written");
    }

    [Fact]
    public async Task RemoveAndBroadcast_OtherInstancesDropL1()
    {
        var channel = $"test-channel-{Guid.NewGuid():N}";
        var keyPrefix = $"test-{Guid.NewGuid():N}:";

        await using var instanceA = NewTieredContext(channel, keyPrefix);
        await using var instanceB = NewTieredContext(channel, keyPrefix);

        // Both instances write the value (independent L1 entries).
        await instanceA.Tiered.SetAsync("k", "shared");
        await instanceB.L1.SetAsync("k", "shared-on-B");

        // A removes + broadcasts.
        await instanceA.Tiered.RemoveAndBroadcastAsync("k");

        // B's L1 should be cleared.
        var dropped = await WaitUntilAsync(
            async () => (await instanceB.L1.GetAsync<string>("k")).IsNotFound);

        dropped.Should().BeTrue("the broadcast reaches B within the budget");

        // B's tiered Get -> L1 miss -> L2 miss (A removed from L2) -> NotFound.
        var bTiered = await instanceB.Tiered.GetAsync<string>("k");
        bTiered.IsNotFound.Should().BeTrue();
    }

    /// <summary>
    /// The write goes to L2 and the local copy is dropped rather than refreshed.
    /// Populating L1 with the written value the way SetNx does would reopen the
    /// race the conditional write exists to close: the backplane subscriber drops
    /// L1 on its own task, so an invalidation applied between the L2 hop and the
    /// populate would be undone by the populate, and a read returns an L1 hit
    /// without consulting L2.
    /// </summary>
    [Fact]
    public async Task SetIfEqual_TheComparisonHeld_WritesL2AndLeavesNoL1Copy()
    {
        await using var ctx = NewTieredContext();
        await ctx.Tiered.SetAsync("k", "v1");

        var result = await ctx.Tiered.SetIfEqualAsync("k", "v1", "v2");

        result.Data.Should().BeTrue();
        (await ctx.L2.GetAsync<string>("k")).Data.Should().Be("v2");
        (await ctx.L1.ExistsAsync("k")).Data.Should().BeFalse();
    }

    [Fact]
    public async Task SetIfEqual_TheComparisonLost_LeavesL2AloneAndNoL1Copy()
    {
        await using var ctx = NewTieredContext();
        await ctx.Tiered.SetAsync("k", "v1");

        var result = await ctx.Tiered.SetIfEqualAsync("k", "not-what-is-stored", "v2");

        result.Data.Should().BeFalse();
        (await ctx.L2.GetAsync<string>("k")).Data.Should().Be("v1");
        (await ctx.L1.ExistsAsync("k")).Data.Should().BeFalse();
    }

    [Fact]
    public async Task SetIfEqual_AnAbsentKey_WritesNothing()
    {
        await using var ctx = NewTieredContext();

        var result = await ctx.Tiered.SetIfEqualAsync("absent", "v1", "v2");

        result.Data.Should().BeFalse();
        (await ctx.L2.ExistsAsync("absent")).Data.Should().BeFalse();
    }

    [Fact]
    public async Task SetIfEqual_TheNextRead_ComesBackFromL2()
    {
        // The local tier is empty afterwards, and a reader gets the value the
        // conditional write left in L2.
        await using var ctx = NewTieredContext();
        await ctx.Tiered.SetAsync("k", "v1");
        _ = await ctx.Tiered.SetIfEqualAsync("k", "v1", "v2");

        (await ctx.Tiered.GetAsync<string>("k")).Data.Should().Be("v2");
    }

    [Fact]
    public async Task RemoveIfEqual_TheComparisonHeld_RemovesFromL2AndLeavesNoL1Copy()
    {
        await using var ctx = NewTieredContext();
        await ctx.Tiered.SetAsync("k", "v1");

        var result = await ctx.Tiered.RemoveIfEqualAsync("k", "v1");

        result.Data.Should().BeTrue();
        (await ctx.L2.ExistsAsync("k")).Data.Should().BeFalse();
        (await ctx.L1.ExistsAsync("k")).Data.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveIfEqual_TheComparisonLost_LeavesL2AloneAndNoL1Copy()
    {
        // The local copy goes even where nothing was removed: the comparison
        // proves something about the shared value at the instant it ran and
        // nothing about a local copy a moment later.
        await using var ctx = NewTieredContext();
        await ctx.Tiered.SetAsync("k", "v1");

        var result = await ctx.Tiered.RemoveIfEqualAsync("k", "not-what-is-stored");

        result.Data.Should().BeFalse();
        (await ctx.L2.GetAsync<string>("k")).Data.Should().Be("v1");
        (await ctx.L1.ExistsAsync("k")).Data.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveIfEqual_AnAbsentKey_RemovesNothing()
    {
        await using var ctx = NewTieredContext();

        var result = await ctx.Tiered.RemoveIfEqualAsync("absent", "v1");

        result.IsOk.Should().BeTrue();
        result.Data.Should().BeFalse();
        (await ctx.L2.ExistsAsync("absent")).Data.Should().BeFalse();
    }

    /// <summary>
    /// Dropping the writing instance's own L1 is not enough: the write the
    /// comparison won replaced the value every other instance holds locally, so
    /// the key is published and the other instances drop their copies.
    /// </summary>
    [Fact]
    public async Task SetIfEqual_TheComparisonHeld_DropsTheOtherInstancesL1()
    {
        var channel = $"test-channel-{Guid.NewGuid():N}";
        var keyPrefix = $"test-{Guid.NewGuid():N}:";

        await using var instanceA = NewTieredContext(channel, keyPrefix);
        await using var instanceB = NewTieredContext(channel, keyPrefix);

        await instanceA.Tiered.SetAsync("k", "v1");
        await instanceB.L1.SetAsync("k", "v1");

        var wrote = await instanceA.Tiered.SetIfEqualAsync("k", "v1", "v2");
        wrote.Data.Should().BeTrue();

        var dropped = await WaitUntilAsync(
            async () => !(await instanceB.L1.ExistsAsync("k")).Data);

        dropped.Should().BeTrue("the broadcast reaches B within the attempt budget");
        (await instanceB.L1.ExistsAsync("k")).Data.Should().BeFalse(
            "the write superseded the value B was holding, so B was told to drop it");
        (await instanceB.Tiered.GetAsync<string>("k")).Data.Should().Be("v2");
    }

    /// <summary>
    /// A comparison that lost changed nothing at L2, so the other instances'
    /// copies are still current and nothing is published.
    /// </summary>
    [Fact]
    public async Task SetIfEqual_TheComparisonLost_LeavesTheOtherInstancesL1Alone()
    {
        var channel = $"test-channel-{Guid.NewGuid():N}";
        var keyPrefix = $"test-{Guid.NewGuid():N}:";

        await using var instanceA = NewTieredContext(channel, keyPrefix);
        await using var instanceB = NewTieredContext(channel, keyPrefix);

        await instanceA.Tiered.SetAsync("k", "v1");
        await instanceB.L1.SetAsync("k", "v1");

        var wrote = await instanceA.Tiered.SetIfEqualAsync("k", "not-what-is-stored", "v2");
        wrote.Data.Should().BeFalse();

        // Nothing to wait for on this arm, so the case waits for a message that
        // is published and reads the absence behind it: the channel delivers in
        // order, so B having acted on the probe means B has acted on anything
        // published before it.
        await DrainBroadcastAsync(instanceA, instanceB);

        (await instanceB.L1.GetAsync<string>("k")).Data.Should().Be(
            "v1", "nothing at the shared store moved, so B's copy still matches it");
    }

    /// <summary>
    /// The same rule for the conditional remove: the entry the comparison
    /// removed is gone from the shared store, so the other instances must not
    /// keep answering it from their own tier.
    /// </summary>
    [Fact]
    public async Task RemoveIfEqual_TheComparisonHeld_DropsTheOtherInstancesL1()
    {
        var channel = $"test-channel-{Guid.NewGuid():N}";
        var keyPrefix = $"test-{Guid.NewGuid():N}:";

        await using var instanceA = NewTieredContext(channel, keyPrefix);
        await using var instanceB = NewTieredContext(channel, keyPrefix);

        await instanceA.Tiered.SetAsync("k", "v1");
        await instanceB.L1.SetAsync("k", "v1");

        var removed = await instanceA.Tiered.RemoveIfEqualAsync("k", "v1");
        removed.Data.Should().BeTrue();

        var dropped = await WaitUntilAsync(
            async () => !(await instanceB.L1.ExistsAsync("k")).Data);

        dropped.Should().BeTrue("the broadcast reaches B within the attempt budget");
        (await instanceB.L1.ExistsAsync("k")).Data.Should().BeFalse();
        (await instanceB.Tiered.GetAsync<string>("k")).IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveIfEqual_TheNextRead_FindsNothing()
    {
        // A reader that would have returned the value from the local copy consults L2
        // instead, and L2 no longer holds the key.
        await using var ctx = NewTieredContext();
        await ctx.Tiered.SetAsync("k", "v1");
        _ = await ctx.Tiered.RemoveIfEqualAsync("k", "v1");

        (await ctx.Tiered.GetAsync<string>("k")).IsNotFound.Should().BeTrue();
    }

    /// <summary>
    /// Publishes one invalidation the observer must act on, and returns once it
    /// has. Every message published before it has been delivered by then, so a
    /// case asserting that nothing was published reads the absence after this
    /// rather than after an elapsed span.
    /// </summary>
    /// <param name="publisher">The instance that publishes the probe.</param>
    /// <param name="observer">The instance whose L1 the probe is read from.</param>
    /// <returns>A task that completes once the observer has acted.</returns>
    private static async Task DrainBroadcastAsync(
        TieredContextFixture publisher,
        TieredContextFixture observer)
    {
        var probe = $"broadcast-probe-{Guid.NewGuid():N}";
        await observer.L1.SetAsync(probe, "seen");
        await publisher.Tiered.RemoveAndBroadcastAsync(probe);

        var acted = await WaitUntilAsync(
            async () => !(await observer.L1.ExistsAsync(probe)).Data);

        acted.Should().BeTrue("the backplane delivers the probe within the attempt budget");
    }

    /// <summary>
    /// Polls a condition until it holds or the attempt budget runs out. The
    /// budget is sized so a healthy run never reaches it, so a run that does
    /// reach it is reporting the condition rather than a slow machine.
    /// </summary>
    /// <param name="condition">The condition to poll.</param>
    /// <returns>Whether the condition held.</returns>
    private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition)
    {
        for (var attempt = 0; attempt < _PROPAGATION_POLL_ATTEMPTS; attempt++)
        {
            if (await condition())
                return true;

            await Task.Delay(_PROPAGATION_POLL_GAP_MS);
        }

        return await condition();
    }

    [MustDisposeResource(false)]
    private TieredContextFixture NewTieredContext(string? channel = null, string? keyPrefix = null)
    {
        channel ??= $"test-channel-{Guid.NewGuid():N}";
        keyPrefix ??= $"test-{Guid.NewGuid():N}:";

        var redis = ConnectionMultiplexer.Connect(r_fixture.ConnectionString);
        var redisOpts = Options.Create(new RedisCacheOptions
        {
            ConnectionString = r_fixture.ConnectionString,
            KeyPrefix = keyPrefix,
            InvalidationChannel = channel,
            DefaultExpiration = TimeSpan.FromMinutes(5),
        });

        var backplane = new RedisCacheInvalidationBackplane(
            redis,
            redisOpts,
            NullLogger<RedisCacheInvalidationBackplane>.Instance);

        var l1 = new DefaultLocalCache(Options.Create(new LocalCacheOptions
        {
            DefaultExpiration = TimeSpan.FromMinutes(5),
        }));

        var l2 = new RedisDistributedCache(
            redis,
            redisOpts,
            new JsonCacheSerializer(),
            NullLogger<RedisDistributedCache>.Instance,
            backplane);

        var tiered = new DefaultTieredCache(
            l1,
            l2,
            NullLogger<DefaultTieredCache>.Instance,
            backplane);

        return new TieredContextFixture(redis, l1, l2, tiered, backplane);
    }

    [MustDisposeResource(false)]
    private sealed class TieredContextFixture : IAsyncDisposable
    {
        private readonly ConnectionMultiplexer r_redis;
        private readonly DefaultLocalCache r_l1;
        private readonly RedisCacheInvalidationBackplane r_backplane;
        private readonly DefaultTieredCache r_tiered;

        [MustDisposeResource(false)]
        internal TieredContextFixture(
            ConnectionMultiplexer redis,
            DefaultLocalCache l1,
            RedisDistributedCache l2,
            DefaultTieredCache tiered,
            RedisCacheInvalidationBackplane backplane)
        {
            r_redis = redis;
            r_l1 = l1;
            L2 = l2;
            r_tiered = tiered;
            r_backplane = backplane;
        }

        internal ILocalCache L1 => r_l1;

        internal RedisDistributedCache L2 { get; }

        internal ITieredCache Tiered => r_tiered;

        public async ValueTask DisposeAsync()
        {
            await r_tiered.DisposeAsync();
            await r_backplane.DisposeAsync();
            r_l1.Dispose();
            await r_redis.DisposeAsync();
        }
    }
}
