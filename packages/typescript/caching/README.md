<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# caching/ - TypeScript caching stack (twin of `DcsvIo.D2.Caching.*`)

> Parent: [`packages/typescript/`](../README.md)

The behavior is specified in [ADR-0008](../../../docs/adrs/0008-caching-marker-interfaces.md). The .NET implementation
is canonical: [`packages/dotnet/caching/`](../../dotnet/caching/README.md).

## Packages (layout mirror)

| Folder                                                       | Package                                 | Status    | Purpose                                                                                                                                                                       | .NET mirror                           |
| ------------------------------------------------------------ | --------------------------------------- | --------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------- |
| [`abstractions/README.md`](abstractions/README.md)           | `@dcsv-io/d2-caching-abstractions`      | **Built** | Marker + building-block cache ports (`ILocalCache` / `IDistributedCache` / `ITieredCache` + Basic/Atomic/Broadcast/Set + backplane + serializer + `InputFailures` / options). | `DcsvIo.D2.Caching.Abstractions`      |
| [`local-default/README.md`](local-default/README.md)         | `@dcsv-io/d2-caching-local-default`     | **Built** | In-process L1 + atomics (no broadcast).                                                                                                                                       | `DcsvIo.D2.Caching.Local.Default`     |
| [`distributed-redis/README.md`](distributed-redis/README.md) | `@dcsv-io/d2-caching-distributed-redis` | **Built** | Redis distributed cache + invalidation backplane (Basic/Atomic/Broadcast/Set; default channel `d2:cache:invalidations`).                                                      | `DcsvIo.D2.Caching.Distributed.Redis` |
| [`tiered/README.md`](tiered/README.md)                       | `@dcsv-io/d2-caching-tiered`            | **Built** | L1+L2 composition - L2-first writes, `*AndBroadcast*`, everyone-acts L1 drop.                                                                                                 | `DcsvIo.D2.Caching.Tiered`            |

## Locked cross-runtime rules

- Full surface: Basic, Atomic, Broadcast, Set, tiered and backplane, with no subset.
- The default invalidation channel is `d2:cache:invalidations`, the same as `RedisCacheOptions.InvalidationChannel` on
  .NET.
- Everyone acts: the backplane has no sender-ID filter.
- Delivery is at most once. After a missed message, the next read goes to L2.
- Tiered writes go to L2 first. Atomic operations run on L2 and then drop L1.
- Every operation returns an `@dcsv-io/d2-result` shape that matches the .NET `D2Result` mapping.

## Parity checks

- Each package has its own unit suites and Redis Testcontainers integration tests, which pin the algorithms and the
  behavior.
- Shared constants and semantics: package tests pin the shared defaults, channel and meter names against the .NET twin.
  There is no full behavior interop harness.
