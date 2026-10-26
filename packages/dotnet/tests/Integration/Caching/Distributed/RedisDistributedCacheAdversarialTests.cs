// -----------------------------------------------------------------------
// <copyright file="RedisDistributedCacheAdversarialTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Caching.Distributed;

using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Tests.Integration.Caching.Distributed.Fixtures;
using JetBrains.Annotations;
using Xunit;

/// <summary>
/// Adversarial / stress tests for <c>RedisDistributedCache</c>. Each
/// scenario probes a known-hazardous code path or race shape and fails if a
/// regression re-introduces the hazard.
/// </summary>
[Collection("Redis")]
public sealed class RedisDistributedCacheAdversarialTests
{
    // The expiry cases poll on this budget. The longest lifetime they wait out
    // is 150 ms, about eight polls, so a passing run never reaches the budget.
    private const int _EXPIRY_POLL_ATTEMPTS = 50;
    private const int _EXPIRY_POLL_GAP_MS = 20;

    private readonly RedisFixture r_fixture;

    public RedisDistributedCacheAdversarialTests(RedisFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
    public async Task IncrementAsync_ConcurrentRemovesAndIncrements_NeverThrows()
    {
        // Mix Increment + Remove on the same key from multiple threads.
        // Increments should always succeed (key gets re-created on first
        // increment after a remove). No crashes, no garbage data.
        await using var ctx = NewCache();
        var increments = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 16),
            new ParallelOptions { MaxDegreeOfParallelism = 16 },
            async (worker, ct) =>
            {
                for (var i = 0; i < 100; i++)
                {
                    if (worker % 4 == 0)
                    {
                        await ctx.Cache.RemoveAsync("counter", ct);
                    }
                    else
                    {
                        var inc = await ctx.Cache.IncrementAsync("counter", ct: ct);
                        inc.IsOk.Should().BeTrue(
                            "Increment should never throw or fail under contention");
                        Interlocked.Increment(ref increments);
                    }
                }
            });

        // The test cannot assert the exact counter value (Removes interleave) but
        // increments must have been processed without crashing.
        increments.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AcquireLockAsync_ExpiredLock_AllowsReacquisition()
    {
        // Owner A acquires with short TTL. After TTL elapses, owner B should
        // be able to acquire. Verifies Redis honors PX expiration.
        await using var ctx = NewCache();
        var first = await ctx.Cache.AcquireLockAsync(
            "lock-k", "owner-A", TimeSpan.FromMilliseconds(150));
        first.Data.Should().BeTrue();

        var acquired = false;

        for (var attempt = 0; attempt < _EXPIRY_POLL_ATTEMPTS && !acquired; attempt++)
        {
            acquired = (await ctx.Cache.AcquireLockAsync(
                "lock-k", "owner-B", TimeSpan.FromSeconds(30))).Data;

            if (!acquired)
                await Task.Delay(_EXPIRY_POLL_GAP_MS);
        }

        acquired.Should().BeTrue("the first owner's lock expires after its TTL");
    }

    [Fact]
    public async Task AcquireLockAsync_HighContention_SerialAccess()
    {
        // 50 contenders try to acquire the same lock with a long TTL, and no
        // contender releases it. At any moment at most one acquirer holds the
        // lock, so counting the successful AcquireLock returns under the long
        // TTL must give exactly 1.
        await using var ctx = NewCache();
        var winners = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 50),
            new ParallelOptions { MaxDegreeOfParallelism = 50 },
            async (i, ct) =>
            {
                var acquired = await ctx.Cache.AcquireLockAsync(
                    "lock-k", $"owner-{i}", TimeSpan.FromSeconds(30), ct);
                if (acquired.Data)
                    Interlocked.Increment(ref winners);
            });

        winners.Should().Be(1, "exactly one contender wins the lock; others see it busy");
    }

    [Fact]
    public async Task SetAsync_LargeValue_RoundTrips()
    {
        // 1 MB value. Redis default config supports this. Verify our serializer
        // and Redis transport handle it without truncation.
        await using var ctx = NewCache();
        var large = new string('x', 1_000_000);

        var setResult = await ctx.Cache.SetAsync("big-k", large);
        setResult.IsOk.Should().BeTrue();

        var get = await ctx.Cache.GetAsync<string>("big-k");
        get.IsOk.Should().BeTrue();
        get.Data!.Length.Should().Be(1_000_000);
    }

    [Fact]
    public async Task SetAddAsync_HighCardinality_HandlesScale()
    {
        // 10K distinct members added concurrently. Cardinality math must be
        // exact - Redis SADD is bounded only by memory.
        await using var ctx = NewCache();
        const int distinctMembers = 10_000;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, distinctMembers),
            new ParallelOptions { MaxDegreeOfParallelism = 32 },
            async (i, ct) =>
            {
                await ctx.Cache.SetAddAsync("big-set", $"member-{i}", ct: ct);
            });

        var card = await ctx.Cache.SetCardinalityAsync("big-set");
        card.Data.Should().Be(distinctMembers);
    }

    [Fact]
    public async Task SetAddAsync_DuplicateMembers_NotDoubleCounted()
    {
        // Same member added 100 times - cardinality must stay at 1.
        await using var ctx = NewCache();
        for (var i = 0; i < 100; i++)
            await ctx.Cache.SetAddAsync("k", "the-same-member");

        var card = await ctx.Cache.SetCardinalityAsync("k");
        card.Data.Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_NullCachedValue_RoundTrips()
    {
        // Caching a null value - Set<string?>(k, null) -> Get<string?>(k)
        // should return Ok(null), not NotFound and not a serialization failure.
        // Pins the JsonCacheSerializer null handling.
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync<string?>("k", null);

        var get = await ctx.Cache.GetAsync<string?>("k");
        get.IsOk.Should().BeTrue();
        get.Data.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_AfterRemove_ReturnsNotFound()
    {
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("k", "v");
        await ctx.Cache.RemoveAsync("k");

        var get = await ctx.Cache.GetAsync<string>("k");
        get.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task SetNxAsync_NullValue_StillEnforcesAtomicity()
    {
        // Edge: SetNx<string?>(k, null). The "absence" of a key vs the
        // "presence of null" must be distinguished - SetNx should write
        // null on first call, return false on second call.
        await using var ctx = NewCache();
        var first = await ctx.Cache.SetNxAsync<string?>("k", null);
        first.Data.Should().BeTrue("first SetNx writes the null value");

        var second = await ctx.Cache.SetNxAsync<string?>("k", "something else");
        second.Data.Should().BeFalse("second SetNx sees the key exists (even with null value)");
    }

    [Fact]
    public async Task IncrementAsync_LargeAmount_HandledCorrectly()
    {
        // Increment by a large value - Redis INCR is 64-bit signed.
        // Anything within long.MaxValue should work.
        await using var ctx = NewCache();
        var inc = await ctx.Cache.IncrementAsync("big-counter", 1_000_000_000_000L);
        inc.IsOk.Should().BeTrue();
        inc.Data.Should().Be(1_000_000_000_000L);
    }

    [Fact]
    public async Task IncrementAsync_NegativeAmount_DecrementsCorrectly()
    {
        await using var ctx = NewCache();
        await ctx.Cache.IncrementAsync("k", 100);
        var dec = await ctx.Cache.IncrementAsync("k", -30);
        dec.Data.Should().Be(70);
    }

    [Fact]
    public async Task SetMany_SomeEntries_AllPresentAfterwards()
    {
        await using var ctx = NewCache();
        var entries = Enumerable.Range(0, 50).ToDictionary(i => $"k{i}", i => $"v{i}");

        await ctx.Cache.SetManyAsync(entries);

        var get = await ctx.Cache.GetManyAsync<string>(entries.Keys.ToList());
        get.IsOk.Should().BeTrue();
        get.Data!.Count.Should().Be(50);
    }

    [Fact]
    public async Task RemoveMany_RemovesAllSpecified()
    {
        await using var ctx = NewCache();
        for (var i = 0; i < 10; i++)
            await ctx.Cache.SetAsync($"k{i}", i);

        await ctx.Cache.RemoveManyAsync(Enumerable.Range(0, 10).Select(i => $"k{i}").ToList());

        for (var i = 0; i < 10; i++)
        {
            var exists = await ctx.Cache.ExistsAsync($"k{i}");
            exists.Data.Should().BeFalse();
        }
    }

    [Fact]
    public async Task GetTtlAsync_ExpiredKey_ReturnsNotFound()
    {
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("k", "v", TimeSpan.FromMilliseconds(100));

        var notFound = false;

        for (var attempt = 0; attempt < _EXPIRY_POLL_ATTEMPTS && !notFound; attempt++)
        {
            notFound = (await ctx.Cache.GetTtlAsync("k")).IsNotFound;

            if (!notFound)
                await Task.Delay(_EXPIRY_POLL_GAP_MS);
        }

        notFound.Should().BeTrue("an expired key has no TTL to report");
    }

    [Fact]
    public async Task SetCardinalityAsync_AbsentKey_ReturnsZero()
    {
        await using var ctx = NewCache();
        var card = await ctx.Cache.SetCardinalityAsync("never-existed");
        card.IsOk.Should().BeTrue();
        card.Data.Should().Be(0);
    }

    [Fact]
    public async Task SetContainsAsync_AbsentKey_ReturnsFalse()
    {
        await using var ctx = NewCache();
        var contains = await ctx.Cache.SetContainsAsync("never-existed", "any");
        contains.IsOk.Should().BeTrue();
        contains.Data.Should().BeFalse();
    }

    [Fact]
    public async Task MixedOps_HighConcurrency_NoExceptions()
    {
        // 32 threads x 100 iterations doing random ops on a small key space.
        // Probes for any unhandled exception, deadlock, or torn state under
        // realistic mixed contention.
        await using var ctx = NewCache();
        const int keyCount = 10;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 32),
            new ParallelOptions { MaxDegreeOfParallelism = 32 },
            async (_, ct) =>
            {
                for (var iter = 0; iter < 100; iter++)
                {
                    // Random.Shared is thread-safe, so the workers need no
                    // per-thread seeded instances.
                    var key = $"k{Random.Shared.Next(keyCount)}";
                    var op = Random.Shared.Next(8);
                    switch (op)
                    {
                        case 0:
                            await ctx.Cache.GetAsync<int>(key, ct);
                            break;
                        case 1:
                            await ctx.Cache.SetAsync(key, iter, TimeSpan.FromMinutes(5), ct);
                            break;
                        case 2:
                            await ctx.Cache.RemoveAsync(key, ct);
                            break;
                        case 3:
                            await ctx.Cache.SetNxAsync(key, iter, TimeSpan.FromMinutes(5), ct);
                            break;
                        case 4:
                            await ctx.Cache.IncrementAsync($"counter-{key}", ct: ct);
                            break;
                        case 5:
                            await ctx.Cache.SetAddAsync($"set-{key}", $"member-{iter}", ct: ct);
                            break;
                        case 6:
                            await ctx.Cache.ExistsAsync(key, ct);
                            break;
                        case 7:
                            await ctx.Cache.GetTtlAsync(key, ct);
                            break;
                    }
                }
            });
    }

    /// <summary>
    /// Many callers hold the same expected value and exactly one write takes.
    /// The script compares and writes inside one Redis
    /// evaluation, so a losing caller cannot slip its write between another
    /// caller's compare and its set.
    /// </summary>
    [Fact]
    public async Task SetIfEqualAsync_ManyCallersFromOneExpectedValue_LetExactlyOneWin()
    {
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("cas-race", 0);

        var won = 0;
        await Parallel.ForEachAsync(
            Enumerable.Range(1, 24),
            async (i, ct) =>
            {
                if ((await ctx.Cache.SetIfEqualAsync("cas-race", 0, i, ct: ct)).Data)
                    _ = Interlocked.Increment(ref won);
            });

        won.Should().Be(1);
    }

    [Fact]
    public async Task SetIfEqualAsync_ManyCallersFromOneExpectedValue_LeaveTheWinnersValue()
    {
        // The call that succeeds stores its own value, so a script that wrote
        // nothing at all fails here even though it passes the refusal case.
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("cas-race-value", 0);

        var winner = 0;
        await Parallel.ForEachAsync(
            Enumerable.Range(1, 24),
            async (i, ct) =>
            {
                if ((await ctx.Cache.SetIfEqualAsync("cas-race-value", 0, i, ct: ct)).Data)
                    _ = Interlocked.Exchange(ref winner, i);
            });

        (await ctx.Cache.GetAsync<int>("cas-race-value")).Data.Should().Be(winner);
    }

    [Fact]
    public async Task SetIfEqualAsync_ARemoveBetweenTheReadAndTheWrite_WritesNothing()
    {
        // Once the key is gone, a caller holding a copy of what used to be
        // stored has nothing to compare against. Re-creating the key would
        // restore a value that the next reader must find absent.
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("cas-removed", "v1");
        await ctx.Cache.RemoveAsync("cas-removed");

        var result = await ctx.Cache.SetIfEqualAsync("cas-removed", "v1", "v2");

        result.Data.Should().BeFalse();
        (await ctx.Cache.ExistsAsync("cas-removed")).Data.Should().BeFalse();
    }

    [Fact]
    public async Task SetIfEqualAsync_ASubMillisecondExpiration_WritesAnEntryThatStillExpires()
    {
        // A positive lifetime under one millisecond truncates to zero whole
        // milliseconds, and the script reads zero as "no expiration" - which
        // would leave the entry in a store that persists to disk and to
        // backups. The local flavor expires the same input at once.
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("cas-sub-ms", "v1");

        var written = await ctx.Cache.SetIfEqualAsync(
            "cas-sub-ms",
            "v1",
            "v2",
            TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond / 2));

        written.Data.Should().BeTrue("the compare matched, so the write took");

        var gone = false;

        for (var attempt = 0; attempt < _EXPIRY_POLL_ATTEMPTS && !gone; attempt++)
        {
            gone = !(await ctx.Cache.ExistsAsync("cas-sub-ms")).Data;

            if (!gone)
                await Task.Delay(_EXPIRY_POLL_GAP_MS);
        }

        gone.Should().BeTrue(
            "a positive expiration must never reach the script's no-expiration arm");
    }

    [Fact]
    public async Task IncrementAsync_ANonPositiveExpiration_LeavesTheKeyWithoutOne()
    {
        // The other side of the millisecond floor: zero and below mean no
        // expiration, which is the arm the script reads as '0', and the floor
        // must not turn that into a one-millisecond entry.
        await using var ctx = NewCache();

        var incremented = await ctx.Cache.IncrementAsync("counter-no-ttl", 1, TimeSpan.Zero);

        incremented.Data.Should().Be(1);
        (await ctx.Cache.GetTtlAsync("counter-no-ttl")).Data.Should().BeNull(
            "a non-positive expiration asks for none");
    }

    [MustDisposeResource(false)]
    private CacheContextFixture NewCache()
    {
        var prefix = $"adversarial-{Guid.NewGuid():N}:";
        return new CacheContextFixture(r_fixture.ConnectionString, prefix);
    }
}
