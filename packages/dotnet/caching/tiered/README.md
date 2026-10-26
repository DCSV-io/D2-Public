<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Caching.Tiered

Two-tier cache that composes one `ILocalCache` (L1) and one
`IDistributedCache` (L2) into the
`ITieredCache` marker interface.

## Install

```bash
dotnet add package DcsvIo.D2.Caching.Tiered
```

## Public surface

`DefaultTieredCache : ITieredCache, IAsyncDisposable` - the implementation.

`services.AddD2TieredCache()` - DI registration. Singleton. Requires
`ILocalCache` + `IDistributedCache` to be registered first.

## Behavior

Reads (`GetAsync` / `GetManyAsync` / `ExistsAsync`):

- Try L1 first
- On L1 miss, fall through to L2
- On L2 hit, populate L1 with the value (next read on this instance is sub-microsecond)
- Return the result

Writes (`SetAsync` / `SetManyAsync` / `RemoveAsync` / `RemoveManyAsync`):

- L2 first - if L2 fails, return that failure and do not touch L1
- L1 second - only fires after L2 succeeded
- If L1 fails after L2 succeeded, the L2-success result is what's returned - the L1 failure is
  logged at Warning (`L1WriteFailedAfterL2Success`) and swallowed. The reason is graceful
  degradation: L1 is the optional layer, and an L1 failure on this instance must not fail a write the
  cluster has successfully accepted. Next read on this instance re-fetches from L2.
- L2-first ordering means partial-write states are impossible; the result is binary (L2's outcome,
  with any L1 failure logged separately).

Atomic primitives (`SetNxAsync` / `SetIfEqualAsync` / `RemoveIfEqualAsync` / `IncrementAsync` /
`ConsumeTokenAsync` / `AcquireLockAsync` / `ReleaseLockAsync`):

- Route through L2 (the cluster source of truth)
- After L2 succeeds, invalidate L1 so the next read fetches the canonical value
  - For `SetNxAsync`: if L2 took the write, populate L1 with the same value; if L2 already had the
    key, drop our (potentially stale) L1
  - For `SetIfEqualAsync`: drop L1 on every outcome, unlike `SetNxAsync` - the comparison is
    decided at L2, and an invalidation arriving between the L2 write and an L1 populate would be
    applied first, leaving this node holding a superseded value
  - For `RemoveIfEqualAsync`: drop L1 on every outcome, in the order `SetIfEqualAsync` uses
    for its write and for the same reason
  - For `SetIfEqualAsync` and `RemoveIfEqualAsync`, dropping this node's L1 is not enough on its
    own: the write the comparison won replaced the value every other instance may hold locally.
    Where a backplane is registered, both members publish the key on the arm where the write took effect,
    so the other instances drop their copies. The arm where the comparison lost changed nothing at
    L2 and publishes nothing. A publish that fails is logged (`InvalidationPublishFailed`) and does
    not change the result the member returns, so the other instances serve their local copy until
    it expires. With no backplane registered there is nothing to publish on, and the other
    instances stay stale until TTL
  - For `IncrementAsync`: always invalidate L1 (counters held in L1 would diverge from L2
    immediately as other instances increment)
  - For `ConsumeTokenAsync`: pure delegation; the bucket state belongs to the shared store, and a
    local copy of it would describe one node, not the cluster
  - For `AcquireLockAsync` / `ReleaseLockAsync`: pure delegation; L1 isn't involved in lock state at
    all

Broadcast variants (`SetAndBroadcastAsync` etc.):

- Do the underlying write, then publish via the registered `ICacheInvalidationBackplane`
- All subscribers (across every connected instance, including this one) receive the key and drop
  their L1 entry - the "everyone acts" rule
- The publishing instance also receives its own message and drops L1; next read re-fetches from L2
  (one extra round-trip - bounded cost; acceptable for read-heavy use cases)

## Backplane subscription

If an `ICacheInvalidationBackplane` is registered, the tiered cache subscribes to it in its
constructor. Every received invalidation key triggers a `RemoveAsync` on the L1 cache, which is
how L1 stays consistent with L2. If the L1 `RemoveAsync` fails, the failure is logged at Warning
(`L1InvalidationFailed`) and processing continues; L1 stays stale on this instance until TTL or the
next write/broadcast for that key. The subscription is disposed when the tiered cache is disposed.

If no backplane is registered, `*AndBroadcast*` methods throw `InvalidOperationException`. The plain
ops (`SetAsync` / `RemoveAsync` etc.) work; L1 caches drift from each other until their TTLs
expire.

## Design rationale: no `ICacheSet`

`ITieredCache` deliberately does NOT implement `ICacheSet` (SADD/SCARD). Set-cardinality is inherently cluster-only - an
L1 set would only see this instance's adds, and cluster cardinality lives in L2, so a set composed across L1+L2 would
misrepresent the cluster cardinality. Callers needing set primitives inject `IDistributedCache` directly.

## Test coverage

`Integration/Caching/Tiered/` under `packages/dotnet/tests/` (xunit collection `"Redis"`, shared
Testcontainers Redis fixture):

- `DefaultTieredCacheRedisTests` - end-to-end against real L1 (IMemoryCache) + real L2 (Redis):
  - `Get_L1Miss_PopulatesFromL2`
  - `Set_WritesToBoth` / `Remove_RemovesFromBoth`
  - `SetAndBroadcast_OtherInstanceL1Drops` - two tiered instances on the same backplane; A writes,
    B's L1 receives the invalidation and drops
  - `SetAndBroadcast_OwnInstanceL1AlsoDrops` - the "everyone acts" rule verified end-to-end
  - `IncrementAsync_RoutesThroughL2_InvalidatesL1`
  - `RemoveAndBroadcast_OtherInstancesDropL1`

## Dependencies

- `DcsvIo.D2.Caching.Abstractions` - `ITieredCache`, `ILocalCache`,
  `IDistributedCache`, `ICacheInvalidationBackplane`
- No backing-store deps directly - the impl is pure composition

## References

- `DcsvIo.D2.Caching.Abstractions` - interface + result-mapping
  reference
- `DcsvIo.D2.Caching.Local.Default` - typical L1 impl
- `DcsvIo.D2.Caching.Distributed.Redis` - typical L2 impl
