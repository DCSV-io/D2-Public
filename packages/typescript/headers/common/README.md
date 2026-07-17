<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-headers-common

> **Duplicated from `contracts/headers/headers.spec.json` — update both in lockstep.** This catalog mirrors its .NET sibling `DcsvIo.D2.Headers.Common` at byte-equal wire values. Both sides emit from the same spec; physical dedup across TS ↔ .NET is not feasible. Parity is asserted by `contract-tests/headers.parity.test.ts` (TS) and `HeaderCatalogConsistencyTests` (.NET).

Cross-transport D2 wire-protocol headers — entries with applicability count >= 2 (i.e. headers that appear identically on multiple transports). Mirrors .NET `DcsvIo.D2.Headers.Common.CommonHeaders`.

## Install

```bash
pnpm add @dcsv-io/d2-headers-common
```

## Public API

| Export               | Source                | Mirror                                      |
| -------------------- | --------------------- | ------------------------------------------- |
| `CommonHeaders`      | `common-headers.g.ts` | `DcsvIo.D2.Headers.Common.CommonHeaders`    |
| `CommonHeaderName`   | `common-headers.g.ts` | n/a (TS-only union type)                    |
| `ALL_COMMON_HEADERS` | `common-headers.g.ts` | `DcsvIo.D2.Headers.Common.AllCommonHeaders` |

## Codegen workflow

`prebuild` regenerates the catalog from `contracts/headers/headers.spec.json` when a generator is available. Generated files (`*.g.ts`) are committed to git.

## When to reach for this catalog

Use `@dcsv-io/d2-headers-common` when the consumer is transport-agnostic — e.g. a tracing utility that handles `traceparent` / `tracestate` regardless of whether the request arrived over HTTP, gRPC, or AMQP. Transport-specific consumers should reach for `@dcsv-io/d2-headers-http`, `@dcsv-io/d2-headers-amqp`, or `@dcsv-io/d2-headers-grpc` instead — those catalogs include the cross-transport entries inline at identical wire values, so a single `import` covers everything that transport's pipeline can encounter.

## Spec contract

`contracts/headers/headers.spec.json` is the single source of truth. Cross-transport entries appear in `CommonHeaders` AND every per-transport catalog whose `applicability` array contains the relevant transport, all at identical wire values (codegen-guaranteed; verified by `HeaderCatalogConsistencyTests` on the .NET side).

## Dependencies

None at runtime — pure constants. DevDeps: `vitest` + `@vitest/coverage-v8` + `typescript`.

## Reference

- `contracts/headers/headers.spec.json` — source spec
- `@dcsv-io/d2-headers-http` — HTTP-applicable subset
- `@dcsv-io/d2-headers-amqp` — AMQP-applicable subset
- `@dcsv-io/d2-headers-grpc` — gRPC-applicable subset
