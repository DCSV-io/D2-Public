<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-caching-tiered

.NET mirror: `DcsvIo.D2.Caching.Tiered`

Node services and backend-for-frontend (BFF) code that need a composed L1+L2 `ITieredCache` inject this
pure-composition implementation. Reads check the in-process L1 first and fall through to the distributed L2. Writes go
L2-first so partial-write states are impossible. Atomics route through L2 with L1 side-effects. An optional
invalidation backplane keeps every instance's L1 coherent under the everyone-acts rule. Every operation returns
`@dcsv-io/d2-result` results.

## Install

```bash
pnpm add @dcsv-io/d2-caching-tiered
```

## Usage

```ts
import { DefaultLocalCache } from "@dcsv-io/d2-caching-local-default";
import {
  connectRedis,
  createRedisCacheOptions,
  JsonCacheSerializer,
  RedisCacheInvalidationBackplane,
  RedisDistributedCache,
} from "@dcsv-io/d2-caching-distributed-redis";
import { DefaultTieredCache } from "@dcsv-io/d2-caching-tiered";
import type { ILogger } from "@dcsv-io/d2-logging";

const logger = /* host ILogger */ undefined as unknown as ILogger;
const l1 = new DefaultLocalCache({ keyPrefix: "bff:" });
const options = createRedisCacheOptions({
  connectionString: process.env.REDIS_URL, // SECRET when credentials embedded - never log
  keyPrefix: "app:",
});
const redis = connectRedis(options);
const serializer = new JsonCacheSerializer();
const backplane = new RedisCacheInvalidationBackplane(redis, options, logger);
await backplane.ready; // channel SUBSCRIBE established (owned by redis package)
const l2 = new RedisDistributedCache({
  redis,
  options,
  serializer,
  logger,
  // L2 backplane optional; tiered holds its own for *AndBroadcast*
});
const cache = new DefaultTieredCache({ l1, l2, logger, backplane });

await cache.set("user:1", { displayName: "Ada" });
const hit = await cache.get<{ displayName: string }>("user:1");
await cache.setAndBroadcast("user:1", { displayName: "Ada Lovelace" });

// unsubscribes only; host still owns l1/l2/backplane/redis
const dispose = cache[Symbol.asyncDispose];
await dispose.call(cache);
```

## Construction

```ts
new DefaultTieredCache({
  l1: ILocalCache,
  l2: IDistributedCache,
  logger: ILogger,
  backplane?: ICacheInvalidationBackplane,
});
```

`l1`, `l2`, and `logger` are required. The optional `backplane` enables `*AndBroadcast*` and cluster-wide L1
invalidation subscribe. Construction is synchronous. When the backplane is a Redis implementation, await that
backplane's `ready` Promise before work that depends on delivery - this package does not establish the channel
subscription.

The tiered cache does not dispose `l1`, `l2`, or `backplane`; the host creates and disposes them. Disposing the tiered
cache only unsubscribes its backplane handler. Construct once at the composition root and share the instance.

## Public surface

- `DefaultTieredCache` - implements `ITieredCache` (`ICacheBasic` + `ICacheAtomic` + `ICacheBroadcast`) and
  `AsyncDisposable`.
- `DefaultTieredCacheDeps` - constructor dependency bag.
- `BACKPLANE_NOT_REGISTERED_MESSAGE` - pinned registration-error text thrown by `*AndBroadcast*` when no backplane was
  passed.
- `TieredCacheOp`, `TIERED_ERROR_CODE_UNKNOWN`, `TieredCacheOpName` - public closed set of operation names plus an
  errorCode sentinel, for dual-runtime ContractFixtures parity.

Basic: `get`, `getMany`, `exists`, `getTtl`, `set`, `setMany`, `remove`, `removeMany`.

Atomic: `setNx`, `setIfEqual`, `removeIfEqual`, `increment`, `consumeToken`, `acquireLock`, `releaseLock`.

Broadcast: `setAndBroadcast`, `setManyAndBroadcast`, `removeAndBroadcast`, `removeManyAndBroadcast`.

This package does not implement `ICacheSet` (SADD/SCARD). Callers that need set primitives inject `IDistributedCache`
directly.

## Behavior

Reads (`get` / `getMany` / `exists`):

- Try L1 first.
- On L1 miss, fall through to L2.
- On L2 hit, populate L1 with the value (L1 default TTL; L2 remains the cluster freshness authority).
- Return the result.
- `exists`: L1 `true` short-circuits; otherwise query L2.
- `getTtl`: L2 only (cluster source of truth).

Writes (`set` / `setMany` / `remove` / `removeMany`):

- L2 first - if L2 fails, return that failure and do not touch L1.
- L1 second - only after L2 succeeded. The same `value` / `entries` / `expirationMs` / `signal` passed to L2 are passed to L1 on success.
- If L1 fails after L2 succeeded, the L2-success result is returned. The L1 failure is logged at Warning and swallowed. L1 is the optional layer: an L1 failure on this instance must not fail a write the cluster accepted. The next read on this instance re-fetches from L2.

Atomic primitives (`setNx` / `setIfEqual` / `removeIfEqual` / `increment` / `consumeToken` / `acquireLock` /
`releaseLock`):

- Route through L2 (cluster source of truth) with full port arity (`value` / `amount` / `expirationMs` / `signal` as applicable).
- After `setNx` succeeds: if L2 stored the value, populate L1 with the same `value` and `expirationMs`; if the key
  already existed, drop L1.
- After `setIfEqual`: drop L1 on every outcome, unlike `setNx`. The comparison is decided at L2, and a backplane
  invalidation arriving between the L2 write and an L1 populate would be applied first, leaving this node with the
  value the shared store has already superseded.
- After `removeIfEqual`: drop L1 on every outcome, in the same order and for the same reason.
- After `increment` succeeds: always drop L1 (counters held in L1 would diverge).
- `consumeToken`: pure L2 delegation - the bucket state belongs to the shared store, and a local copy would apply to
  one node rather than to the cluster.
- Locks: pure L2 delegation - L1 is not involved. `releaseLock` is idempotent on ownership miss; a store-down failure surfaces as `serviceUnavailable` from L2.

Broadcast (`setAndBroadcast` and siblings):

- Perform the tiered write first; if the write fails, return that result and do not publish (every operation).
- On write success, publish via the injected `ICacheInvalidationBackplane`. Publish failure returns the backplane `D2Result` as-is.
- All subscribers (every connected instance, including this one) receive the key and drop their L1 entry - everyone acts.
- Missing backplane throws a plain `Error` with `BACKPLANE_NOT_REGISTERED_MESSAGE` (registration error, not a `D2Result`).

## Backplane subscription

When a backplane is passed at construction, the tiered cache subscribes in the constructor. Every received invalidation key triggers `remove` on L1. If that L1 remove fails, the failure is logged at Warning and processing continues; L1 stays stale on this instance until TTL or the next write/broadcast for that key. Disposing the tiered cache disposes the subscription only.

Without a backplane, plain ops still work; L1 caches drift across instances until their TTLs expire. `*AndBroadcast*` throws.

## Design rationale: no ICacheSet

`ITieredCache` deliberately does not implement `ICacheSet`. Set cardinality is cluster-only - it cannot be composed
correctly across L1+L2 (an L1 set would only see this instance's adds; cluster cardinality is available only from L2).
Callers needing set primitives inject `IDistributedCache` directly.

## Logging

Structured Warning logs only (see also [Telemetry](#telemetry)):

| When                                            | Message                                          | Bindings                               |
| ----------------------------------------------- | ------------------------------------------------ | -------------------------------------- |
| L1 remove fails inside the invalidation handler | `Tiered cache L1 invalidation handler failed.`   | `key`, `errorCode`                     |
| L1 write/remove fails after L2 succeeded        | `Tiered cache L1 write failed after L2 success.` | `operation`, `keyOrCount`, `errorCode` |

`operation` is one of `set` / `setMany` / `remove` / `removeMany`. `errorCode` comes from the L1 `D2Result` (`"unknown"` when absent). Bindings never include exception messages. Keep PII out of cache keys (see abstractions).

## Telemetry

This package publishes no OpenTelemetry meters. The L1 and L2 packages define their own meters. Observability for this
package is structured logging only - see [Logging](#logging).

## Result mapping

Per-call validation and store failures surface from L1/L2 (`notFound`, `someFound`, `validationFailed` via `InputFailures`, `serviceUnavailable`, `conflict` on increment type mismatch). Tiered composition adds:

| Path                                                                     | Outcome                                                           |
| ------------------------------------------------------------------------ | ----------------------------------------------------------------- |
| L2 write fails                                                           | Return L2 result; L1 untouched                                    |
| L1 fails after L2 write ok                                               | Return L2 success; Warning log                                    |
| L2 hard fail on `getMany` missing keys (`!success && !isPartialSuccess`) | Propagate L2 even if L1 had partial hits                          |
| L2 `someFound` on missing keys                                           | Merge with L1 hits; `ok(merged)` or `someFound({ data: merged })` |
| L2 `notFound` on missing keys                                            | `someFound({ data: l1Hits })` or `notFound()`                     |
| `*AndBroadcast*` write fails                                             | Return write result; no publish                                   |
| `*AndBroadcast*` publish fails                                           | Return backplane result                                           |
| `*AndBroadcast*` without backplane                                       | Throws `Error` (`BACKPLANE_NOT_REGISTERED_MESSAGE`)               |

## Disposal

`[Symbol.asyncDispose]` unsubscribes the backplane handler and is idempotent. It does not dispose L1, L2, or the backplane. Live operations remain callable after dispose (subscription already torn down).

## Divergences from the .NET implementation

- Construction uses a deps object instead of MS.DI `AddD2TieredCache()`; hosts compose L1/L2/logger/backplane explicitly.
- Durations on L1/L2 use millisecond numbers (`expirationMs`), not `TimeSpan`.
- Registration errors throw a plain `Error` with a pinned message, not `InvalidOperationException`.
- When the backplane is Redis-backed, `@dcsv-io/d2-caching-distributed-redis` signals channel-subscribe readiness
  through its `ready` Promise; this package keeps a sync constructor and a sync port `subscribe`.
- Counter / lock numeric width follows the TypeScript port (`number` within `Number.MAX_SAFE_INTEGER`).
- Logging uses short structured redis-style Warning message strings and camelCase binding fields. Dual-runtime parity
  covers EventId meanings (L1 invalidation fail / L1 write fail after L2 ok), Warning-only severity, and `errorCode`
  presence. It does not cover LoggerMessage template byte-equality or the .NET `"SetAsync"` / `"SetManyAsync"`
  operation-name strings (TypeScript uses `"set"` / `"setMany"` / `"remove"` / `"removeMany"`).
- Runtime dependencies are abstractions + result + logging only (no `@dcsv-io/d2-utilities`; .NET tiered likewise has no Utilities package dependency).

## Dependencies

| Package                            | Role                                                                                      |
| ---------------------------------- | ----------------------------------------------------------------------------------------- |
| `@dcsv-io/d2-caching-abstractions` | Ports (`ITieredCache`, `ILocalCache`, `IDistributedCache`, `ICacheInvalidationBackplane`) |
| `@dcsv-io/d2-result`               | Result factories                                                                          |
| `@dcsv-io/d2-logging`              | `ILogger`                                                                                 |

No backing-store packages at runtime - this package is pure composition. No `@dcsv-io/d2-utilities` (validation/falsey live in L1/L2). Typical hosts also depend on `@dcsv-io/d2-caching-local-default` and `@dcsv-io/d2-caching-distributed-redis` for L1/L2/backplane implementations.

## Sister packages

- `@dcsv-io/d2-caching-abstractions` - ports + result mapping
- `@dcsv-io/d2-caching-local-default` - typical L1
- `@dcsv-io/d2-caching-distributed-redis` - typical L2 + backplane
- .NET equivalent: `DcsvIo.D2.Caching.Tiered`
