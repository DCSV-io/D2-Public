<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Caching.Abstractions

Shared abstractions for the D2 cache stack, used by `DcsvIo.D2.Caching.Local.Default` (per-process),
`DcsvIo.D2.Caching.Distributed.Redis` (cluster), `DcsvIo.D2.Caching.Tiered` (composed), and every consumer.

## Install

```bash
dotnet add package DcsvIo.D2.Caching.Abstractions
```

## Public surface

### Building blocks

The fine-grained interfaces. Implementations declare which they support; marker interfaces compose
them.

- `ICacheBasic` - `Get` / `GetMany` / `Exists` / `GetTtl` / `Set` / `SetMany`
  / `Remove` / `RemoveMany`
- `ICacheAtomic` - `SetNx` / `SetIfEqual` / `RemoveIfEqual` / `Increment` /
  `ConsumeToken` / `AcquireLock` / `ReleaseLock`
- `ICacheBroadcast` - `SetAndBroadcast` / `SetManyAndBroadcast` /
  `RemoveAndBroadcast` / `RemoveManyAndBroadcast`
- `ICacheSet` - `SetAdd` / `SetCardinality` / `SetRemove` / `SetContains`
  (cluster-only - Redis SADD/SCARD/SREM/SISMEMBER)

All operations return `D2Result<T>` / `D2Result`.

Null or empty inputs (missing key, missing keys collection, missing entries dictionary) return
`D2Result.ValidationFailed` with an `InputError` that identifies the offending parameter (built with the shared
`InputFailures.Required(...)` helper). Implementations never throw `ArgumentException` for per-call caller mistakes:
every per-call failure is reported on the result. Construction-time and DI-registration errors are not per-call failures
and do surface as exceptions; see the `*AndBroadcast*` exception in [Result mapping](#result-mapping).

### Cache key convention

Cache keys follow the `EntityName:{id}` shape, for example `Order:{orderId}`, `Jwks:{kid}` and
`WhoIs:{ip}`. The `LocalCacheOptions.KeyPrefix` is a namespace prefix added in front of that shape,
useful when multiple caches share a process. It does not replace the shape. Keep personal data out
of keys: keys appear in logs, traces, and Redis MONITOR / OBJECT inspection. Hash any
user-supplied identifier first.

A key reaches a log sink verbatim, by design: the tiered cache writes it into `L1WriteFailedAfterL2Success`,
`L1DropFailed`, `L1InvalidationFailed` and `InvalidationPublishFailed`, because an operator reading one of those log
lines needs the key to find the entry. A key carrying a personal value therefore puts that value in every sink the host
ships logs to, which the rule to keep personal data out of keys forbids.

### Marker interfaces

These are the types DI resolves. The marker name states the cache scope at the dependency site, so a
reader of a constructor parameter sees the behavioral profile without checking the registration.

`ILocalCache` composes `ICacheBasic` and `ICacheAtomic`.
Per-process scope. Atomic operations at process scope. No broadcast (nothing outside this
process can see local cache state). Use for instance-scoped data: per-instance
fingerprint cache, per-instance counters, hot in-process lookups.

`IDistributedCache` composes `ICacheBasic`, `ICacheAtomic`, `ICacheBroadcast` and `ICacheSet`.
Cluster scope. Atomic operations cluster-wide. Every read goes to the remote store (no L1).
Use when freshness matters more than read speed: rate-limit counters, distributed
locks, ephemeral session lookups.

`ITieredCache` composes `ICacheBasic`, `ICacheAtomic` and `ICacheBroadcast`. Composed L1+L2. A read checks L1 first,
falls through to L2 on a miss, and populates L1. Writes go L2-first (L1 only if L2 succeeded). Atomic operations go
through L2 and invalidate L1 as a side effect. Use for read-heavy entity data where freshness within a few seconds is
acceptable. It does NOT compose `ICacheSet`: set primitives are cluster-only and tiered composition would silently hide
their cluster-wide nature. Callers needing SADD/SCARD inject `IDistributedCache` directly.

`IDistributedCache` and `ITieredCache` compose the same building blocks except `ICacheSet`, which only
`IDistributedCache` composes, so the methods of the other building blocks are identical. The marker
distinction is behavioral: DI registration determines which concrete implementation a consumer
gets, and the parameter type at the call site tells the reader whether the consumer uses a
single-tier remote cache or a two-tier composed cache. The implementations behave differently; the
shared building blocks have the same contract.

### Supporting types

`LocalCacheOptions` - `MaxEntries` (default 100_000), `DefaultExpiration` (default 1h),
`KeyPrefix` (default empty).

`InputFailures` - pre-built `D2Result.ValidationFailed` factories
(`Required<T>(paramName)` / `Required(paramName)` / `Invalid<T>(paramName)`, plus
`TokenBucket(...)` for the argument checks every flavor's token bucket shares). Implementations use
them so the cache surface returns errors as values and throws no `ArgumentException` for
per-call caller mistakes. Constructors and DI registration still throw, because those errors occur
at registration, not per call.

`TokenBucketDecision` - the result of one `ConsumeTokenAsync` call: `Admitted`, and
`RetryAfter`, the wait until the bucket refills to the cost the caller asked for
(zero on an admitted request).

`ICacheSerializer` - pluggable serialization for distributed caches. The default
implementation in `distributed-redis` is JSON; provider-specific implementations may use
MessagePack or Protobuf instead. Local caches store objects directly and do not need this.

`ICacheInvalidationBackplane` - optional pub/sub backplane for cross-instance
L1 invalidation. Tiered consumers subscribe at construction, and `*AndBroadcast*`
writes publish on every send. The interface is provider-agnostic: Redis pub/sub,
Postgres LISTEN/NOTIFY or an in-process implementation for tests can implement it.

Provider-specific options (Redis connection string, Sentinel topology, channel prefixes for pub/sub,
retry config, etc.) live on the implementation's own options class. The abstractions define no
shared "DistributedCacheOptions" base class, because each distributed implementation needs its own
provider-specific surface anyway, and the few common fields (`DefaultExpiration`, `KeyPrefix`) are
easier to redeclare in each implementation than to inherit. `DefaultTieredCache` declares no options
class.

## Result mapping

- `GetAsync<T>` - hit -> `Ok(value)`; miss -> `NotFound`; backing-store error ->
  failure.
- `GetManyAsync<T>` - all hit -> `Ok(dict)`; some hit -> `SomeFound(partialDict)`;
  none -> `NotFound`.
- `SetAsync<T>` / `RemoveAsync` - `Ok` (idempotent); failure on backing-store
  error.
- `ExistsAsync` - `Ok(true)` if present, `Ok(false)` if not.
- `GetTtlAsync` - key + TTL -> `Ok(span)`; key but no TTL -> `Ok(null)`; absent
  -> `NotFound`.
- `SetNxAsync<T>` - wrote -> `Ok(true)`; already existed -> `Ok(false)`.
- `SetIfEqualAsync<T>` - stored value equalled `expected` and was replaced ->
  `Ok(true)`; it differed, or the key was absent -> `Ok(false)`, nothing written.
  `expected` must be a type whose equality is by value in every flavor; a
  `record` of primitives satisfies it, an array does not.
- `RemoveIfEqualAsync<T>` - stored value equalled `expected` and the key was
  removed -> `Ok(true)`; it differed, or the key was absent -> `Ok(false)`,
  nothing removed. Same by-value comparison rule as `SetIfEqualAsync<T>`. An
  owner-scoped release calls this member: it reads the entry, then removes it
  only while it is still the entry that was read.
- `IncrementAsync` - `Ok(newValue)`; type mismatch (Redis WRONGTYPE parity) ->
  `Conflict`.
- `ConsumeTokenAsync` - refills the bucket by the time elapsed since its last
  use, clamps to the capacity, then consumes the cost: `Ok(admitted, zero
  wait)` when the bucket held it, `Ok(refused, the wait until it holds it)`
  when it did not. The store's clock supplies the instant, so there is no window
  boundary to spend an allowance twice across. A key holding something else ->
  `Conflict`.
- `AcquireLockAsync` - acquired -> `Ok(true)`; held by other -> `Ok(false)`.
- `ReleaseLockAsync` - `Ok` (idempotent - no-op if not held by this caller).
- `*AndBroadcast*` - same as their plain counterparts. Throws
  `InvalidOperationException` if no `ICacheInvalidationBackplane` is
  registered. This is a registration error, not a per-call failure, so it falls
  under the same exception as construction-time errors.
- `SetAddAsync` - new member -> `Ok(true)`; already present -> `Ok(false)`.
- `SetCardinalityAsync` - `Ok(count)`; absent set -> `Ok(0)`.
- `SetRemoveAsync` - was present -> `Ok(true)`; was absent -> `Ok(false)`
  (idempotent).
- `SetContainsAsync` - `Ok(true|false)`.

## Broadcast variants - when to use

The `*AndBroadcast*` methods write or remove AND publish an invalidation message. Other instances'
tiered caches subscribe to the backplane and drop their L1 copies on receipt, which keeps L1
caches across the cluster in sync without polling.

Use the broadcast variant when:

- The data is shared across instances (user profile, org settings, contact info, JWKS).
- A user-initiated remove must be visible cluster-wide quickly.
- Coordinated state changes that depend on every instance seeing the new state.

Use the plain (non-broadcast) variant when:

- Cache-warming / startup seed (no other instance has a stale copy of a key that didn't exist).
- Single-writer-single-reader keys (derived from this instance's process ID, etc.).
- Hot-path writes where the broadcast cost dominates (counter ticks, telemetry).
- Refresh writes of effectively-the-same data.
- Single-instance deployments (register no backplane).
- Short-TTL data where staleness is naturally bounded.

The `*AndBroadcast*` suffix is searchable, so a reviewer can see which writes propagate
across the cluster.

## The invalidation backplane

`ICacheInvalidationBackplane` is the cross-instance pub/sub interface behind the `*AndBroadcast*`
writes and any other flow that tells every instance to drop key K from its L1.

### Every subscriber receives every message

Every subscriber receives every message, including messages this instance itself published.
There is no sender-ID filter. The cost of self-receive is bounded:

- Tiered cache after `SetAndBroadcastAsync` - the publisher's L1 entry just got dropped; next read
  of K re-fetches from L2 (one extra round-trip, bounded by write rate)
- Tiered cache after `RemoveAndBroadcastAsync` - L1 was already removed; receive is a no-op
- Other callers, such as a session-revoke flow, usually want their OWN instance's L1 to
  drop too, so self-receive is correct behavior there, not a cost

The rule needs no flags, no per-call config and no caller bookkeeping. A session-revoke flow works
because the caller's L1 drops alongside everyone else's.

### Subscribe pattern

```csharp
public sealed class MySubscriber : IAsyncDisposable
{
    private readonly IAsyncDisposable _subscription;

    public MySubscriber(ICacheInvalidationBackplane backplane)
    {
        _subscription = backplane.Subscribe(async (key, ct) =>
        {
            // Handle the invalidation. Async-native; isolated from other
            // subscribers' handlers (an exception here doesn't affect them).
            await DoSomethingWithKey(key, ct);
        });
    }

    public ValueTask DisposeAsync() => _subscription.DisposeAsync();
}
```

`Subscribe` returns an `IAsyncDisposable`; the subscriber holds it as a field and disposes it when
the subscriber itself is disposed. Lifetime tracking is explicit, so an `event +=` without a
matching `-=` cannot occur.

The tiered cache subscribes in its constructor, so registering `services.AddD2TieredCache()` is
enough to subscribe. A subscriber outside the cache library (one that holds state of its own)
calls `Subscribe(handler)` in its own constructor.

## Atomic on tiered - how it works

`ITieredCache` exposes the same atomic surface as `IDistributedCache` because the atomicity
guarantee comes from L2 (the cluster source of truth) regardless of L1's role. Each atomic member
behaves as follows:

- `IncrementAsync` -> call L2's atomic increment. On success, invalidate L1 (force next read to
  repopulate from canonical L2 value) + broadcast invalidation. Counters are usually contested
  across instances; holding a stale local copy after-increment is worse than the tiny re-read cost.
- `SetNxAsync` -> call L2's SetNx. On success, write L1 + broadcast invalidation. On fail (key
  already existed in L2), invalidate L1 (in case L1 had a stale entry) + don't broadcast.
- `SetIfEqualAsync` -> call L2's compare-and-set. Invalidate L1 on every outcome, unlike
  `SetNxAsync`: the comparison is decided at L2, and a backplane invalidation arriving between
  the L2 write and an L1 populate would be applied first, leaving this node holding the value
  the shared store has already superseded.
- `RemoveIfEqualAsync` -> call L2's compare-and-delete, then invalidate L1 on every
  outcome, in the order `SetIfEqualAsync` uses for its write and for the same reason.
- Both conditional writes broadcast on the arm where the write took effect, where a backplane is
  registered: that arm superseded the shared value, so every other instance's local copy is the
  value it replaced. The arm where the comparison lost changed nothing and broadcasts nothing. A
  failed publish is logged and does not change the result the member returns.
- `ConsumeTokenAsync` -> pure delegation to L2. The bucket state belongs to the shared
  store, and a local copy of it would describe one node, not the cluster.
- `AcquireLockAsync` / `ReleaseLockAsync` -> pure delegation to L2. L1 isn't involved - lock
  state is a coordination primitive, not a cached value.

L1 is never authoritative for atomic state; L2 is. L1 only mirrors L2 or is invalidated.

## Dependencies

- `DcsvIo.D2.Result` - every op returns `D2Result<T>`
- `DcsvIo.D2.I18n.Abstractions` - `TKMessage` for typed default error messages
- `DcsvIo.D2.I18n.Keys` - the `TK` constants those messages use
- `DcsvIo.D2.Utilities` - `Falsey()`, the emptiness check `InputFailures.TokenBucket` uses. It
  declares no runtime NuGet package, so a consumer of these abstractions takes only
  `JetBrains.Annotations`, a compile-time attributes package, in its closure.

These are the only runtime dependencies (no DI, no logging, no provider libraries), so any handler,
repository or domain code can declare a cache dependency without taking in an implementation's runtime.

## References

- `DcsvIo.D2.Caching.Local.Default` - `DefaultLocalCache` impl
- `DcsvIo.D2.Caching.Distributed.Redis` -
  `RedisDistributedCache` + `RedisCacheInvalidationBackplane` impls
- `DcsvIo.D2.Caching.Tiered` - `DefaultTieredCache` (L1+L2 composition)
