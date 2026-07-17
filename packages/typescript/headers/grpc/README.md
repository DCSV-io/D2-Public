<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-headers-grpc

> **Duplicated from `contracts/headers/headers.spec.json` — update both in lockstep.** This catalog mirrors its .NET sibling `DcsvIo.D2.Headers.Grpc` at byte-equal wire values. Both sides emit from the same spec; physical dedup across TS ↔ .NET is not feasible. Parity is asserted by `contract-tests/headers.parity.test.ts` (TS) and `HeaderCatalogConsistencyTests` (.NET).

D2 wire-protocol headers applicable to the gRPC transport. Today the catalog holds the gRPC-applicable subset of cross-transport entries (`Authorization`, `x-d2-context`, `traceparent`, `tracestate`) at identical wire values per `headers.spec.json`. Mirrors .NET `DcsvIo.D2.Headers.Grpc.GrpcHeaders`.

## Install

```bash
pnpm add @dcsv-io/d2-headers-grpc
```

## Public API

| Export             | Source              | Mirror                                  |
| ------------------ | ------------------- | --------------------------------------- |
| `GrpcHeaders`      | `grpc-headers.g.ts` | `DcsvIo.D2.Headers.Grpc.GrpcHeaders`    |
| `GrpcHeaderName`   | `grpc-headers.g.ts` | n/a (TS-only union type)                |
| `ALL_GRPC_HEADERS` | `grpc-headers.g.ts` | `DcsvIo.D2.Headers.Grpc.AllGrpcHeaders` |

## Codegen workflow

`prebuild` regenerates the catalog from `contracts/headers/headers.spec.json` when a generator is available. Generated files (`*.g.ts`) are committed to git.

## When to reach for this catalog

Use `@dcsv-io/d2-headers-grpc` from any gRPC-context consumer — gRPC interceptors, gRPC client wrappers. The catalog includes cross-transport entries (e.g. `TRACEPARENT`) at identical wire values to the other catalogs (codegen-guaranteed and verified by `HeaderCatalogConsistencyTests` on the .NET side).

## Notes on gRPC framework constants

gRPC framework constants like `grpc-encoding`, `grpc-status`, `grpc-message`, `grpc-timeout` come from `Grpc.Core.Metadata` (.NET) or the corresponding gRPC framework symbol on the TS side and are NOT part of `headers.spec.json`. This catalog covers only D2-defined headers and the cross-transport ones that flow through gRPC alongside framework headers.

## Spec contract

`contracts/headers/headers.spec.json` is the single source of truth. Every entry whose `applicability` array contains `"grpc"` lives in this catalog.

## Dependencies

None at runtime — pure constants. DevDeps: `vitest` + `@vitest/coverage-v8` + `typescript`.

## Reference

- `contracts/headers/headers.spec.json` — source spec
- `@dcsv-io/d2-headers-common` — cross-transport subset
- `@dcsv-io/d2-headers-http` — HTTP-applicable subset
- `@dcsv-io/d2-headers-amqp` — AMQP-applicable subset
