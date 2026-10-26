<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-headers-http

> **Duplicated from `contracts/headers/headers.spec.json` - update both in lockstep.** This catalog mirrors its .NET
> sibling `DcsvIo.D2.Headers.Http` at byte-equal wire values. Both sides emit from the same spec; physical dedup
> across TS <-> .NET is not feasible. Parity is asserted by a TypeScript contract test and by
> `HeaderCatalogConsistencyTests` (.NET).

D2 wire-protocol headers applicable to the HTTP transport. Includes the HTTP-specific entries (`Authorization`,
`Idempotency-Key`, `X-D2-Client-Fingerprint`, `X-D2-Internal-Token`) and the cross-transport entries that are also
sent with HTTP requests (`x-d2-context`, `traceparent`, `tracestate`) at identical wire values per
`headers.spec.json`. Mirrors .NET `DcsvIo.D2.Headers.Http.HttpHeaders`.

## Install

```bash
pnpm add @dcsv-io/d2-headers-http
```

## Public API

| Export             | Source              | Mirror                                  |
| ------------------ | ------------------- | --------------------------------------- |
| `HttpHeaders`      | `http-headers.g.ts` | `DcsvIo.D2.Headers.Http.HttpHeaders`    |
| `HttpHeaderName`   | `http-headers.g.ts` | n/a (TS-only union type)                |
| `ALL_HTTP_HEADERS` | `http-headers.g.ts` | `DcsvIo.D2.Headers.Http.AllHttpHeaders` |

## Codegen workflow

`prebuild` regenerates the catalog from `contracts/headers/headers.spec.json` when a generator is available. Generated files (`*.g.ts`) are committed to git.

## When to reach for this catalog

Use `@dcsv-io/d2-headers-http` from any HTTP-context consumer - SvelteKit hooks, fetch wrappers, route guards, BFF
outbound calls. The catalog includes both the HTTP-only entries (e.g. `IDEMPOTENCY_KEY`) and the cross-transport
entries (e.g. `TRACEPARENT`) that an HTTP pipeline can encounter, so one `import` covers everything that
pipeline can encounter.

## Spec contract

`contracts/headers/headers.spec.json` is the single source of truth. Every entry whose `applicability` array contains
`"http"` lives in this catalog (cross-transport entries also live in `@dcsv-io/d2-headers-common` and every other
transport catalog they apply to, all at identical wire values; guaranteed by codegen and verified by
`HeaderCatalogConsistencyTests` on the .NET side).

## Dependencies

None at runtime - pure constants. DevDeps: `vitest` + `@vitest/coverage-v8` + `typescript`.

## Reference

- `contracts/headers/headers.spec.json` - source spec
- `@dcsv-io/d2-headers-common` - cross-transport subset
- `@dcsv-io/d2-headers-amqp` - AMQP-applicable subset
- `@dcsv-io/d2-headers-grpc` - gRPC-applicable subset
