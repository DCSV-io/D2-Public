<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Caching.Local.Default

Default per-process implementation of `ILocalCache`. Wraps
`Microsoft.Extensions.Caching.Memory.IMemoryCache` for value storage; uses a `ConcurrentDictionary`
for in-process lock state.

## Install

```bash
dotnet add package DcsvIo.D2.Caching.Local.Default
```

## Public surface

`DefaultLocalCache : ILocalCache, IDisposable` - the implementation.

`LocalCacheOptions` - `MaxEntries` (default 100_000), `DefaultExpiration` (1h),
`KeyPrefix` (empty). The default is sized for production workloads where
session-liveness, JWKS, token-exchange, and other per-process caches share a single
singleton; ~100 MB process RSS worst-case at ~1 KB / entry.

`services.AddD2LocalCache(opts => ...)` - DI registration. Singleton lifetime.

## Design rationale: no BaseHandler wrapping

Every other shared lib that does I/O wraps each operation in `BaseHandler` for OTel spans +
structured logs + per-call metrics + a catch-all try/catch. This lib does not, on purpose.

`IMemoryCache.TryGetValue` costs tens of nanoseconds with zero allocations on .NET 8+. The BaseHandler pipeline
is ~1-5 microseconds with allocations (ActivitySource start, Stopwatch, ILogger.BeginScope dict alloc,
D2Result alloc, 4 metric instrument calls). For an op that does 60 ns of real work, handler overhead
is 100x the work itself, and per-call cache spans add noise to traces that is never useful.

For distributed cache (Redis: 1-5 ms network round-trip) the same handler overhead is 0.1-0.5% -
there it stays.

Local cache calls its methods directly and uses a static `Meter` for aggregate counters
(`d2.cache.local.hits` / `.misses` / `.sets` / `.removes` / `.evictions`). The Meter instruments cost
almost nothing (an atomic increment), and the aggregate counters show cache health
(hit rate, eviction pressure).

## Design rationale: `IMemoryCache` over self-rolling LRU

`IMemoryCache` is `ConcurrentDictionary`-backed (confirmed by Microsoft Learn) - lock-free reads
with striped writes. The actual cache work is hardware-accelerated. The usual guidance
(BitFaster, FusionCache, ASP.NET Core internals) is "wrap `IMemoryCache` for typical workloads;
switch to `BitFaster.Caching.ConcurrentLru` only if profiling shows contention." At the default
100_000-entry ceiling and normal CRUD throughput, MemoryCache's known pitfalls (`_cacheSize`
contention, compaction task explosion) do not apply.

Strict-LRU semantics - which IMemoryCache doesn't give (it's "compaction by priority + age") - would
require an exclusive lock per read (move-to-front on a linked list). That would remove the read
concurrency that `ConcurrentDictionary` provides. Do not write your own LRU unless you have a measured
reason.

A strict-LRU or strict-FIFO implementation can replace this one by changing the `ILocalCache`
registration, with no call-site change.

## The SizeLimit pitfall and how this cache avoids it

`IMemoryCache` only enforces `SizeLimit` if every `Set` call provides a `Size` value on the entry.
Without it, the cache grows unbounded (eviction only fires on system-wide memory pressure - see
dotnet/runtime#114714). The configured entry ceiling would not hold.

This impl always sets `entry.Size = 1` internally. Caller never sees `Size` on the API surface.
`MaxEntries` is therefore the actual entry-count cap most callers expect. `CompactionPercentage` is left
at the default 0.05 (single overshoot evicts 5% rather than triggering a large sort+evict pass).

## Atomic primitives at process scope

- `SetNxAsync` - atomic via `IMemoryCache.TryGetValue` + write-if-absent under per-cache lock.
- `SetIfEqualAsync` - same per-cache lock. Compares the stored object with
  `EqualityComparer<T>.Default`, so `expected` must be a type whose equality is by value;
  a `record` of primitives is, an array is not. A present null matches an expected null;
  an absent key does not match.
- `RemoveIfEqualAsync` - the removing sibling, under the same per-cache lock and the same
  by-value comparison. The removal drops the entry's recorded lifetime with it, so a removed key
  reports no TTL. Present null and absence have the same distinct meanings as in `SetIfEqualAsync`.
- `IncrementAsync` - `lock` on the cache instance for the read-modify-write window. Counter
  values stored in the same key namespace as regular values (Redis WRONGTYPE parity); incrementing a
  key that holds a non-`long` value returns `D2Result.Conflict`.
- `ConsumeTokenAsync` - the token bucket, under the same per-cache lock, measured on the
  injected `TimeProvider`. Its state is held in the same key namespace as regular values, so a key
  holding something else returns `D2Result.Conflict` as `IncrementAsync` does. Clock rollback
  preserves the last accrual timestamp and fractional remainder; recovery to that timestamp
  does not refill the bucket again.
- `AcquireLockAsync` / `ReleaseLockAsync` - `ConcurrentDictionary<string, LockEntry>` with
  `(lockId, expiresAt)`. Acquire uses `AddOrUpdate` with expiration check; release uses
  compare-and-remove keyed on the lockId. Locks expire automatically, so a crashed caller does not
  hold a lock forever.

Every one of them operates at process scope. They guarantee atomicity within this instance; cluster-wide
coordination requires `IDistributedCache`.

## Test coverage

The tests are under `packages/dotnet/tests/`.

- Unit tests (`Unit/Caching/Local/DefaultLocalCacheUnitTests.cs`) - surface tests on D2Result
  mapping, argument validation, key prefixing, idempotency, TTL semantics (preservation across
  Increment, rejection of non-positive values), per-entry validation in multi-key ops. Fast.
- Behavior tests (`Unit/Caching/Local/DefaultLocalCacheBehaviorTests.cs`) - exercise real
  `IMemoryCache` semantics: capacity-driven eviction, real TTL expiration with wall-clock waits,
  16-32 thread concurrency stress on Increment + AcquireLock, race-protection probes for SetCore +
  SetNx contender races. They stay in the unit tier because no external infra is required (in-process
  `IMemoryCache` covers everything).

## Notes

- Calls on a disposed instance throw `ObjectDisposedException`, matching `IMemoryCache`'s own
  contract. The "ops never throw" guarantee applies to per-call data flow under a live instance, not
  to misuse-after-dispose. Don't share the cache instance between the disposing scope and any
  in-flight async caller.
- `GetTtlAsync` may transiently report no-TTL after a Capacity eviction - see the
  `r_expirations` field doc on `DefaultLocalCache.cs`. Workloads needing strict-LRU-with-coherent-TTL
  semantics should use another `ILocalCache` implementation instead of relying on this one's tradeoffs.

## Dependencies

- `DcsvIo.D2.Caching.Abstractions` - `ILocalCache` + options
- `Microsoft.Extensions.Caching.Memory` - backing store
- `Microsoft.Extensions.Options` - DI options binding
- `Microsoft.Extensions.DependencyInjection.Abstractions` - registration

## References

- `DcsvIo.D2.Caching.Abstractions` - interface + result-mapping
  reference
- `DcsvIo.D2.Caching.Distributed.Redis` - cluster-scoped
  counterpart
- `DcsvIo.D2.Caching.Tiered` - composes this lib as the L1 layer
