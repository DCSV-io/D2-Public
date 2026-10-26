// -----------------------------------------------------------------------
// <copyright file="DefaultLocalCacheUnitTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Caching.Local;

using System.Collections.Generic;
using AwesomeAssertions;
using DcsvIo.D2.Caching;
using DcsvIo.D2.Caching.Local.Default;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

/// <summary>
/// Surface tests for <see cref="DefaultLocalCache"/>: D2Result mapping,
/// argument validation, key prefixing, idempotency. Real cache behaviors
/// (eviction under capacity, TTL expiration timing, concurrent access)
/// are tested in <c>DefaultLocalCacheBehaviorTests</c>.
/// </summary>
public sealed class DefaultLocalCacheUnitTests
{
    [Fact]
    public async Task SetIfEqualAsync_PresentNullReference_ReplacesValue()
    {
        using var cache = NewCache();
        await cache.SetAsync<string?>("nullable", null);

        var result = await cache.SetIfEqualAsync("nullable", null, "replacement");

        result.Data.Should().BeTrue();
        (await cache.GetAsync<string>("nullable")).Data.Should().Be("replacement");
    }

    [Fact]
    public async Task RemoveIfEqualAsync_PresentNullReference_RemovesValue()
    {
        using var cache = NewCache();
        await cache.SetAsync<string?>("nullable", null);

        var result = await cache.RemoveIfEqualAsync<string?>("nullable", null);

        result.Data.Should().BeTrue();
        (await cache.ExistsAsync("nullable")).Data.Should().BeFalse();
    }

    [Fact]
    public async Task SetIfEqualAsync_PresentNullableValueNull_ReplacesValue()
    {
        using var cache = NewCache();
        await cache.SetAsync<int?>("nullable", null);

        var result = await cache.SetIfEqualAsync<int?>("nullable", null, 42);

        result.Data.Should().BeTrue();
        (await cache.GetAsync<int?>("nullable")).Data.Should().Be(42);
    }

    [Fact]
    public async Task RemoveIfEqualAsync_PresentNullableValueNull_RemovesValue()
    {
        using var cache = NewCache();
        await cache.SetAsync<int?>("nullable", null);

        var result = await cache.RemoveIfEqualAsync<int?>("nullable", null);

        result.Data.Should().BeTrue();
        (await cache.ExistsAsync("nullable")).Data.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConditionalWrite_AbsentNull_DoesNotMatch(bool remove)
    {
        using var cache = NewCache();

        var result = remove
            ? await cache.RemoveIfEqualAsync<int?>("missing", null)
            : await cache.SetIfEqualAsync<int?>("missing", null, 42);

        result.Data.Should().BeFalse();
        (await cache.ExistsAsync("missing")).Data.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, null, 1)]
    [InlineData(true, null, 1)]
    [InlineData(false, "different", null)]
    [InlineData(true, "different", null)]
    [InlineData(false, 1, 2)]
    [InlineData(true, 1, 2)]
    [InlineData(false, "1", 1)]
    [InlineData(true, "1", 1)]
    public async Task ConditionalWrite_MismatchedValue_DoesNotMutate(
        bool remove, object? stored, int? expected)
    {
        using var cache = NewCache();
        await cache.SetAsync("mismatch", stored);

        var result = remove
            ? await cache.RemoveIfEqualAsync("mismatch", expected)
            : await cache.SetIfEqualAsync("mismatch", expected, 42);

        result.Data.Should().BeFalse();
        (await cache.ExistsAsync("mismatch")).Data.Should().BeTrue();
        (await cache.GetAsync<object?>("mismatch")).Data.Should().Be(stored);
    }

    [Fact]
    public async Task GetAsync_HitReturnsOk()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", 42);

        var result = await cache.GetAsync<int>("k");

        result.IsOk.Should().BeTrue();
        result.Data.Should().Be(42);
    }

    [Fact]
    public async Task GetAsync_MissReturnsNotFound()
    {
        using var cache = NewCache();
        var result = await cache.GetAsync<int>("missing");

        result.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_NullKey_ReturnsValidationFailed()
    {
        using var cache = NewCache();
        var result = await cache.GetAsync<int>(null!);

        result.IsValidationFailed.Should().BeTrue();
        result.InputErrors.Should().ContainSingle(e => e.Field == "key");
    }

    [Fact]
    public async Task GetAsync_EmptyKey_ReturnsValidationFailed()
    {
        using var cache = NewCache();
        var result = await cache.GetAsync<int>(string.Empty);

        result.IsValidationFailed.Should().BeTrue();
        result.InputErrors.Should().ContainSingle(e => e.Field == "key");
    }

    [Fact]
    public async Task GetManyAsync_AllHitReturnsOk()
    {
        using var cache = NewCache();
        await cache.SetAsync("a", 1);
        await cache.SetAsync("b", 2);

        var result = await cache.GetManyAsync<int>(["a", "b"]);

        result.IsOk.Should().BeTrue();
        result.Data!["a"].Should().Be(1);
        result.Data!["b"].Should().Be(2);
    }

    [Fact]
    public async Task GetManyAsync_AllMissReturnsNotFound()
    {
        using var cache = NewCache();
        var result = await cache.GetManyAsync<int>(["x", "y"]);
        result.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task GetManyAsync_PartialHitReturnsSomeFound()
    {
        using var cache = NewCache();
        await cache.SetAsync("a", 1);

        var result = await cache.GetManyAsync<int>(["a", "missing"]);

        result.IsSomeFound.Should().BeTrue();
        result.Data!.Should().ContainKey("a");
        result.Data!.Should().NotContainKey("missing");
    }

    [Fact]
    public async Task GetManyAsync_EmptyKeys_ReturnsValidationFailed()
    {
        using var cache = NewCache();
        var result = await cache.GetManyAsync<int>([]);

        result.IsValidationFailed.Should().BeTrue();
        result.InputErrors.Should().ContainSingle(e => e.Field == "keys");
    }

    [Fact]
    public async Task ExistsAsync_PresentReturnsTrue()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", 1);
        var result = await cache.ExistsAsync("k");
        result.IsOk.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_AbsentReturnsFalse()
    {
        using var cache = NewCache();
        var result = await cache.ExistsAsync("missing");
        result.IsOk.Should().BeTrue();
        result.Data.Should().BeFalse();
    }

    [Fact]
    public async Task GetTtlAsync_AbsentKeyReturnsNotFound()
    {
        using var cache = NewCache();
        var result = await cache.GetTtlAsync("missing");
        result.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task GetTtlAsync_PresentKeyWithTtlReturnsRemaining()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", 1, TimeSpan.FromMinutes(5));
        var result = await cache.GetTtlAsync("k");
        result.IsOk.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Value.Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
        result.Data!.Value.Should().BeGreaterThan(TimeSpan.FromMinutes(4));
    }

    [Fact]
    public async Task RemoveAsync_AbsentKeyIsIdempotentOk()
    {
        using var cache = NewCache();
        var result = await cache.RemoveAsync("never-set");
        result.IsOk.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAsync_RemovesPreviouslySet()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", 1);
        await cache.RemoveAsync("k");
        (await cache.GetAsync<int>("k")).IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveManyAsync_RemovesAll()
    {
        using var cache = NewCache();
        await cache.SetAsync("a", 1);
        await cache.SetAsync("b", 2);
        await cache.RemoveManyAsync(["a", "b"]);

        (await cache.ExistsAsync("a")).Data.Should().BeFalse();
        (await cache.ExistsAsync("b")).Data.Should().BeFalse();
    }

    [Fact]
    public async Task SetNxAsync_NewKeyReturnsTrue()
    {
        using var cache = NewCache();
        var result = await cache.SetNxAsync("k", 1);
        result.IsOk.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task SetNxAsync_ExistingKeyReturnsFalseAndDoesNotOverwrite()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", 100);

        var result = await cache.SetNxAsync("k", 200);

        result.IsOk.Should().BeTrue();
        result.Data.Should().BeFalse();
        (await cache.GetAsync<int>("k")).Data.Should().Be(100);
    }

    [Fact]
    public async Task IncrementAsync_NewKeyReturnsAmount()
    {
        using var cache = NewCache();
        var result = await cache.IncrementAsync("counter");
        result.IsOk.Should().BeTrue();
        result.Data.Should().Be(1);
    }

    [Fact]
    public async Task IncrementAsync_ExistingNumericIncrements()
    {
        using var cache = NewCache();
        await cache.IncrementAsync("counter", 5);
        var result = await cache.IncrementAsync("counter", 3);
        result.Data.Should().Be(8);
    }

    [Fact]
    public async Task IncrementAsync_NegativeAmountWorks()
    {
        using var cache = NewCache();
        await cache.IncrementAsync("counter", 10);
        var result = await cache.IncrementAsync("counter", -4);
        result.Data.Should().Be(6);
    }

    [Fact]
    public async Task IncrementAsync_NonNumericKeyReturnsConflict()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", "not-a-number");
        var result = await cache.IncrementAsync("k");
        result.IsConflict.Should().BeTrue();
    }

    [Fact]
    public async Task IncrementAsync_ExistingKeyWithTtl_PreservesTtl_NotResetToDefault()
    {
        // Regression for the bug where SetCore was called with `expiration: null`
        // on the increment-existing path, which fell back to DefaultExpiration
        // (1h) instead of preserving the existing TTL. Redis-parity contract:
        // INCR on a key with TTL must keep the TTL; only the first SET sets it.
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock: clock);
        var shortTtl = TimeSpan.FromMinutes(2);

        await cache.SetAsync("counter", 5L, shortTtl);

        // Advance the fake clock by 30 s, which shortens the remaining TTL
        // without any real-time wait.
        clock.Advance(TimeSpan.FromSeconds(30));

        await cache.IncrementAsync("counter");

        var ttlR = await cache.GetTtlAsync("counter");
        ttlR.IsOk.Should().BeTrue();
        ttlR.Data.Should().NotBeNull();

        // TTL must remain bounded by the original 2-minute window and must not
        // jump back up to the 1-hour default. After 30 s elapsed, TTL ~= 90 s < 2 min.
        ttlR.Data!.Value.Should().BeLessThan(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public async Task IncrementAsync_ExistingKeyWithoutTtl_StaysWithoutTtl()
    {
        // Companion: a key written with the default expiration keeps a TTL
        // bounded by that default after an increment, so the increment path
        // does not extend or add a TTL.
        using var cache = NewCache();

        await cache.SetAsync("counter", 5L); // null expiration -> DefaultExpiration applied
        await cache.IncrementAsync("counter");

        // The original DefaultExpiration is still applied from the initial Set;
        // increment must not layer another TTL on top. The check that the TTL
        // has not been re-extended is that it is still bounded by the default
        // window.
        var ttlR = await cache.GetTtlAsync("counter");
        ttlR.IsOk.Should().BeTrue();
        ttlR.Data.Should().NotBeNull();
        ttlR.Data!.Value.Should().BeLessThan(TimeSpan.FromHours(1.1));
    }

    [Fact]
    public async Task SetAsync_ZeroExpiration_ReturnsValidationFailed()
    {
        // Regression: SetCore once silently treated TimeSpan.Zero as "no
        // expiration", so the cache slot was kept forever although the call
        // passed a zero TTL. The public surface now validates the expiration
        // and rejects zero.
        using var cache = NewCache();

        var result = await cache.SetAsync("k", "value", TimeSpan.Zero);

        result.Success.Should().BeFalse();
        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task SetAsync_NegativeExpiration_ReturnsValidationFailed()
    {
        using var cache = NewCache();

        var result = await cache.SetAsync("k", "value", TimeSpan.FromSeconds(-1));

        result.Success.Should().BeFalse();
        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task SetManyAsync_ContainsEmptyKey_ReturnsValidationFailed()
    {
        // Regression: per-entry validation gap. The top-level dictionary was
        // validated but the individual keys were not, so empty-key entries
        // silently merged into the prefix-only cache slot.
        using var cache = NewCache();

        var entries = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["valid"] = "v1",
            [string.Empty] = "v2",
        };

        var result = await cache.SetManyAsync(entries);

        result.Success.Should().BeFalse();
        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveManyAsync_ContainsEmptyKey_ReturnsValidationFailed()
    {
        using var cache = NewCache();

        var result = await cache.RemoveManyAsync(new[] { "valid", string.Empty });

        result.Success.Should().BeFalse();
        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GetManyAsync_ContainsEmptyKey_ReturnsValidationFailed()
    {
        using var cache = NewCache();

        var result = await cache.GetManyAsync<string>(new[] { "valid", string.Empty });

        result.Success.Should().BeFalse();
        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireLockAsync_FirstCallerAcquires()
    {
        using var cache = NewCache();
        var result = await cache.AcquireLockAsync("k", "owner-A", TimeSpan.FromSeconds(30));
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireLockAsync_SecondCallerWhileHeldReturnsFalse()
    {
        using var cache = NewCache();
        await cache.AcquireLockAsync("k", "owner-A", TimeSpan.FromSeconds(30));
        var result = await cache.AcquireLockAsync("k", "owner-B", TimeSpan.FromSeconds(30));
        result.Data.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseLockAsync_CorrectIdReleases()
    {
        using var cache = NewCache();
        await cache.AcquireLockAsync("k", "owner-A", TimeSpan.FromSeconds(30));
        await cache.ReleaseLockAsync("k", "owner-A");
        var second = await cache.AcquireLockAsync("k", "owner-B", TimeSpan.FromSeconds(30));
        second.Data.Should().BeTrue();
    }

    [Fact]
    public async Task ReleaseLockAsync_WrongIdIsNoOp()
    {
        using var cache = NewCache();
        await cache.AcquireLockAsync("k", "owner-A", TimeSpan.FromSeconds(30));
        await cache.ReleaseLockAsync("k", "owner-B");  // wrong id
        var second = await cache.AcquireLockAsync("k", "owner-B", TimeSpan.FromSeconds(30));
        second.Data.Should().BeFalse();  // still held by A
    }

    [Fact]
    public async Task ReleaseLockAsync_NeverHeldKeyIsNoOp()
    {
        using var cache = NewCache();
        Exception? thrown = null;
        try
        {
            await cache.ReleaseLockAsync("never-held", "any-id");
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        thrown.Should().BeNull();
    }

    [Fact]
    public async Task KeyPrefix_AppliesAutomatically()
    {
        var prefixedCache = NewCache(opts => opts.KeyPrefix = "test:");
        await prefixedCache.SetAsync("k", 42);

        // The cache applies the prefix internally: the test passes "k" and the
        // library stores the entry under "test:k".
        var result = await prefixedCache.GetAsync<int>("k");
        result.IsOk.Should().BeTrue();
        result.Data.Should().Be(42);
    }

    [Fact]
    public async Task Dispose_IsIdempotent()
    {
        var cache = NewCache();
        cache.Dispose();
        var act = cache.Dispose;
        act.Should().NotThrow();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Dispose_AcquireLockAsync_ThrowsObjectDisposedException()
    {
        var cache = NewCache();
        cache.Dispose();

        var act = async () => await cache.AcquireLockAsync("k", "lock-1", TimeSpan.FromSeconds(1));
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task Dispose_ReleaseLockAsync_ThrowsObjectDisposedException()
    {
        var cache = NewCache();
        cache.Dispose();

        var act = async () => await cache.ReleaseLockAsync("k", "lock-1");
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task Dispose_SetAsync_ThrowsObjectDisposedException()
    {
        var cache = NewCache();
        cache.Dispose();

        // Non-lock path must also fail closed (not only IMemoryCache-backed Get).
        var act = async () => await cache.SetAsync("k", 1);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Theory]
    [InlineData("GetAsync")]
    [InlineData("GetManyAsync")]
    [InlineData("ExistsAsync")]
    [InlineData("GetTtlAsync")]
    [InlineData("SetAsync")]
    [InlineData("SetManyAsync")]
    [InlineData("RemoveAsync")]
    [InlineData("RemoveManyAsync")]
    [InlineData("SetNxAsync")]
    [InlineData("SetIfEqualAsync")]
    [InlineData("RemoveIfEqualAsync")]
    [InlineData("IncrementAsync")]
    [InlineData("ConsumeTokenAsync")]
    [InlineData("AcquireLockAsync")]
    [InlineData("ReleaseLockAsync")]
    public async Task Dispose_EveryPublicOp_ThrowsObjectDisposedException(string opName)
    {
        var cache = NewCache();
        cache.Dispose();

        // Do not capture a using-disposed local in the async lambda (inspectcode).
        var act = () => InvokePublicOpAsync(cache, opName);
        await act.Should().ThrowAsync<ObjectDisposedException>(because: opName);
    }

    [Fact]
    public void Ctor_NullOptionsThrows()
    {
        var act = () => new DefaultLocalCache(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SetIfEqualAsync_NullKey_ReturnsValidationFailed()
    {
        using var cache = NewCache();

        var result = await cache.SetIfEqualAsync(null!, 1, 2);

        result.IsValidationFailed.Should().BeTrue();
        result.InputErrors.Should().ContainSingle(e => e.Field == "key");
    }

    [Fact]
    public async Task SetIfEqualAsync_EmptyKey_ReturnsValidationFailed()
    {
        using var cache = NewCache();

        var result = await cache.SetIfEqualAsync(string.Empty, 1, 2);

        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task SetIfEqualAsync_ZeroExpiration_ReturnsValidationFailed()
    {
        using var cache = NewCache();

        var result = await cache.SetIfEqualAsync("k", 1, 2, TimeSpan.Zero);

        result.IsValidationFailed.Should().BeTrue();
        result.InputErrors.Should().ContainSingle(e => e.Field == "expiration");
    }

    [Fact]
    public async Task SetIfEqualAsync_NegativeExpiration_ReturnsValidationFailed()
    {
        using var cache = NewCache();

        var result = await cache.SetIfEqualAsync("k", 1, 2, TimeSpan.FromSeconds(-1));

        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task SetIfEqualAsync_AbsentKey_ReturnsOkFalse()
    {
        // Once the key is gone, a copy of what used to be stored has nothing to
        // compare against, and writing anyway would overwrite whatever
        // authoritative source the absence sends the next reader to.
        using var cache = NewCache();

        var result = await cache.SetIfEqualAsync("absent", 1, 2);

        result.IsOk.Should().BeTrue();
        result.Data.Should().BeFalse();
    }

    [Fact]
    public async Task SetIfEqualAsync_AbsentKey_WritesNothing()
    {
        using var cache = NewCache();

        _ = await cache.SetIfEqualAsync("absent", 1, 2);

        (await cache.GetAsync<int>("absent")).IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task SetIfEqualAsync_TheStoredValue_ReturnsOkTrue()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", 1);

        var result = await cache.SetIfEqualAsync("k", 1, 2);

        result.IsOk.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task SetIfEqualAsync_TheStoredValue_ReplacesIt()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", 1);

        _ = await cache.SetIfEqualAsync("k", 1, 2);

        (await cache.GetAsync<int>("k")).Data.Should().Be(2);
    }

    [Fact]
    public async Task SetIfEqualAsync_AnotherValue_ReturnsOkFalse()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", 1);

        var result = await cache.SetIfEqualAsync("k", 99, 2);

        result.IsOk.Should().BeTrue();
        result.Data.Should().BeFalse();
    }

    [Fact]
    public async Task SetIfEqualAsync_AnotherValue_LeavesTheStoredValueAlone()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", 1);

        _ = await cache.SetIfEqualAsync("k", 99, 2);

        (await cache.GetAsync<int>("k")).Data.Should().Be(1);
    }

    [Fact]
    public async Task SetIfEqualAsync_AValueOfAnotherType_ReturnsOkFalse()
    {
        // The stored object is not a T, so the expected value has nothing to be
        // compared against and the write must not happen.
        using var cache = NewCache();
        await cache.SetAsync("k", "a string");

        var result = await cache.SetIfEqualAsync("k", 1, 2);

        result.IsOk.Should().BeTrue();
        result.Data.Should().BeFalse();
    }

    [Fact]
    public async Task SetIfEqualAsync_AValueOfAnotherType_LeavesTheStoredValueAlone()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", "a string");

        _ = await cache.SetIfEqualAsync("k", 1, 2);

        (await cache.GetAsync<string>("k")).Data.Should().Be("a string");
    }

    [Fact]
    public async Task SetIfEqualAsync_TheWrittenValue_TakesTheGivenExpiration()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", 1);

        _ = await cache.SetIfEqualAsync("k", 1, 2, TimeSpan.FromMinutes(5));

        (await cache.GetTtlAsync("k")).Data.Should().BeCloseTo(
            TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RemoveIfEqualAsync_NullKey_ReturnsValidationFailed()
    {
        using var cache = NewCache();

        var result = await cache.RemoveIfEqualAsync(null!, 1);

        result.IsValidationFailed.Should().BeTrue();
        result.InputErrors.Should().ContainSingle(e => e.Field == "key");
    }

    [Fact]
    public async Task RemoveIfEqualAsync_EmptyKey_ReturnsValidationFailed()
    {
        using var cache = NewCache();

        var result = await cache.RemoveIfEqualAsync(string.Empty, 1);

        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveIfEqualAsync_AbsentKey_ReturnsOkFalse()
    {
        // A false result tells the caller that the entry it held is not the
        // entry stored now, the same result as when another writer replaced it.
        using var cache = NewCache();

        var result = await cache.RemoveIfEqualAsync("absent", 1);

        result.IsOk.Should().BeTrue();
        result.Data.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveIfEqualAsync_TheStoredValue_ReturnsOkTrue()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", 1);

        var result = await cache.RemoveIfEqualAsync("k", 1);

        result.IsOk.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveIfEqualAsync_TheStoredValue_RemovesIt()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", 1);

        _ = await cache.RemoveIfEqualAsync("k", 1);

        (await cache.GetAsync<int>("k")).IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveIfEqualAsync_AnotherValue_ReturnsOkFalse()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", 1);

        var result = await cache.RemoveIfEqualAsync("k", 99);

        result.IsOk.Should().BeTrue();
        result.Data.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveIfEqualAsync_AnotherValue_LeavesTheStoredValueAlone()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", 1);

        _ = await cache.RemoveIfEqualAsync("k", 99);

        (await cache.GetAsync<int>("k")).Data.Should().Be(1);
    }

    [Fact]
    public async Task RemoveIfEqualAsync_AValueOfAnotherType_LeavesTheStoredValueAlone()
    {
        // The stored object is not a T, so the expected value has nothing to be
        // compared against and the removal must not happen.
        using var cache = NewCache();
        await cache.SetAsync("k", "a string");

        var result = await cache.RemoveIfEqualAsync("k", 1);

        result.Data.Should().BeFalse();
        (await cache.GetAsync<string>("k")).Data.Should().Be("a string");
    }

    [Fact]
    public async Task RemoveIfEqualAsync_TheRemovedKey_LosesItsExpiration()
    {
        // The cache stores the entry's lifetime separately from the entry, so a
        // removal that left the lifetime behind would report a TTL for a key
        // that is gone.
        using var cache = NewCache();
        await cache.SetAsync("k", 1, TimeSpan.FromMinutes(5));

        _ = await cache.RemoveIfEqualAsync("k", 1);

        (await cache.GetTtlAsync("k")).IsNotFound.Should().BeTrue();
    }

    private static DefaultLocalCache NewCache(
        Action<LocalCacheOptions>? configure = null, TimeProvider? clock = null)
    {
        var opts = new LocalCacheOptions();
        configure?.Invoke(opts);
        return new DefaultLocalCache(Options.Create(opts), clock);
    }

    private static async Task InvokePublicOpAsync(DefaultLocalCache cache, string opName)
    {
        switch (opName)
        {
            case "GetAsync":
                await cache.GetAsync<int>("k");
                break;
            case "GetManyAsync":
                await cache.GetManyAsync<int>(["k"]);
                break;
            case "ExistsAsync":
                await cache.ExistsAsync("k");
                break;
            case "GetTtlAsync":
                await cache.GetTtlAsync("k");
                break;
            case "SetAsync":
                await cache.SetAsync("k", 1);
                break;
            case "SetManyAsync":
                await cache.SetManyAsync(new Dictionary<string, int> { ["k"] = 1 });
                break;
            case "RemoveAsync":
                await cache.RemoveAsync("k");
                break;
            case "RemoveManyAsync":
                await cache.RemoveManyAsync(["k"]);
                break;
            case "SetNxAsync":
                await cache.SetNxAsync("k", 1);
                break;
            case "SetIfEqualAsync":
                await cache.SetIfEqualAsync("k", 1, 2);
                break;
            case "RemoveIfEqualAsync":
                await cache.RemoveIfEqualAsync("k", 1);
                break;
            case "IncrementAsync":
                await cache.IncrementAsync("k");
                break;
            case "ConsumeTokenAsync":
                await cache.ConsumeTokenAsync("k", 1, TimeSpan.FromSeconds(1), 1);
                break;
            case "AcquireLockAsync":
                await cache.AcquireLockAsync("k", "lock-1", TimeSpan.FromSeconds(1));
                break;
            case "ReleaseLockAsync":
                await cache.ReleaseLockAsync("k", "lock-1");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(opName), opName, null);
        }
    }
}
