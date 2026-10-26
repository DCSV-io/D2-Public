<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Headers.Grpc

D2 wire-protocol headers for the gRPC transport. The catalog contains the gRPC-applicable subset of cross-transport
entries (`Authorization`, `x-d2-context`, `traceparent`, `tracestate`) at identical wire values. The
`DcsvIo.D2.Headers.SourceGen` source generator emits it from the headers contract spec, filtered with
`applicability.Contains("grpc")`. It has the same wire values as the TypeScript package `@dcsv-io/d2-headers-grpc`, byte
for byte, and shared contract tests assert that parity.

## Install

```bash
dotnet add package DcsvIo.D2.Headers.Grpc
```

## Public API

| Member                           | Type                           | Purpose                                                         |
| -------------------------------- | ------------------------------ | --------------------------------------------------------------- |
| `GrpcHeaders.AUTHORIZATION`      | `const string "Authorization"` | RFC 6750 bearer token header                                    |
| `GrpcHeaders.PROPAGATED_CONTEXT` | `const string "x-d2-context"`  | Base64url-of-JSON propagated context envelope (cross-transport) |
| `GrpcHeaders.TRACEPARENT`        | `const string "traceparent"`   | W3C Trace Context (cross-transport)                             |
| `GrpcHeaders.TRACESTATE`         | `const string "tracestate"`    | W3C tracestate (cross-transport)                                |
| `GrpcHeaders.AllGrpcHeaders`     | `IReadOnlyList<string>`        | All wire values in `constName` order                            |

## Notes on gRPC framework constants

gRPC framework constants like `grpc-encoding`, `grpc-status`, `grpc-message`, `grpc-timeout` come from
`Grpc.Core.Metadata` and are not part of the headers contract spec. This catalog covers only D2-defined headers and the
cross-transport ones that flow through gRPC alongside framework headers.

## When to use this catalog

Use `DcsvIo.D2.Headers.Grpc` from any gRPC-context consumer: gRPC interceptors and host-supplied outbound client
wrappers. On a cross-process hop the gRPC client typically forwards an internal transaction token, minted once and
left unchanged, in the `Authorization` header over mTLS, which establishes workload identity. The catalog values are
identical to the corresponding entries in `DcsvIo.D2.Headers.Common` / `DcsvIo.D2.Headers.Http` (codegen-guaranteed and
verified by header catalog consistency tests).

## Spec contract

The headers contract spec is the single source of truth. Every entry whose `applicability` array contains `"grpc"` is
in this catalog.

## Dependencies

- `DcsvIo.D2.Headers.SourceGen` (build-time analyzer)

No runtime dependencies - pure constants.

Related packages: `DcsvIo.D2.Headers.Common`, `DcsvIo.D2.Headers.Http`, `DcsvIo.D2.Headers.Amqp`.
