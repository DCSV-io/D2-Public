<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Caching.Distributed.Redis

Redis-backed implementation of `IDistributedCache` and
`ICacheInvalidationBackplane`. Wraps `StackExchange.Redis`.

## Install

```bash
dotnet add package DcsvIo.D2.Caching.Distributed.Redis
```

## Public surface

`RedisDistributedCache : IDistributedCache` - implements every building block
(`ICacheBasic` + `ICacheAtomic` + `ICacheBroadcast` + `ICacheSet`) over a
`StackExchange.Redis.IConnectionMultiplexer`.

`RedisCacheInvalidationBackplane : ICacheInvalidationBackplane` - pub/sub-backed
invalidation channel. It subscribes once at construction and dispatches each message to all
registered handlers, including messages this instance published (the "everyone acts" rule).

`JsonCacheSerializer : ICacheSerializer` - default value serializer, built on
`System.Text.Json`. Redis CLI can inspect its values directly. Replace it by registering
another `ICacheSerializer`.

`RedisCacheOptions` - connection string, default TTL, key prefix, channel name,
command/connect timeouts, retries.

`services.AddD2DistributedCacheRedis(opts => ...)` - registers
`RedisDistributedCache` + the underlying `IConnectionMultiplexer`.

`services.AddD2RedisCacheInvalidationBackplane()` - opt-in registration of the
pub/sub backplane. Required for `*AndBroadcast*` methods on the cache (and on
tiered caches that compose it).

## Lua scripts (internal)

The Lua scripts live in `RedisLuaScripts.cs`. Each runs a compound atomic operation in a single
round-trip:

- `INCREMENT_WITH_OPTIONAL_TTL` - `INCRBY` + conditional `PEXPIRE`. Used by `IncrementAsync`.
  Sets the TTL only when explicitly requested; existing TTLs survive subsequent increments.
- `RELEASE_LOCK_IF_OWNER` - compare-and-delete. `ReleaseLockAsync` only DELs if the stored value
  matches the caller's `lockId`. This prevents the ABA problem, in which a stale releaser would
  release a lock that someone else now holds.
- `SET_ADD_WITH_OPTIONAL_TTL` - `SADD` + conditional `PEXPIRE` on first add. Sets TTL only when
  the set is being created; existing sets keep their TTL.
- `SET_IF_EQUAL` - compare-and-set, the same shape as `RELEASE_LOCK_IF_OWNER` with a `SET` in
  place of the `DEL`. Used by `SetIfEqualAsync`. An absent key returns 0 and nothing is written, so a
  caller holding a copy of what used to be there cannot resurrect it. The dual-runtime constants
  fixture pins its body byte-for-byte against the TypeScript implementation.
- `REMOVE_IF_EQUAL` - compare-and-delete, used by `RemoveIfEqualAsync`. Its body matches
  `RELEASE_LOCK_IF_OWNER`, and the two are kept apart because they compare different things: a lock
  holder's raw identifier there, a serialized payload here. An absent key returns 0.
- `CONSUME_TOKEN` - token-bucket refill-then-consume, used by `ConsumeTokenAsync`. Refills by
  the elapsed time the server's own `TIME` reports, clamps to the capacity, then consumes, and
  returns `{admitted, retry_after_ms}`. Tokens are held as thousandths so the refill stays integer
  arithmetic. The dual-runtime constants fixture pins its body byte-for-byte against the TypeScript
  implementation. Clock rollback preserves the last accrual timestamp and fractional remainder;
  recovery to that timestamp does not credit elapsed time twice.

Lua is intentionally NOT exposed as a public surface (`IRedisScriptExecutor` etc.). The scripts are an
implementation detail of the Redis implementation.

## Error handling

- Connection lost / timeout (`RedisException`) -
  `D2Result.ServiceUnavailable` (graceful degradation; caller decides fail-open
  vs fail-closed via Result inspection).
- WRONGTYPE on Increment (key holds a non-string data structure) -
  `D2Result.Conflict`.
- "value is not an integer or out of range" on Increment (key holds a
  non-numeric string) - `D2Result.Conflict`.
- Serialization failure - `D2Result` with error code
  `COULD_NOT_BE_SERIALIZED` / `COULD_NOT_BE_DESERIALIZED`.

## Connection management

`AddD2DistributedCacheRedis(opts)` registers `IConnectionMultiplexer` as a singleton - one
connection pool shared across the app. The connection is built from
`RedisCacheOptions.ConnectionString` (StackExchange format:
`host:port[,host:port],password=...,ssl=true`). Sentinel and Cluster topologies are inferred from
the connection string.

Defaults:

- `AbortOnConnectFail = false` - graceful degradation if Redis isn't up at startup; cache calls
  return `ServiceUnavailable` until it comes back.
- `CommandTimeout = 2s`, `ConnectTimeout = 5s`, `ConnectRetries = 3` - configurable per
  `RedisCacheOptions`.

## Observability

Static `Meter` (`DcsvIo.D2.Caching.Distributed.Redis`) emits aggregate counters:
`d2.cache.redis.{hits, misses, sets, removes, broadcasts, errors}`. The cache emits no per-call span: Redis operations
are network-bound (1-5ms), and a per-call ActivitySource span would dominate the operation's cost at typical cache rates
without adding information worth that cost. The aggregate counters show whether the cache is healthy (hit rate, error
rate). Per-call observability is left to the consuming handler's own pipeline.

## Test coverage

`Integration/Caching/Distributed/` under `packages/dotnet/tests/` (xunit collection `"Redis"`,
shared Testcontainers Redis fixture):

- `RedisDistributedCacheTests` - full op surface against real Redis. Includes:
  - `SetNxAsync_Concurrent_OnlyOneWinsAcrossCluster` - 32 contenders, exactly one wins (cluster-wide
    atomic SET NX)
  - `IncrementAsync_Concurrent_AggregatesAtomically` - 8x200 increments aggregate to exactly 1600
    (cluster-wide atomic INCR via Lua)
  - `IncrementAsync_OnNonNumeric_ReturnsConflict` - Redis WRONGTYPE / "not an integer" -> `Conflict`
    (verifies both paths)
  - `SetAddAsync_Concurrent_BuildsCorrectCardinality` - 100 concurrent SADDs with 30 distinct values
    -> SCARD = 30
  - `AcquireReleaseLock_RoundTrip` + `ReleaseLockAsync_WrongOwner_DoesNotRelease` - Lua
    compare-and-delete prevents ABA
- `RedisCacheInvalidationBackplaneTests` - pub/sub backplane against real Redis. Verifies:
  - The "everyone acts" rule (publisher receives own messages)
  - Multi-subscriber independence (each gets every message)
  - Error isolation (one throwing handler doesn't break others)
  - Disposal stops handler invocation
  - Cross-instance delivery (publish on instance A -> received on instance B)

The test container is launched once per test run via `Testcontainers.Redis` and shared across all
`[Collection("Redis")]` test classes. Each test uses a fresh key prefix / channel name for
isolation.

## Dependencies

- `DcsvIo.D2.Caching.Abstractions` - `IDistributedCache`,
  `ICacheInvalidationBackplane`, `ICacheSerializer`
- `StackExchange.Redis` - Redis client
- `Microsoft.Extensions.Options`, `Microsoft.Extensions.DependencyInjection.Abstractions`,
  `Microsoft.Extensions.Logging.Abstractions`

## References

- `DcsvIo.D2.Caching.Abstractions` - interface + result-mapping
  reference
- `DcsvIo.D2.Caching.Tiered` - composes the local cache and this library into
  L1+L2 cascades
