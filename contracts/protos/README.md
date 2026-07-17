<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/protos/`

Hand-authored Protocol Buffer contracts — the `.proto` source files that define gRPC service interfaces and shared message types used across .NET services and the TypeScript gRPC client.

## Layout

```
contracts/protos/
└── common/
    └── v1/
        ├── d2_result.proto   — D2Result gRPC wire shape (status, errorCode, messages, traceId)
        ├── health.proto      — standard health-check service
        └── ping.proto        — liveness ping
```

## Consumed by

- **.NET** — service / library projects compile these via `Grpc.Tools` (MSBuild) in each consuming project that references them (including packages under `packages/dotnet/*`); C# stubs are generated at build time
- **TypeScript** — `@dcsv-io/d2-protos` (Buf + `ts-proto` generated types and gRPC stubs from these protos; sources committed)
- **TypeSpec** — internal IDL tooling; not required for published packages

These are hand-authored `.proto` files (not generated from a spec), so there is no source-gen project of their own — the consumers above generate FROM them.

## See also

- All contracts: [contracts catalog](../README.md)
