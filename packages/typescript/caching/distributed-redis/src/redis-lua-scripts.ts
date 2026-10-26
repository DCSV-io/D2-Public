// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

// Lua bodies byte-equivalent to .NET RedisLuaScripts.cs (INCREMENT,
// RELEASE_LOCK, SET_ADD, SET_IF_EQUAL, REMOVE_IF_EQUAL and CONSUME_TOKEN).
// These public constants pin the scripts for ContractFixtures parity (barrel
// re-export); they are not an executor API.

/**
 * Atomic INCRBY + optional PEXPIRE when PTTL &lt; 0 and ARGV[2] != '0'.
 * If the result is outside +/-9007199254740991 (JS Number.MAX_SAFE_INTEGER,
 * dual-runtime twin of .NET), reverses DECRBY in-script and errors
 * `ERR safe_integer_overflow` so the store is never left out of range.
 */
export const INCREMENT_WITH_OPTIONAL_TTL = `
local result = redis.call('INCRBY', KEYS[1], ARGV[1])
if result > 9007199254740991 or result < -9007199254740991 then
    redis.call('DECRBY', KEYS[1], ARGV[1])
    return redis.error_reply('ERR safe_integer_overflow')
end
if ARGV[2] ~= '0' and redis.call('PTTL', KEYS[1]) < 0 then
    redis.call('PEXPIRE', KEYS[1], ARGV[2])
end
return result
`.trim();

/** Atomic compare-and-delete lock release. Returns 1 if removed, else 0. */
export const RELEASE_LOCK_IF_OWNER = `
if redis.call('GET', KEYS[1]) == ARGV[1] then
    return redis.call('DEL', KEYS[1])
else
    return 0
end
`.trim();

/**
 * Atomic compare-and-set. Only SETs the key when the stored value matches
 * ARGV[1] (the expected serialized value). ARGV[2] = the new serialized
 * value; ARGV[3] = TTL in milliseconds (0 = no expiration). Returns 1 if
 * written, 0 on mismatch or an absent key.
 */
export const SET_IF_EQUAL = `
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
`.trim();

/**
 * Atomic compare-and-delete. Only DELs the key when the stored value matches
 * ARGV[1] (the expected serialized value). Returns 1 if removed, 0 on
 * mismatch or an absent key. The body matches {@link RELEASE_LOCK_IF_OWNER},
 * and the two are kept apart because they compare different things: a lock
 * holder's raw identifier there, a serialized payload here.
 */
export const REMOVE_IF_EQUAL = `
if redis.call('GET', KEYS[1]) == ARGV[1] then
    return redis.call('DEL', KEYS[1])
else
    return 0
end
`.trim();

/** Atomic SADD + optional PEXPIRE when PTTL < 0 and ARGV[2] != '0'. */
export const SET_ADD_WITH_OPTIONAL_TTL = `
local added = redis.call('SADD', KEYS[1], ARGV[1])
if ARGV[2] ~= '0' and redis.call('PTTL', KEYS[1]) < 0 then
    redis.call('PEXPIRE', KEYS[1], ARGV[2])
end
return added
`.trim();

/**
 * Atomic token-bucket refill-then-consume. ARGV[1] = tokens added per
 * interval; ARGV[2] = that interval in milliseconds; ARGV[3] = bucket
 * capacity; ARGV[4] = tokens this request costs. Returns
 * `{admitted, retry_after_ms}`, where `admitted` is 1 or 0 and the wait is 0
 * for an admitted request.
 *
 * Tokens are held as thousandths so the refill stays integer arithmetic: one
 * elapsed millisecond adds a fraction of a token at most rates, and a bucket
 * that floored that fraction to zero would never refill at all.
 *
 * The `rem` field stores the part of the accrual that did not amount to a
 * whole thousandth, and the next call adds it back before dividing. A bucket
 * keyed by one address takes arrivals far more often than one thousandth of a
 * token takes to accrue, and a division that dropped its remainder each time
 * would refill at a rate the arrivals themselves reduced, down to no refill at
 * all. The stored value is always under one interval, and a bucket that reaches
 * its capacity resets it to zero.
 *
 * The instant comes from `TIME`, the store's own clock, which every node
 * shares, so two nodes whose clocks disagree still charge one bucket
 * consistently. A stored instant later than the store's own now (a clock
 * stepped backwards) refills nothing rather than draining the bucket.
 *
 * The key's lifetime is a refill from empty to full plus one interval, so a
 * bucket nobody touches again is dropped, and a caller returning after that
 * lifetime finds the full bucket the refill would have given it anyway.
 */
export const CONSUME_TOKEN = `
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
`.trim();
