<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# result/

> Parent: [`packages/dotnet/`](../README.md)

The `D2Result` errors-as-values core that every handler, repo, and service returns - plus the spec-driven source
generator that emits its JSON wire envelope. The gRPC trailer envelope is spec-driven as well: a host-supplied
source generator generates the trailer constants from
[`contracts/grpc-trailers/`](../../../contracts/grpc-trailers/README.md), and this folder contains no generator for
them. The result core has semantic factories, the partial-success ladder, `BubbleFail` propagation, and an
auto-injected `traceId`; user-facing messages are typed as translation keys, so the compiler requires every message to
be translatable. The envelope and trailer catalogs are spec-driven, so the JSON and gRPC wire shapes match the TS-side
`@dcsv-io/d2-result` catalog byte for byte.

## Packages

| Package                                                 | Description                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| ------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [`core/`](core/README.md)                               | `D2Result<T>` - semantic factories, partial-success ladder, `BubbleFail` propagation, auto-injected `traceId`, translation-key-typed messages.                                                                                                                                                                                                                                                                                                                    |
| [`grpc/`](grpc/README.md)                               | Codec that round-trips a `D2Result` through the `D2ResultProto` gRPC response envelope without losing a field. `ToProto()` (server side, wraps the result) / `ToD2Result<T>()` (client side, rebuilds the result) / `HandleAsync<T>()` (call wrapper that fails open on a transport fault) / `IsTransientGrpcException()`. Business failures travel as normal gRPC `OK` responses; `RpcException` is used only for host-supplied auth and transport-layer faults. |
| [`envelope-source-gen/`](envelope-source-gen/README.md) | Roslyn generator emitting the `D2Result` JSON wire-envelope field-name constants into `core/` from `contracts/d2result-envelope/d2result-envelope.spec.json`.                                                                                                                                                                                                                                                                                                     |
