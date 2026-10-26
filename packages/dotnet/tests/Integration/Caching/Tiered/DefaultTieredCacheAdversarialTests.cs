// -----------------------------------------------------------------------
// <copyright file="DefaultTieredCacheAdversarialTests.cs" company="DCSV">
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
using DcsvIo.D2.Tests.Integration.Caching.Tiered.Fixtures;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

/// <summary>
/// Adversarial / chaos tests for <c>DefaultTieredCache</c>. Cluster-wide
/// scenarios with multiple instances, concurrent ops, and races between
/// L1 / L2 / backplane.
/// </summary>
[Collection("Redis")]
public sealed class DefaultTieredCacheAdversarialTests
{
    // A 100ms lifetime is gone on the first or second read of a healthy run;
    // the budget is sized so a passing run never reaches it.
    private const int _EXPIRY_POLL_ATTEMPTS = 50;
    private const int _EXPIRY_POLL_GAP_MS = 20;

    // A Redis pub/sub round trip lands on the first or second read of a healthy
    // run; the budget is sized so a passing run never reaches it.
    private const int _PROPAGATION_POLL_ATTEMPTS = 50;
    private const int _PROPAGATION_POLL_GAP_MS = 20;

    private readonly RedisFixture r_fixture;

    public DefaultTieredCacheAdversarialTests(RedisFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
    public async Task TwoInstances_ConcurrentSetAndBroadcast_FinalStateConsistent()
    {
        // Two replicas. Both fire SetAndBroadcast on the same key with
        // different values. Final L2 value: one of the two (last-writer-wins).
        // After all broadcasts settle, both instances' L1 must agree with L2.
        var channel = $"chaos-{Guid.NewGuid():N}";
        var keyPrefix = $"chaos-{Guid.NewGuid():N}:";

        await using var a = NewTieredContext(channel, keyPrefix);
        await using var b = NewTieredContext(channel, keyPrefix);

        // Fire 50 sets from each instance concurrently.
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 100),
            new ParallelOptions { MaxDegreeOfParallelism = 16 },
            async (i, ct) =>
            {
                var instance = i % 2 == 0 ? a : b;
                await instance.Tiered.SetAndBroadcastAsync("k", $"value-{i}", ct: ct);
            });

        // L2 has the final value (whichever was last).
        var l2Final = await a.L2.GetAsync<string>("k");
        l2Final.IsOk.Should().BeTrue();
        var canonical = l2Final.Data;

        // Poll until the broadcasts drain. An L1 copy no broadcast dropped
        // keeps disagreeing, so the budget runs out and the assertion fails.
        var agreed = await WaitUntilAsync(async () =>
            (await a.Tiered.GetAsync<string>("k")).Data == canonical
            && (await b.Tiered.GetAsync<string>("k")).Data == canonical);

        agreed.Should().BeTrue("every broadcast reaches both instances within the budget");

        // Both instances' tiered Get should agree (they fall through to L2 on miss
        // since broadcast invalidated their L1).
        var aGet = await a.Tiered.GetAsync<string>("k");
        var bGet = await b.Tiered.GetAsync<string>("k");
        aGet.Data.Should().Be(canonical);
        bGet.Data.Should().Be(canonical);
    }

    [Fact]
    public async Task TwoInstances_RemoveOnA_BReadsNotFound()
    {
        // Sequential test of cross-instance remove + broadcast.
        var channel = $"chaos-{Guid.NewGuid():N}";
        var keyPrefix = $"chaos-{Guid.NewGuid():N}:";

        await using var a = NewTieredContext(channel, keyPrefix);
        await using var b = NewTieredContext(channel, keyPrefix);

        // Both have the value cached in L1 + L2 (via initial Set on A).
        await a.Tiered.SetAsync("k", "shared");
        await b.Tiered.GetAsync<string>("k");  // populates B's L1 from L2

        // A removes + broadcasts.
        await a.Tiered.RemoveAndBroadcastAsync("k");

        var dropped = await WaitUntilAsync(async () =>
            (await b.Tiered.GetAsync<string>("k")).IsNotFound);

        dropped.Should().BeTrue("the broadcast reaches B within the budget");

        // B's tiered Get -> L1 was dropped by broadcast -> L2 miss -> NotFound.
        var bGet = await b.Tiered.GetAsync<string>("k");
        bGet.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task ThreeInstances_ChainedInvalidation_AllAgree()
    {
        // 3-instance scenario simulating a small cluster. Instance A writes,
        // B and C read (populate their L1), then A writes again with broadcast.
        // After settling, the L1 of A, of B and of C must each agree with L2.
        var channel = $"chaos-{Guid.NewGuid():N}";
        var keyPrefix = $"chaos-{Guid.NewGuid():N}:";

        await using var a = NewTieredContext(channel, keyPrefix);
        await using var b = NewTieredContext(channel, keyPrefix);
        await using var c = NewTieredContext(channel, keyPrefix);

        await a.Tiered.SetAsync("k", "v1");
        await b.Tiered.GetAsync<string>("k");  // B's L1 = v1
        await c.Tiered.GetAsync<string>("k");  // C's L1 = v1

        await a.Tiered.SetAndBroadcastAsync("k", "v2");

        var settled = await WaitUntilAsync(async () =>
            (await b.Tiered.GetAsync<string>("k")).Data == "v2"
            && (await c.Tiered.GetAsync<string>("k")).Data == "v2");

        settled.Should().BeTrue("the broadcast reaches B and C within the budget");

        // All three must see v2 on next read.
        var aGet = await a.Tiered.GetAsync<string>("k");
        var bGet = await b.Tiered.GetAsync<string>("k");
        var cGet = await c.Tiered.GetAsync<string>("k");

        aGet.Data.Should().Be("v2");
        bGet.Data.Should().Be("v2");
        cGet.Data.Should().Be("v2");
    }

    [Fact]
    public async Task GetMany_PartialL1_FallsThroughToL2_PopulatesL1()
    {
        // L1 has {a, b}; L2 has {a, b, c, d, e}. GetMany([a,b,c,d,e]) should
        // return all 5, populate L1 with {c, d, e}, and verify L1 now has all.
        await using var ctx = NewTieredContext(
            $"chaos-{Guid.NewGuid():N}",
            $"chaos-{Guid.NewGuid():N}:");

        // Seed L2 with all 5.
        for (var i = 0; i < 5; i++)
            await ctx.L2.SetAsync($"k{i}", $"v{i}");

        // Seed L1 with first 2.
        await ctx.L1.SetAsync("k0", "v0");
        await ctx.L1.SetAsync("k1", "v1");

        var get = await ctx.Tiered.GetManyAsync<string>(new[] { "k0", "k1", "k2", "k3", "k4" });

        get.IsOk.Should().BeTrue();
        get.Data!.Should().HaveCount(5);
        for (var i = 0; i < 5; i++)
            get.Data[$"k{i}"].Should().Be($"v{i}");

        // L1 now has all 5 (populate-on-L2-hit).
        for (var i = 0; i < 5; i++)
        {
            var l1 = await ctx.L1.GetAsync<string>($"k{i}");
            l1.IsOk.Should().BeTrue();
        }
    }

    [Fact]
    public async Task IncrementAsync_AcrossInstances_AggregatesAtomically()
    {
        // Two instances both incrementing the same counter via tiered.
        // Routes through L2 (cluster source of truth). Final L2 value
        // must equal total increments across both instances.
        var channel = $"chaos-{Guid.NewGuid():N}";
        var keyPrefix = $"chaos-{Guid.NewGuid():N}:";

        await using var a = NewTieredContext(channel, keyPrefix);
        await using var b = NewTieredContext(channel, keyPrefix);

        const int perInstance = 100;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 2 * perInstance),
            new ParallelOptions { MaxDegreeOfParallelism = 16 },
            async (i, ct) =>
            {
                var instance = i % 2 == 0 ? a : b;
                await instance.Tiered.IncrementAsync("counter", ct: ct);
            });

        // Verify directly on L2.
        var l2Final = await a.L2.GetAsync<long>("counter");
        l2Final.IsOk.Should().BeTrue();
        l2Final.Data.Should().Be(2 * perInstance);
    }

    [Fact]
    public async Task SetAndBroadcastAsync_BroadcastFails_ReturnsFailure_DataInL2()
    {
        // If the set succeeds and the broadcast fails, the call returns the broadcast
        // failure; the value is in L2, and other instances do not learn to invalidate.
        var backplane = new FailingBackplaneStub();
        await using var redis = await ConnectionMultiplexer.ConnectAsync(
            r_fixture.ConnectionString);
        using var l1 = new DefaultLocalCache(Options.Create(new LocalCacheOptions
        {
            DefaultExpiration = TimeSpan.FromMinutes(5),
        }));
        var l2 = NewL2WithBackplane(redis, backplane);
        await using var tiered = new DefaultTieredCache(
            l1,
            l2,
            NullLogger<DefaultTieredCache>.Instance,
            backplane);

        var result = await tiered.SetAndBroadcastAsync("k", "v");
        var stored = await l2.GetAsync<string>("k");

        (result.IsServiceUnavailable, stored.Data).Should().Be((true, "v"));
    }

    [Fact]
    public async Task SetAndBroadcastAsync_NoBackplane_Throws()
    {
        await using var ctx = NewTieredContextNoBackplane();

        // ReSharper disable once AccessToDisposedClosure -- act is awaited inside
        // ThrowAsync, before ctx disposes.
        var act = async () => await ctx.Tiered.SetAndBroadcastAsync("k", "v");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task TieredCache_DisposedAndReused_HandlesGracefully()
    {
        // Dispose tiered cache -> its backplane subscription is removed.
        // Verify no NPE / no crash on subsequent ops.
        var channel = $"chaos-{Guid.NewGuid():N}";
        var keyPrefix = $"chaos-{Guid.NewGuid():N}:";
        var ctx = NewTieredContext(channel, keyPrefix);

        await ctx.Tiered.SetAsync("k", "v");
        await ctx.DisposeAsync();

        // After dispose, the underlying caches are gone too, so ctx must not be
        // used again. The test verifies that disposal itself does not throw.
    }

    [Fact]
    public async Task Tiered_SetAsyncWithVeryShortTtl_ExpiresOnBothTiers()
    {
        // A 100ms lifetime on both tiers. The reads are polled rather than
        // taken after an elapsed span, so a slow machine costs an extra poll
        // rather than a red case.
        await using var ctx = NewTieredContext(
            $"chaos-{Guid.NewGuid():N}",
            $"chaos-{Guid.NewGuid():N}:");

        await ctx.Tiered.SetAsync("k", "v", TimeSpan.FromMilliseconds(100));

        var gone = false;

        for (var attempt = 0; attempt < _EXPIRY_POLL_ATTEMPTS && !gone; attempt++)
        {
            gone = (await ctx.L1.GetAsync<string>("k")).IsNotFound
                && (await ctx.L2.GetAsync<string>("k")).IsNotFound;

            if (!gone)
                await Task.Delay(_EXPIRY_POLL_GAP_MS);
        }

        gone.Should().BeTrue("a 100ms lifetime expires on both tiers");
        (await ctx.Tiered.GetAsync<string>("k")).IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task Tiered_NullValue_RoundTripsThroughBothTiers()
    {
        // Cache a null value via tiered. Both L1 and L2 should hold "null"
        // (not absent - present-with-null). GetAsync<string?> should return
        // Ok(null), not NotFound.
        await using var ctx = NewTieredContext(
            $"chaos-{Guid.NewGuid():N}",
            $"chaos-{Guid.NewGuid():N}:");

        await ctx.Tiered.SetAsync<string?>("k", null);

        var get = await ctx.Tiered.GetAsync<string?>("k");
        get.IsOk.Should().BeTrue();
        get.Data.Should().BeNull();

        // Both tiers should report Exists=true.
        (await ctx.L1.ExistsAsync("k")).Data.Should().BeTrue();
        (await ctx.L2.ExistsAsync("k")).Data.Should().BeTrue();
    }

    /// <summary>
    /// The interleaving the member exists to close. A holds a live copy in L1 and
    /// L2; B revokes it and broadcasts; A then conditionally writes back the copy
    /// it read. The comparison is decided at L2, which B has already changed, so
    /// A writes nothing and B's value stands.
    /// </summary>
    [Fact]
    public async Task SetIfEqual_ARevocationLandingFirst_LeavesTheRevocationStanding()
    {
        var channel = $"chaos-{Guid.NewGuid():N}";
        var keyPrefix = $"chaos-{Guid.NewGuid():N}:";

        await using var a = NewTieredContext(channel, keyPrefix);
        await using var b = NewTieredContext(channel, keyPrefix);

        await a.Tiered.SetAsync("k", "live");
        var readByA = (await a.Tiered.GetAsync<string>("k")).Data;

        await b.Tiered.SetAndBroadcastAsync("k", "revoked");
        var wrote = await a.Tiered.SetIfEqualAsync("k", readByA, "live-again");

        wrote.Data.Should().BeFalse();
        (await b.L2.GetAsync<string>("k")).Data.Should().Be("revoked");
    }

    [Fact]
    public async Task SetIfEqual_ARevocationLandingFirst_LeavesNoLiveCopyOnTheLosingNode()
    {
        // The losing node must not keep returning values from its own L1 either: the
        // comparison proved something about the shared value and nothing about a
        // local copy, so the local copy goes on every outcome.
        var channel = $"chaos-{Guid.NewGuid():N}";
        var keyPrefix = $"chaos-{Guid.NewGuid():N}:";

        await using var a = NewTieredContext(channel, keyPrefix);
        await using var b = NewTieredContext(channel, keyPrefix);

        await a.Tiered.SetAsync("k", "live");
        var readByA = (await a.Tiered.GetAsync<string>("k")).Data;

        await b.Tiered.SetAndBroadcastAsync("k", "revoked");
        _ = await a.Tiered.SetIfEqualAsync("k", readByA, "live-again");

        (await a.L1.ExistsAsync("k")).Data.Should().BeFalse();
        (await a.Tiered.GetAsync<string>("k")).Data.Should().Be("revoked");
    }

    /// <summary>
    /// The admitting case beside it: with nothing racing, the same call writes.
    /// Without this the refusing test above would pass against an implementation
    /// whose conditional write never lands at all.
    /// </summary>
    [Fact]
    public async Task SetIfEqual_NothingRacingIt_Writes()
    {
        var channel = $"chaos-{Guid.NewGuid():N}";
        var keyPrefix = $"chaos-{Guid.NewGuid():N}:";

        await using var a = NewTieredContext(channel, keyPrefix);

        await a.Tiered.SetAsync("k", "live");
        var readByA = (await a.Tiered.GetAsync<string>("k")).Data;

        var wrote = await a.Tiered.SetIfEqualAsync("k", readByA, "live-again");

        wrote.Data.Should().BeTrue();
        (await a.L2.GetAsync<string>("k")).Data.Should().Be("live-again");
    }

    /// <summary>
    /// Polls a condition until it holds or the attempt budget runs out. The
    /// budget is sized so a healthy run never reaches it; reaching it means the
    /// condition did not hold, not that the machine was slow.
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
    private TieredContextFixture NewTieredContext(string channel, string keyPrefix)
    {
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

    private RedisDistributedCache NewL2WithBackplane(
        IConnectionMultiplexer redis,
        ICacheInvalidationBackplane backplane)
        => new(
            redis,
            Options.Create(new RedisCacheOptions
            {
                ConnectionString = r_fixture.ConnectionString,
                KeyPrefix = $"failing-bp-{Guid.NewGuid():N}:",
                DefaultExpiration = TimeSpan.FromMinutes(5),
            }),
            new JsonCacheSerializer(),
            NullLogger<RedisDistributedCache>.Instance,
            backplane);

    [MustDisposeResource(false)]
    private TieredContextFixture NewTieredContextNoBackplane()
    {
        var redis = ConnectionMultiplexer.Connect(r_fixture.ConnectionString);
        var redisOpts = Options.Create(new RedisCacheOptions
        {
            ConnectionString = r_fixture.ConnectionString,
            KeyPrefix = $"no-bp-{Guid.NewGuid():N}:",
            DefaultExpiration = TimeSpan.FromMinutes(5),
        });
        var l1 = new DefaultLocalCache(Options.Create(new LocalCacheOptions
        {
            DefaultExpiration = TimeSpan.FromMinutes(5),
        }));
        var l2 = new RedisDistributedCache(
            redis,
            redisOpts,
            new JsonCacheSerializer(),
            NullLogger<RedisDistributedCache>.Instance,
            backplane: null);
        var tiered = new DefaultTieredCache(
            l1,
            l2,
            NullLogger<DefaultTieredCache>.Instance,
            backplane: null);
        return new TieredContextFixture(redis, l1, l2, tiered, backplane: null);
    }

    [MustDisposeResource(false)]
    private sealed class TieredContextFixture : IAsyncDisposable
    {
        private readonly ConnectionMultiplexer r_redis;
        private readonly DefaultLocalCache r_l1;
        private readonly RedisCacheInvalidationBackplane? r_backplane;
        private readonly DefaultTieredCache r_tiered;

        [MustDisposeResource(false)]
        internal TieredContextFixture(
            ConnectionMultiplexer redis,
            DefaultLocalCache l1,
            RedisDistributedCache l2,
            DefaultTieredCache tiered,
            RedisCacheInvalidationBackplane? backplane)
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
            if (r_backplane is not null)
                await r_backplane.DisposeAsync();
            r_l1.Dispose();
            await r_redis.DisposeAsync();
        }
    }
}
