// -----------------------------------------------------------------------
// <copyright file="RedisLuaScripts.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching.Distributed.Redis;

/// <summary>
/// Lua scripts used internally by the Redis cache implementation for atomic
/// compound operations. Kept here so the source is reviewable in one
/// place rather than scattered through the operation handlers.
/// </summary>
internal static class RedisLuaScripts
{
    /// <summary>
    /// Atomic INCRBY + optional PEXPIRE on first creation. ARGV[1] = amount;
    /// ARGV[2] = TTL in milliseconds (0 = no expiration). Sets the TTL only
    /// when the key has no existing TTL (PTTL &lt; 0 covers both "key new"
    /// and "key exists with no expiration") - existing TTLs survive
    /// subsequent increments.
    /// </summary>
    /// <remarks>
    /// After INCRBY, if the result is outside IEEE-754 max safe integer
    /// (+/-9007199254740991 - dual-runtime bound shared with TypeScript
    /// <c>Number.MAX_SAFE_INTEGER</c>), reverses with DECRBY in the same
    /// script and returns <c>ERR safe_integer_overflow</c> so the store is
    /// never left with a value that Node cannot represent exactly.
    /// </remarks>
    internal const string INCREMENT_WITH_OPTIONAL_TTL = """
        local result = redis.call('INCRBY', KEYS[1], ARGV[1])
        if result > 9007199254740991 or result < -9007199254740991 then
            redis.call('DECRBY', KEYS[1], ARGV[1])
            return redis.error_reply('ERR safe_integer_overflow')
        end
        if ARGV[2] ~= '0' and redis.call('PTTL', KEYS[1]) < 0 then
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
        end
        return result
        """;

    /// <summary>
    /// Atomic compare-and-delete for distributed lock release. Only DELs
    /// the key if the stored value matches ARGV[1] (the lockId). Returns
    /// 1 if removed, 0 if mismatch / absent.
    /// </summary>
    internal const string RELEASE_LOCK_IF_OWNER = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        else
            return 0
        end
        """;

    /// <summary>
    /// Atomic compare-and-set. Only SETs the key when the stored value
    /// matches ARGV[1] (the expected serialized value). ARGV[2] = the new
    /// serialized value; ARGV[3] = TTL in milliseconds (0 = no expiration).
    /// Returns 1 if written, 0 on mismatch or an absent key.
    /// </summary>
    /// <remarks>
    /// An absent key returns 0: GET returns false in Lua for a missing key,
    /// which never equals a serialized payload. A caller holding a copy of what
    /// used to be stored therefore cannot write over the value the next reader
    /// loads in its place.
    /// </remarks>
    internal const string SET_IF_EQUAL = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            if ARGV[3] == '0' then
                redis.call('SET', KEYS[1], ARGV[2])
            else
                redis.call('SET', KEYS[1], ARGV[2], 'PX', ARGV[3])
            end
            return 1
        else
            return 0
        end
        """;

    /// <summary>
    /// Atomic compare-and-delete. Only DELs the key when the stored value
    /// matches ARGV[1] (the expected serialized value). Returns 1 if removed,
    /// 0 on mismatch or an absent key.
    /// </summary>
    /// <remarks>
    /// An absent key returns 0: GET returns false in Lua for a missing key,
    /// which never equals a serialized payload. The body matches
    /// <see cref="RELEASE_LOCK_IF_OWNER"/>, and the two are kept apart because
    /// they compare different things - a lock holder's raw identifier there, a
    /// serialized payload here - and a change to one is not a change to the
    /// other.
    /// </remarks>
    internal const string REMOVE_IF_EQUAL = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        else
            return 0
        end
        """;

    /// <summary>
    /// Atomic token-bucket refill-then-consume. ARGV[1] = tokens added per
    /// interval; ARGV[2] = that interval in milliseconds; ARGV[3] = bucket
    /// capacity; ARGV[4] = tokens this request costs. Returns
    /// <c>{admitted, retry_after_ms}</c>, where <c>admitted</c> is 1 or 0 and
    /// the wait is 0 on the admitted arm.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tokens are held as thousandths so the refill stays integer arithmetic:
    /// one elapsed millisecond adds a fraction of a token at most rates, and a
    /// bucket that floored that fraction to zero would never refill at all.
    /// </para>
    /// <para>
    /// The <c>rem</c> field holds the part of the accrual that did not amount
    /// to a whole thousandth, and the next call adds it back before dividing. A
    /// bucket keyed by one address takes arrivals far more often than one
    /// thousandth of a token takes to accrue, and a division that dropped its
    /// remainder each time would refill at a rate the arrivals themselves
    /// reduced, down to no refill at all. The stored value is always under one
    /// interval, and a bucket that reaches its capacity drops it.
    /// </para>
    /// <para>
    /// The instant comes from <c>TIME</c> - the store's own clock, which every
    /// node shares - so two nodes whose clocks disagree still charge one bucket
    /// consistently. A stored instant later than the store's own now (a clock
    /// stepped backwards) refills nothing rather than draining the bucket.
    /// </para>
    /// <para>
    /// The key's lifetime is a refill from empty to full plus one interval, so a
    /// bucket nobody touches again is dropped, and a caller returning after that
    /// lifetime finds the full bucket the refill would have given it anyway.
    /// </para>
    /// </remarks>
    internal const string CONSUME_TOKEN = """
        local refill = tonumber(ARGV[1])
        local interval = tonumber(ARGV[2])
        local burst = tonumber(ARGV[3]) * 1000
        local cost = tonumber(ARGV[4]) * 1000
        local clock = redis.call('TIME')
        local at = (tonumber(clock[1]) * 1000) + math.floor(tonumber(clock[2]) / 1000)
        local state = redis.call('HMGET', KEYS[1], 'tokens', 'at', 'rem')
        local tokens = burst
        local rem = 0
        if state[1] then
            at = math.max(at, tonumber(state[2]))
            local elapsed = at - tonumber(state[2])
            local accrued = (elapsed * refill * 1000) + tonumber(state[3] or 0)
            tokens = tonumber(state[1]) + math.floor(accrued / interval)
            rem = accrued % interval
            if tokens >= burst then
                tokens = burst
                rem = 0
            end
        end
        local admitted = 0
        local wait = 0
        if tokens >= cost then
            admitted = 1
            tokens = tokens - cost
        else
            wait = math.ceil((((cost - tokens) * interval) - rem) / (refill * 1000))
        end
        redis.call('HSET', KEYS[1], 'tokens', tokens, 'at', at, 'rem', rem)
        redis.call('PEXPIRE', KEYS[1], math.ceil((burst * interval) / (refill * 1000)) + interval)
        return {admitted, wait}
        """;

    /// <summary>
    /// Atomic SADD + optional PEXPIRE on first add. ARGV[1] = member;
    /// ARGV[2] = TTL in milliseconds (0 = no expiration). Sets the TTL
    /// only when the set is being created (TTL not yet set), so existing
    /// sets keep their TTL.
    /// </summary>
    internal const string SET_ADD_WITH_OPTIONAL_TTL = """
        local added = redis.call('SADD', KEYS[1], ARGV[1])
        if ARGV[2] ~= '0' and redis.call('PTTL', KEYS[1]) < 0 then
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
        end
        return added
        """;
}
