<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Headers.Common

Cross-transport D2 wire-protocol headers — entries with applicability count ≥ 2 (headers that appear identically on multiple transports). Codegen-emitted from the headers contract spec via `DcsvIo.D2.Headers.SourceGen` (filtered with `applicability.Length >= 2`). Mirrors the TypeScript package `@dcsv-io/d2-headers-common` at byte-equal wire values; parity is asserted by shared contract tests.

## Install

```bash
dotnet add package DcsvIo.D2.Headers.Common
```

## Public API

| Member                             | Type                          | Purpose                                                                        |
| ---------------------------------- | ----------------------------- | ------------------------------------------------------------------------------ |
| `CommonHeaders.PROPAGATED_CONTEXT` | `const string "x-d2-context"` | Base64url-of-JSON propagated context envelope (HTTP + gRPC + AMQP)             |
| `CommonHeaders.TRACEPARENT`        | `const string "traceparent"`  | W3C Trace Context (HTTP + gRPC + AMQP)                                         |
| `CommonHeaders.TRACESTATE`         | `const string "tracestate"`   | W3C tracestate (HTTP + gRPC + AMQP)                                            |
| `CommonHeaders.AllCommonHeaders`   | `IReadOnlyList<string>`       | All wire values in `constName` order — useful for cross-spec consistency tests |

(Catalog is codegen-emitted; the table above lists today's three cross-transport entries. New cross-transport entries appear here automatically when added to the spec.)

## When to reach for this catalog

Use `DcsvIo.D2.Headers.Common` when the consumer is transport-agnostic — e.g. a tracing utility that handles `traceparent` / `tracestate` regardless of whether the request arrived over HTTP, gRPC, or AMQP. Transport-specific consumers should reach for `DcsvIo.D2.Headers.Http`, `DcsvIo.D2.Headers.Amqp`, or `DcsvIo.D2.Headers.Grpc` instead — those catalogs include the cross-transport entries inline at identical wire values, so a single `using` covers everything that transport's pipeline can encounter.

## Spec contract

The headers contract spec is the single source of truth. Cross-transport entries appear in `CommonHeaders` and every per-transport catalog whose `applicability` array contains the relevant transport, all at identical wire values (codegen-guaranteed and verified by header catalog consistency tests).

## Dependencies

- `DcsvIo.D2.Headers.SourceGen` (build-time analyzer; `OutputItemType="Analyzer"` + `ReferenceOutputAssembly="false"`)

No runtime dependencies — pure constants.

Sister packages: `DcsvIo.D2.Headers.Http`, `DcsvIo.D2.Headers.Amqp`, `DcsvIo.D2.Headers.Grpc`.
