// -----------------------------------------------------------------------
// <copyright file="RedisDistributedCacheTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Caching.Distributed;

using AwesomeAssertions;
using DcsvIo.D2.Caching.Distributed.Redis;
using DcsvIo.D2.Tests.Integration.Caching.Distributed.Fixtures;
using DcsvIo.D2.Tests.Unit.Caching.Local.Fixtures;
using JetBrains.Annotations;
using StackExchange.Redis;
using Xunit;

/// <summary>
/// End-to-end tests for <c>RedisDistributedCache</c> against a real
/// Redis container. Covers the full op surface (basic / atomic / set /
/// broadcast). Each test uses its own key prefix to stay isolated from
/// neighbors in the shared fixture.
/// </summary>
[Collection("Redis")]
public sealed class RedisDistributedCacheTests
{
    private readonly RedisFixture r_fixture;

    public RedisDistributedCacheTests(RedisFixture fixture)
    {
        r_fixture = fixture;
    }

    [Theory]
    [InlineData(1000, 1000, 0)]
    [InlineData(1, 3000, 1000)]
    public async Task ConsumeToken_ClockRecoversFromRollback_PreservesAccrual(
        long initialAt, long interval, long remainder)
    {
        await using var ctx = NewCache();
        var key = $"rollback-{Guid.NewGuid():N}";
        await ctx.Database.HashSetAsync(
            key,
            [new HashEntry("tokens", 0), new HashEntry("at", initialAt),
                new HashEntry("rem", remainder)]);

        // Only TIME is substituted; Redis executes the production Lua arithmetic and writes.
        var script = """
            local server = redis
            local redis = {call = function(command, ...)
                if command == 'TIME' then return {0, tonumber(ARGV[5]) * 1000} end
                return server.call(command, ...)
            end}
            """ + "\n" + RedisLuaScripts.CONSUME_TOKEN;

        var rollback = await ctx.Database.ScriptEvaluateAsync(
            script, [key], [1, interval, 1, 1, 0]);
        ((long[])rollback!)[0].Should().Be(0);
        var recovered = await ctx.Database.ScriptEvaluateAsync(
            script, [key], [1, interval, 1, 1, initialAt]);
        ((long[])recovered!)[0].Should().Be(0);
        ((long)await ctx.Database.HashGetAsync(key, "at")).Should().Be(initialAt);
        ((long)await ctx.Database.HashGetAsync(key, "rem")).Should().Be(remainder);

        var refillAt = initialAt + interval - (remainder / 1000);
        var before = await ctx.Database.ScriptEvaluateAsync(
            script, [key], [1, interval, 1, 1, refillAt - 1]);
        ((long[])before!).Should().Equal(0, 1);
        var refilled = await ctx.Database.ScriptEvaluateAsync(
            script, [key], [1, interval, 1, 1, refillAt]);
        ((long[])refilled!).Should().Equal(1, 0);
    }

    [Fact]
    public async Task GetSet_RoundTrips()
    {
        await using var ctx = NewCache();
        var setResult = await ctx.Cache.SetAsync("k", "hello world");
        setResult.IsOk.Should().BeTrue();

        var getResult = await ctx.Cache.GetAsync<string>("k");
        getResult.IsOk.Should().BeTrue();
        getResult.Data.Should().Be("hello world");
    }

    [Fact]
    public async Task GetAsync_MissReturnsNotFound()
    {
        await using var ctx = NewCache();
        var result = await ctx.Cache.GetAsync<string>("absent");
        result.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task GetManyAsync_PartialHit_ReturnsSomeFound()
    {
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("a", 1);
        await ctx.Cache.SetAsync("b", 2);

        var result = await ctx.Cache.GetManyAsync<int>(["a", "b", "missing"]);
        result.IsSomeFound.Should().BeTrue();
        result.Data!.Should().ContainKey("a");
        result.Data!.Should().ContainKey("b");
        result.Data!.Should().NotContainKey("missing");
    }

    [Fact]
    public async Task SetNxAsync_Concurrent_OnlyOneWinsAcrossCluster()
    {
        // Cluster-wide atomicity: 32 concurrent SetNx attempts on the same key,
        // exactly one wins (returns true), all others observe the winner's value.
        await using var ctx = NewCache();
        var winners = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 32),
            new ParallelOptions { MaxDegreeOfParallelism = 32 },
            async (i, ct) =>
            {
                var result = await ctx.Cache.SetNxAsync("k", $"owner-{i}", ct: ct);
                if (result.Data) Interlocked.Increment(ref winners);
            });

        winners.Should().Be(1);
    }

    [Fact]
    public async Task IncrementAsync_Concurrent_AggregatesAtomically()
    {
        // Cluster-wide atomic increment: 8 threads x 200 increments -> 1600 total.
        await using var ctx = NewCache();
        const int threads = 8;
        const int iterations = 200;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, threads),
            new ParallelOptions { MaxDegreeOfParallelism = threads },
            async (_, ct) =>
            {
                for (var i = 0; i < iterations; i++)
                    await ctx.Cache.IncrementAsync("counter", ct: ct);
            });

        var final = await ctx.Cache.GetAsync<long>("counter");
        final.IsOk.Should().BeTrue();
        final.Data.Should().Be(threads * iterations);
    }

    [Fact]
    public async Task IncrementAsync_OnNonNumeric_ReturnsConflict()
    {
        // Redis WRONGTYPE -> Conflict result.
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("k", "not-a-number");
        var result = await ctx.Cache.IncrementAsync("k");
        result.IsConflict.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireReleaseLock_RoundTrip()
    {
        await using var ctx = NewCache();
        var acquire = await ctx.Cache.AcquireLockAsync(
            "lock-k", "owner-A", TimeSpan.FromSeconds(30));
        acquire.Data.Should().BeTrue();

        // Same owner re-acquiring sees the existing lock - SET NX fails.
        var reacquire = await ctx.Cache.AcquireLockAsync(
            "lock-k", "owner-A", TimeSpan.FromSeconds(30));
        reacquire.Data.Should().BeFalse();

        var release = await ctx.Cache.ReleaseLockAsync("lock-k", "owner-A");
        release.IsOk.Should().BeTrue();

        var reacquireAfterRelease = await ctx.Cache.AcquireLockAsync(
            "lock-k", "owner-B", TimeSpan.FromSeconds(30));
        reacquireAfterRelease.Data.Should().BeTrue();
    }

    [Fact]
    public async Task ReleaseLockAsync_WrongOwner_DoesNotRelease()
    {
        // Compare-and-delete Lua should refuse to release if lockId mismatches.
        await using var ctx = NewCache();
        await ctx.Cache.AcquireLockAsync("lock-k", "owner-A", TimeSpan.FromSeconds(30));
        await ctx.Cache.ReleaseLockAsync("lock-k", "owner-B");  // wrong owner - no-op

        var stillHeld = await ctx.Cache.AcquireLockAsync(
            "lock-k", "owner-C", TimeSpan.FromSeconds(30));
        stillHeld.Data.Should().BeFalse();  // A still holds
    }

    [Fact]
    public async Task SetAddAsync_Concurrent_BuildsCorrectCardinality()
    {
        // 100 concurrent SetAdds with 30 distinct members + repeats.
        // Final cardinality must equal exactly the number of distinct values.
        await using var ctx = NewCache();
        const int distinctMembers = 30;
        const int totalAdds = 100;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, totalAdds),
            new ParallelOptions { MaxDegreeOfParallelism = 16 },
            async (i, ct) =>
            {
                var member = $"member-{i % distinctMembers}";
                await ctx.Cache.SetAddAsync("set-k", member, ct: ct);
            });

        var card = await ctx.Cache.SetCardinalityAsync("set-k");
        card.IsOk.Should().BeTrue();
        card.Data.Should().Be(distinctMembers);
    }

    [Fact]
    public async Task SetContainsAsync_AddedThenContains()
    {
        await using var ctx = NewCache();
        await ctx.Cache.SetAddAsync("set-k", "alpha");

        var has = await ctx.Cache.SetContainsAsync("set-k", "alpha");
        has.Data.Should().BeTrue();

        var missing = await ctx.Cache.SetContainsAsync("set-k", "beta");
        missing.Data.Should().BeFalse();
    }

    [Fact]
    public async Task SetRemoveAsync_RemovesPreviouslyAdded()
    {
        await using var ctx = NewCache();
        await ctx.Cache.SetAddAsync("set-k", "alpha");
        var removed = await ctx.Cache.SetRemoveAsync("set-k", "alpha");
        removed.Data.Should().BeTrue();

        var has = await ctx.Cache.SetContainsAsync("set-k", "alpha");
        has.Data.Should().BeFalse();
    }

    [Fact]
    public async Task GetTtlAsync_HonorsExpiration()
    {
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("k", "v", TimeSpan.FromMinutes(5));
        var ttl = await ctx.Cache.GetTtlAsync("k");
        ttl.IsOk.Should().BeTrue();
        ttl.Data.Should().NotBeNull();
        ttl.Data!.Value.Should().BeGreaterThan(TimeSpan.FromMinutes(4));
        ttl.Data!.Value.Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task GetTtlAsync_AbsentKey_ReturnsNotFound()
    {
        await using var ctx = NewCache();
        var ttl = await ctx.Cache.GetTtlAsync("absent");
        ttl.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAsync_RemovesValue()
    {
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("k", "v");
        await ctx.Cache.RemoveAsync("k");
        var get = await ctx.Cache.GetAsync<string>("k");
        get.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task SetAndBroadcast_WithoutBackplane_Throws()
    {
        await using var ctx = NewCache();  // no backplane
        InvalidOperationException? caught = null;
        try
        {
            await ctx.Cache.SetAndBroadcastAsync("k", "v");
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        caught.Should().NotBeNull();
    }

    [Fact]
    public async Task SetIfEqual_TheStoredValue_Writes()
    {
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("cas-hit", "v1");

        var result = await ctx.Cache.SetIfEqualAsync("cas-hit", "v1", "v2");

        result.Data.Should().BeTrue();
        (await ctx.Cache.GetAsync<string>("cas-hit")).Data.Should().Be("v2");
    }

    [Fact]
    public async Task SetIfEqual_AnotherValue_WritesNothing()
    {
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("cas-miss", "v1");

        var result = await ctx.Cache.SetIfEqualAsync("cas-miss", "not-stored", "v2");

        result.Data.Should().BeFalse();
        (await ctx.Cache.GetAsync<string>("cas-miss")).Data.Should().Be("v1");
    }

    [Fact]
    public async Task SetIfEqual_AnAbsentKey_ReturnsOkFalseAndCreatesNothing()
    {
        await using var ctx = NewCache();

        var result = await ctx.Cache.SetIfEqualAsync("cas-absent", "v1", "v2");

        result.Data.Should().BeFalse();
        (await ctx.Cache.ExistsAsync("cas-absent")).Data.Should().BeFalse();
    }

    [Fact]
    public async Task SetIfEqual_FalseyKey_ReturnsValidationFailed()
    {
        await using var ctx = NewCache();

        var result = await ctx.Cache.SetIfEqualAsync(string.Empty, "v1", "v2");

        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task SetIfEqual_TheWrittenValue_TakesTheGivenExpiration()
    {
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("cas-ttl", "v1");

        _ = await ctx.Cache.SetIfEqualAsync(
            "cas-ttl", "v1", "v2", TimeSpan.FromMinutes(10));

        (await ctx.Cache.GetTtlAsync("cas-ttl")).Data.Should().BeCloseTo(
            TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SetIfEqual_ANonPositiveExpiration_IsRefusedAndWritesNothing(int seconds)
    {
        // A non-positive lifetime must not reach the script's no-expiry arm,
        // which writes an entry that Redis never expires. The local flavor
        // returns an input failure for the same argument.
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("cas-nonpositive-ttl", "v1", TimeSpan.FromMinutes(10));

        var result = await ctx.Cache.SetIfEqualAsync(
            "cas-nonpositive-ttl", "v1", "v2", TimeSpan.FromSeconds(seconds));

        result.IsValidationFailed.Should().BeTrue();
        (await ctx.Cache.GetAsync<string>("cas-nonpositive-ttl")).Data.Should().Be("v1");
        (await ctx.Cache.GetTtlAsync("cas-nonpositive-ttl")).Data.Should().NotBeNull();
    }

    [Fact]
    public async Task SetIfEqual_AComparisonAgainstARecordOfPrimitives_ComparesByValue()
    {
        // The distributed flavor compares the serialized bytes it stores, so a
        // caller's freshly-built expected value has to compare equal to the one
        // the store wrote.
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("cas-record", new CachedPointFixture(1, 2));

        var result = await ctx.Cache.SetIfEqualAsync(
            "cas-record", new CachedPointFixture(1, 2), new CachedPointFixture(3, 4));

        result.Data.Should().BeTrue();
        (await ctx.Cache.GetAsync<CachedPointFixture>("cas-record")).Data
            .Should().Be(new CachedPointFixture(3, 4));
    }

    [Fact]
    public async Task RemoveIfEqual_TheStoredValue_Removes()
    {
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("cad-hit", "v1");

        var result = await ctx.Cache.RemoveIfEqualAsync("cad-hit", "v1");

        result.Data.Should().BeTrue();
        (await ctx.Cache.ExistsAsync("cad-hit")).Data.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveIfEqual_AnotherValue_RemovesNothing()
    {
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("cad-miss", "v1");

        var result = await ctx.Cache.RemoveIfEqualAsync("cad-miss", "not-stored");

        result.Data.Should().BeFalse();
        (await ctx.Cache.GetAsync<string>("cad-miss")).Data.Should().Be("v1");
    }

    [Fact]
    public async Task RemoveIfEqual_AnAbsentKey_ReturnsOkFalse()
    {
        await using var ctx = NewCache();

        var result = await ctx.Cache.RemoveIfEqualAsync("cad-absent", "v1");

        result.IsOk.Should().BeTrue();
        result.Data.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveIfEqual_FalseyKey_ReturnsValidationFailed()
    {
        await using var ctx = NewCache();

        var result = await ctx.Cache.RemoveIfEqualAsync(string.Empty, "v1");

        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveIfEqual_AComparisonAgainstARecordOfPrimitives_ComparesByValue()
    {
        // The distributed flavor compares the serialized bytes it stores, so a
        // caller's freshly-built expected value has to compare equal to the one
        // the store wrote.
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("cad-record", new CachedPointFixture(1, 2));

        var result = await ctx.Cache.RemoveIfEqualAsync(
            "cad-record", new CachedPointFixture(1, 2));

        result.Data.Should().BeTrue();
        (await ctx.Cache.ExistsAsync("cad-record")).Data.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveIfEqual_ARecordDifferingInOneMember_RemovesNothing()
    {
        // The call that removes nothing. Without this case the record case above
        // passes against an implementation that removes the key whatever the
        // caller expected.
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("cad-record-miss", new CachedPointFixture(1, 2));

        var result = await ctx.Cache.RemoveIfEqualAsync(
            "cad-record-miss", new CachedPointFixture(1, 99));

        result.Data.Should().BeFalse();
        (await ctx.Cache.GetAsync<CachedPointFixture>("cad-record-miss")).Data
            .Should().Be(new CachedPointFixture(1, 2));
    }

    [Fact]
    public async Task RemoveIfEqual_ManyCallersFromOneExpectedValue_TellExactlyOneItRemoved()
    {
        // The comparison and the removal are one Lua evaluation, so exactly one
        // caller is told it removed the key. This observes the atomicity of that
        // evaluation; the comparison itself is pinned by the refusing cases.
        await using var ctx = NewCache();
        var cache = ctx.Cache;
        await cache.SetAsync("cad-race", new CachedPointFixture(1, 2));

        var attempts = await Task.WhenAll(
            Enumerable.Range(0, 16).Select(async _ =>
                await cache.RemoveIfEqualAsync(
                    "cad-race", new CachedPointFixture(1, 2))));

        attempts.Count(a => a.Data).Should().Be(1);
        (await cache.ExistsAsync("cad-race")).Data.Should().BeFalse();
    }

    [Fact]
    public async Task ConsumeToken_ABucketNobodyUsed_AdmitsItsWholeBurst()
    {
        await using var ctx = NewCache();
        var admitted = 0;

        for (var i = 0; i < 4; i++)
        {
            var decision = await ctx.Cache.ConsumeTokenAsync(
                "tb-burst", 10, TimeSpan.FromMinutes(1), burst: 3);

            if (decision.Data.Admitted)
                admitted++;
        }

        admitted.Should().Be(3, "a bucket is created full and holds its capacity");
    }

    [Fact]
    public async Task ConsumeToken_PastTheBurst_ReportsAWaitInsideTheInterval()
    {
        await using var ctx = NewCache();

        for (var i = 0; i < 2; i++)
        {
            await ctx.Cache.ConsumeTokenAsync(
                "tb-wait", 10, TimeSpan.FromMinutes(1), burst: 2);
        }

        var refused = await ctx.Cache.ConsumeTokenAsync(
            "tb-wait", 10, TimeSpan.FromMinutes(1), burst: 2);

        refused.Data.Admitted.Should().BeFalse();

        // Ten tokens a minute is one token every six seconds, less whatever the
        // server's own clock advanced while the admitted calls spent the burst,
        // which the bucket accrues rather than drops. The upper bound is the interval
        // itself and the lower bound sits well inside it, so a loaded host that
        // spends a second on two round trips does not fail a correct
        // implementation while a bucket that dropped the accrual still would.
        refused.Data.RetryAfter.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(6));
        refused.Data.RetryAfter.Should().BeGreaterThan(TimeSpan.FromSeconds(3));
    }

    /// <summary>
    /// The refill is the server's own elapsed time, so this waits for a real one.
    /// The rate is twenty tokens a second, which refills the one token the last
    /// request is short of in fifty milliseconds; the budget is attempts rather
    /// than a deadline, and it is far larger than a healthy server needs.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task ConsumeToken_AfterTheServerRefills_AdmitsAgain()
    {
        await using var ctx = NewCache();
        var interval = TimeSpan.FromSeconds(1);

        for (var i = 0; i < 2; i++)
            await ctx.Cache.ConsumeTokenAsync("tb-refill", 20, interval, burst: 2);

        var admittedAgain = false;

        for (var attempt = 0; attempt < 200 && !admittedAgain; attempt++)
        {
            var decision = await ctx.Cache.ConsumeTokenAsync(
                "tb-refill", 20, interval, burst: 2);
            admittedAgain = decision.Data.Admitted;

            if (!admittedAgain)
                await Task.Delay(decision.Data.RetryAfter);
        }

        admittedAgain.Should().BeTrue(
            "the bucket refills by the time the server sees pass, so a caller that "
            + "waits the reported span is admitted");
    }

    [Fact]
    public async Task ConsumeToken_TwoKeys_AreTwoBuckets()
    {
        await using var ctx = NewCache();

        await ctx.Cache.ConsumeTokenAsync("tb-one", 10, TimeSpan.FromMinutes(1), burst: 1);
        var other = await ctx.Cache.ConsumeTokenAsync(
            "tb-two", 10, TimeSpan.FromMinutes(1), burst: 1);

        other.Data.Admitted.Should().BeTrue("a bucket is per key");
    }

    [Fact]
    public async Task ConsumeToken_TheBucketKey_TakesALifetime()
    {
        await using var ctx = NewCache();

        await ctx.Cache.ConsumeTokenAsync("tb-ttl", 10, TimeSpan.FromMinutes(1), burst: 10);

        (await ctx.Cache.GetTtlAsync("tb-ttl")).Data.Should().BeCloseTo(
            TimeSpan.FromMinutes(2),
            TimeSpan.FromSeconds(5),
            "a refill from empty plus one interval of slack, so a bucket nobody "
            + "touches again is dropped rather than kept");
    }

    [Fact]
    public async Task ConsumeToken_ManyCallersOnOneBucket_AdmitNoMoreThanTheBurst()
    {
        await using var ctx = NewCache();
        var cache = ctx.Cache;

        var attempts = await Task.WhenAll(
            Enumerable.Range(0, 32).Select(async _ =>
                await cache.ConsumeTokenAsync(
                    "tb-race", 1, TimeSpan.FromMinutes(1), burst: 4)));

        attempts.Count(a => a.Data.Admitted).Should().Be(
            4,
            "the script refills and consumes in one server-side step, so two "
            + "callers cannot both spend the last token");
    }

    /// <summary>
    /// Ten tokens a minute is one thousandth of a token every six milliseconds,
    /// and a round trip to the server is far shorter than that, so every arrival
    /// in the run below lands inside a step the refill cannot take. The wait the
    /// bucket quotes must still fall by the time that passed, which it does only
    /// if the arrivals left the accrued time in place. The loop polls the quoted
    /// wait on an attempt budget a healthy server never reaches: a script that
    /// reset the accrual on each arrival keeps quoting the first wait, spends the
    /// budget, and fails the assertion.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task ConsumeToken_ArrivalsInsideOneStep_KeepTheAccruedTime()
    {
        await using var ctx = NewCache();
        var interval = TimeSpan.FromMinutes(1);

        await ctx.Cache.ConsumeTokenAsync("tb-carry", 10, interval, burst: 1);
        var first = await ctx.Cache.ConsumeTokenAsync("tb-carry", 10, interval, burst: 1);
        var target = first.Data.RetryAfter - TimeSpan.FromMilliseconds(300);
        var later = first;

        for (var attempt = 0; attempt < 200_000 && later.Data.RetryAfter >= target; attempt++)
        {
            later = await ctx.Cache.ConsumeTokenAsync("tb-carry", 10, interval, burst: 1);
        }

        first.Data.Admitted.Should().BeFalse();
        later.Data.Admitted.Should().BeFalse();
        later.Data.RetryAfter.Should().BeLessThan(
            first.Data.RetryAfter - TimeSpan.FromMilliseconds(300),
            "the script carries the accrual it could not spend on a whole "
            + "thousandth into the next call, so hammering one bucket does not "
            + "reset the time it has banked");
    }

    /// <summary>
    /// An earlier script version wrote bucket keys without the <c>rem</c>
    /// field, so a running deployment can hold such keys. The script reads the
    /// missing field as no accrual rather than failing on it.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task ConsumeToken_ABucketTheEarlierScriptWrote_IsReadAsNoAccrual()
    {
        await using var ctx = NewCache();
        var key = ctx.KeyPrefix + "tb-legacy";

        // Half a bucket, counted a minute ago, in the two-field shape.
        await ctx.Database.HashSetAsync(
            key,
            [
                new HashEntry("tokens", 500),
                new HashEntry(
                    "at",
                    DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds()),
            ]);

        var decision = await ctx.Cache.ConsumeTokenAsync(
            "tb-legacy", 10, TimeSpan.FromMinutes(1), burst: 1);

        decision.IsOk.Should().BeTrue(
            "a bucket written before the accrual field existed still answers");
        decision.Data.Admitted.Should().BeTrue(
            "half a thousandth held plus a minute of a ten-a-minute rate is a "
            + "full token, so the request is admitted");
    }

    [Fact]
    public async Task ConsumeToken_AKeyHoldingSomethingElse_AnswersConflict()
    {
        await using var ctx = NewCache();
        await ctx.Cache.SetAsync("tb-wrongtype", "not a bucket");

        var result = await ctx.Cache.ConsumeTokenAsync(
            "tb-wrongtype", 10, TimeSpan.FromMinutes(1), burst: 5);

        result.IsConflict.Should().BeTrue(
            "the bucket is a hash, so a key holding a string answers WRONGTYPE, "
            + "and that is the same type collision the in-process flavors report "
            + "as Conflict rather than a store outage");
    }

    [Fact]
    public async Task ConsumeToken_FalseyKey_ReturnsValidationFailed()
    {
        await using var ctx = NewCache();

        var result = await ctx.Cache.ConsumeTokenAsync(
            string.Empty, 10, TimeSpan.FromMinutes(1), burst: 1);

        result.IsValidationFailed.Should().BeTrue();
        result.InputErrors.Should().ContainSingle(e => e.Field == "key");
    }

    [MustDisposeResource(false)]
    private CacheContextFixture NewCache()
    {
        // Fresh key prefix per test for isolation against other tests in the same fixture.
        var prefix = $"test-{Guid.NewGuid():N}:";
        return new CacheContextFixture(r_fixture.ConnectionString, prefix);
    }
}
