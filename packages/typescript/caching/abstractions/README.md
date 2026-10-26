<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-caching-abstractions

.NET mirror: `DcsvIo.D2.Caching.Abstractions`

Node services and backend-for-frontend (BFF) code inject these cache ports without pulling
Redis, logging, or DI wiring into domain code. The package is the TypeScript equivalent of
`DcsvIo.D2.Caching.Abstractions`: marker interfaces (`ILocalCache` /
`IDistributedCache` / `ITieredCache`), fine-grained building blocks
(`ICacheBasic` / `ICacheAtomic` / `ICacheBroadcast` / `ICacheSet`), plus
`ICacheInvalidationBackplane`, `ICacheSerializer`, `InputFailures`,
`TokenBucketDecision`, and `LocalCacheOptions` / `LOCAL_CACHE_DEFAULTS`. Every operation returns
`Promise<D2Result<...>>` / `D2Result<...>` via `@dcsv-io/d2-result`. The package ships no
implementations, only contracts and pure helpers.

## Install

```bash
pnpm add @dcsv-io/d2-caching-abstractions
```

## Public surface - building blocks

Fine-grained interfaces. Implementations declare which they support; marker
interfaces compose them. Method names are camelCase (the .NET `Async` suffix is dropped);
cancellation is optional `signal?: AbortSignal`; durations are milliseconds
(`expirationMs`, `defaultExpirationMs`, remaining TTL from `getTtl`).

- `ICacheBasic` - `get` / `getMany` / `exists` / `getTtl` / `set` /
  `setMany` / `remove` / `removeMany`
- `ICacheAtomic` - `setNx` / `setIfEqual` / `removeIfEqual` / `increment` /
  `consumeToken` / `acquireLock` / `releaseLock`
- `ICacheBroadcast` - `setAndBroadcast` / `setManyAndBroadcast` /
  `removeAndBroadcast` / `removeManyAndBroadcast`
- `ICacheSet` - `setAdd` / `setCardinality` / `setRemove` / `setContains`
  (cluster-only - Redis SADD/SCARD/SREM/SISMEMBER)

All operations return `D2Result` / `D2Result<T>` (async operations wrap it in
`Promise`). Multi-entry maps use `ReadonlyMap<string, T>` (callers may
`new Map(Object.entries(record))`).

Falsey inputs (missing key, missing keys collection, missing entries map)
return `validationFailed` with an `InputError` that identifies the offending parameter
(built via `InputFailures.required(...)`). Implementations never throw for
per-call caller mistakes: every per-call failure is returned on the result.
Construction-time and DI-registration errors are a different lifecycle
concern and do throw - see the `*AndBroadcast*` row of [Result mapping](#result-mapping).

## Cache key convention + PII

Cache keys follow the `EntityName:{id}` shape, for example `Order:{orderId}`,
`Jwks:{kid}` and `WhoIs:{ip}`.
`LocalCacheOptions.keyPrefix` is a namespace prefix added in front of that
convention (useful when multiple caches share a process), not a substitute for
it. Keep PII out of keys: keys appear in logs, traces, and store
inspection. Hash any user-supplied identifier first.

## Marker interfaces

These are the types consumers inject. The marker name states the cache scope at the
dependency site, so a reader of the parameter sees the behavioral profile
without checking registration.

`ILocalCache` - composes `ICacheBasic` + `ICacheAtomic`.
Per-process scope. Atomic operations at process scope. No broadcast (nothing outside
this process can see local cache state). Use for instance-scoped data, such as a
per-instance fingerprint cache, per-instance counters, and hot in-process lookups.

`IDistributedCache` - composes `ICacheBasic` + `ICacheAtomic` +
`ICacheBroadcast` + `ICacheSet`.
Cluster scope. Atomic operations cluster-wide. Every read goes to the remote store
(no L1). Use when freshness matters more than read speed: rate-limit counters,
distributed locks, ephemeral session lookups.

`ITieredCache` - composes `ICacheBasic` + `ICacheAtomic` +
`ICacheBroadcast`.
Composed L1+L2. Reads check L1 first, fall through to L2, and populate L1.
Writes go to L2 first (L1 only if L2 succeeded). Atomic operations route through L2 and
invalidate L1 as a side effect. Use for read-heavy entity data where freshness
within a few seconds is acceptable. It does not compose `ICacheSet`: set
primitives are cluster-only, and tiered composition would obscure their
cluster-wide scope. Callers needing SADD/SCARD inject `IDistributedCache`
directly.

`ICacheBasic` + `ICacheAtomic` + `ICacheBroadcast` are method-identical on
`IDistributedCache` and `ITieredCache`. `ICacheSet` is only on
`IDistributedCache`, so `IDistributedCache` and `ITieredCache` are not fully structurally identical.
The marker distinction is behavioral - DI registration determines the concrete implementation; the
parameter type at the call site tells the reader whether they consume
"single-tier remote" or "two-tier composed."

## Supporting types

`LocalCacheOptions` / `LOCAL_CACHE_DEFAULTS` / `createLocalCacheOptions` -
the options are `maxEntries` (100_000), `defaultExpirationMs` (3_600_000 / 1h) and
`keyPrefix` (`""`). The factory merges an optional partial over the defaults and
returns a fresh mutable object. There is no options validation (mirrors .NET).

`TokenBucketDecision` - the value a `consumeToken` call returns: `admitted`, and
`retryAfterMs` - the wait until the bucket refills to the cost the caller asked
for, zero on an admitted request.

`InputFailures` - pre-built `validationFailed` factory
(`required(paramName)` / `required<T>(paramName)` / `invalid(paramName)`, plus
`tokenBucket(...)` for the argument checks every flavor's token bucket shares)
used by implementations so the cache surface stays errors-as-values rather than throws for
per-call caller mistakes. Constructors and DI registration still throw - they are a registration-time
concern, not per-call input.

`ICacheSerializer` - pluggable serialization for distributed caches.
`contentType: string` (free string, e.g. `"application/json"`);
`serialize` / `deserialize` with `Uint8Array`. This package defines the port only; a default JSON
implementation is part of the `@dcsv-io/d2-caching-distributed-redis` package surface. Local
caches store objects directly and do not need this. Implementations use the
`COULD_NOT_BE_SERIALIZED` / `COULD_NOT_BE_DESERIALIZED` failure codes.

`ICacheInvalidationBackplane` - optional pub/sub backplane for
cross-instance L1 invalidation (`AsyncDisposable`). Tiered consumers
subscribe at construction; `*AndBroadcast*` writes publish on every send.
Provider-agnostic - swappable for Redis pub/sub, Postgres LISTEN/NOTIFY,
in-process for tests.

## Result mapping

| Op family                      | Success / partial                                                                                                                                                                                          | Failure / notes                                                                           |
| ------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| `get`                          | hit -> `ok(value)`                                                                                                                                                                                         | miss -> `notFound`; store down -> `serviceUnavailable`                                    |
| `getMany`                      | all -> `ok(map)`; some -> `someFound(partial)`                                                                                                                                                             | none -> `notFound`                                                                        |
| `exists`                       | `ok(true\|false)`                                                                                                                                                                                          | store down -> fail                                                                        |
| `getTtl`                       | remaining ms -> `ok(number)`; no expiry -> `ok(undefined)`                                                                                                                                                 | absent -> `notFound`                                                                      |
| `set` / `setMany`              | `ok`                                                                                                                                                                                                       | store down -> fail                                                                        |
| `remove` / `removeMany`        | `ok` (**idempotent**)                                                                                                                                                                                      | store down -> fail                                                                        |
| `setNx`                        | wrote -> `ok(true)`; exists -> `ok(false)`                                                                                                                                                                 | store down -> fail                                                                        |
| `setIfEqual`                   | stored value equalled `expected` and was replaced -> `ok(true)`; it differed, or the key was absent -> `ok(false)`, nothing written                                                                        | store down -> fail                                                                        |
| `removeIfEqual`                | stored value equalled `expected` and the key was removed -> `ok(true)`; it differed, or the key was absent -> `ok(false)`, nothing removed                                                                 | store down -> fail                                                                        |
| `increment`                    | `ok(newValue)`; **TTL applied only on key create; subsequent ops preserve TTL**                                                                                                                            | type mismatch -> `conflict`; store down -> fail                                           |
| `consumeToken`                 | bucket has the cost -> `ok(admitted, zero wait)`; short -> `ok(refused, the wait until it has the cost)`. The refill is by elapsed time, so there is no window boundary to spend an allowance twice across | key holds a value that is not bucket state -> `conflict`; store down -> fail              |
| `acquireLock`                  | acquired -> `ok(true)`; held -> `ok(false)`                                                                                                                                                                | **requires** `expirationMs`; store down -> fail                                           |
| `releaseLock`                  | `ok` (**idempotent** - no-op if not held)                                                                                                                                                                  | store down -> fail                                                                        |
| `*AndBroadcast*`               | same as plain counterparts                                                                                                                                                                                 | **throws** if no backplane registered (registration error, not D2Result)                  |
| `setAdd`                       | new -> `ok(true)`; present -> `ok(false)`; **TTL only on set create**                                                                                                                                      | store down -> fail                                                                        |
| `setCardinality`               | `ok(count)`; absent -> `ok(0)`                                                                                                                                                                             | store down -> fail                                                                        |
| `setRemove`                    | removed -> `ok(true)`; absent -> `ok(false)` (**idempotent**)                                                                                                                                              | store down -> fail                                                                        |
| `setContains`                  | `ok(true\|false)`                                                                                                                                                                                          | store down -> fail                                                                        |
| `publishInvalidation` / `Many` | `ok`                                                                                                                                                                                                       | backplane error -> fail                                                                   |
| bad input (falsey key, etc.)   | -                                                                                                                                                                                                          | `validationFailed` via `InputFailures` (implementations return it; the ports document it) |

Counters are `number`, not `bigint`, an intentional TypeScript ergonomic
difference from .NET `long`. Stay within `Number.MAX_SAFE_INTEGER`.

## Broadcast variants - when to use

The `*AndBroadcast*` methods write or remove and also publish an invalidation message.
Other instances' tiered caches subscribe to the backplane and drop their L1
copies on receipt, which keeps the L1 caches of the cluster in sync without polling.

Use the broadcast variant when:

- The data is shared across instances (user profile, org settings, JWKS).
- A user-initiated remove must be visible cluster-wide quickly.
- Coordinated state changes that depend on every instance seeing the new state.

Use the plain (non-broadcast) variant when:

- Cache-warming / startup seed.
- Single-writer-single-reader keys (derived from this instance's process ID).
- Hot-path writes where the broadcast cost dominates (counter ticks).
- Refresh writes of effectively-the-same data.
- Single-instance deployments (don't register a backplane).
- Short-TTL data where staleness is naturally bounded.

## Invalidation backplane

`ICacheInvalidationBackplane` is the transport for `*AndBroadcast*` writes and any other
"tell every instance to drop K from its L1" flow.

### Everyone acts

Every subscriber receives every message, including messages this instance
itself published. There is no sender-ID filter. The cost of self-receive is
bounded (tiered next-read re-fetch from L2, or a no-op remove).

### Dispose unsubscribes / stops further delivery

`subscribe(handler)` returns an `AsyncDisposable`. Disposing a subscription
removes that handler from fan-out and stops further invalidation key
delivery to it. The handler's `AbortSignal` is aborted on dispose
(accompanies unsubscribe; does not replace it). A throw in one handler must not
break delivery to other handlers. Each `subscribe` is independent. Dispose is
idempotent. Disposing the backplane tears down shared provider resources,
unsubscribes remaining handlers, and cancels in-flight handler work.

### At-most-once delivery

Missed message -> next read hits L2. Acceptable for cache invalidation.

```ts
const subscription = backplane.subscribe(async (key, signal) => {
  // Drop L1 entry for `key`. signal aborts when subscription is disposed.
  await dropLocal(key, signal);
});

// Later - dispose the subscription (AsyncDisposable):
const dispose = subscription[Symbol.asyncDispose];
await dispose.call(subscription);
```

## Atomic on tiered - how it works

`ITieredCache` exposes the same atomic surface as `IDistributedCache` because
the atomicity guarantee comes from L2 (the cluster source of truth). Pattern:

- **`increment`** -> L2 atomic increment; on success invalidate L1 + broadcast.
- **`setNx`** -> L2 SetNx; on success write L1 + broadcast; on fail invalidate L1.
- `setIfEqual` -> L2 compare-and-set. Invalidate L1 on every outcome, unlike
  `setNx`: the comparison is decided at L2, and a backplane invalidation arriving
  between the L2 write and an L1 populate would be applied first, leaving this
  node with the value the shared store has already superseded.
- `removeIfEqual` -> L2 compare-and-delete. Invalidate L1 on every outcome, in
  the order `setIfEqual` uses for its write and for the same reason.
- `consumeToken` -> pure delegation to L2. The bucket state belongs to the
  shared store, and a local copy would apply to one node, not the cluster.
- `acquireLock` / `releaseLock` -> pure delegation to L2 (lock state is
  coordination, not a cached value).

L1 is never authoritative for atomic state; L2 is. L1 only mirrors L2 or
invalidates its entry. The behavior is implemented in `@dcsv-io/d2-caching-tiered`.

## Configuration carve-out

This package defines no shared `DistributedCacheOptions`. Provider-
specific options (Redis connection string, Sentinel topology, channel name
for pub/sub, retry config, etc.) belong to the implementation package's own
options class. The few common fields (`defaultExpirationMs`, `keyPrefix`) are
easier to redeclare per implementation than to inherit. The same holds for tiered: the tiered
package declares its own options when it has a setting to expose.

The invalidation channel constant (`d2:cache:invalidations`) is part of the
Redis package options. This package defines the interface only.

## Usage

Inject markers at composition roots; this package registers nothing.

```ts
import type { ILocalCache, IDistributedCache, ITieredCache } from "@dcsv-io/d2-caching-abstractions";
import { InputFailures, createLocalCacheOptions } from "@dcsv-io/d2-caching-abstractions";

// Domain / handler code depends only on the marker:
async function loadProfile(cache: ITieredCache, userId: string) {
  return cache.get<Profile>(`Profile:${userId}`);
}

// Impls use InputFailures for per-call validation:
function guardKey(key: string) {
  if (!key) return InputFailures.required("key");
  return undefined;
}

const localOpts = createLocalCacheOptions({ keyPrefix: "jwks:" });
```

## Telemetry

This package has no telemetry; the abstractions contain no hooks. Metrics are emitted by the
local-default and redis implementations; the tiered package emits structured logs and no meters.

## Dependencies

- `@dcsv-io/d2-result` - every op returns `D2Result` / `D2Result<T>`
- `@dcsv-io/d2-i18n-keys` - `TK.common.errors.NOT_NULL_VIOLATION` for `InputFailures`

The package has no other runtime dependencies (no DI, no logging, no provider libraries), so
any handler can declare a cache dependency without pulling in an implementation.

## Sister packages

- `@dcsv-io/d2-caching-local-default` - local in-process impl
- `@dcsv-io/d2-caching-distributed-redis` - Redis + backplane
- `@dcsv-io/d2-caching-tiered` - L1+L2 composition
- .NET equivalent: `DcsvIo.D2.Caching.Abstractions`
