<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# result/

> Parent: [`public/packages/dotnet/`](../README.md)

The `D2Result` errors-as-values core that every handler, repo, and service returns from — plus the spec-driven source generators that emit its JSON wire envelope and its gRPC trailer envelope (trailer constants generated under private Auth.Grpc SG). The result core carries semantic factories, the partial-success ladder, `BubbleFail` propagation, and an auto-injected `traceId`; user-facing messages are typed as translation keys so every message is compile-time enforced to be translatable. The envelope and trailer catalogs are spec-driven so the JSON / gRPC wire shapes match the TS-side `@dcsv-io/d2-result` catalog byte-for-byte.

## Packages

| Package                                                       | Description                                                                                                                          |
| ------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| [`core/`](core/README.md)                                     | `D2Result<T>` — semantic factories, partial-success ladder, `BubbleFail` propagation, auto-injected `traceId`, translation-key-typed messages. |
| [`grpc/`](grpc/README.md)                                     | Faithful `D2Result` ↔ `D2ResultProto` gRPC response-envelope codec. `ToProto()` (server WRAP) / `ToD2Result<T>()` (client RE-MATERIALIZE) / `HandleAsync<T>()` (call wrapper with transport-fault fail-open) / `IsTransientGrpcException()`. Business failures travel as normal gRPC `OK` responses; `RpcException` is reserved for monorepo-private auth/transport-layer faults (PackageId `DcsvIo.D2.Private.Auth.Grpc`; AssemblyName policy A). |
| [`envelope-source-gen/`](envelope-source-gen/README.md)       | Roslyn generator emitting the `D2Result` JSON wire-envelope field-name constants into `core/` from `contracts/d2result-envelope/d2result-envelope.spec.json`. |
