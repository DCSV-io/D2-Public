<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/grpc-trailers/`

gRPC trailer key catalog — the metadata trailer names written by the .NET gRPC handler (`d2_error_code`, `d2_messages`, `traceId`) and read by the TypeScript gRPC client to reconstruct a `D2Result` from a gRPC response.

## Consumed by

- **.NET** — monorepo-private composition under `private/packages/dotnet/auth/grpc-trailers-source-gen/` (not on public export; Roslyn source-gen → `D2GrpcTrailers` constants into PackageId `DcsvIo.D2.Private.Auth.Grpc` / AssemblyName policy A)
- **TypeScript** — monorepo-private `private/tools/ts-codegen` › `grpc-trailers-emit.ts` (not on public export) (→ matching trailer-key constants in `@dcsv-io/d2-private-grpc-client`)

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
