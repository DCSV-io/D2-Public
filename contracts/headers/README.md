<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/headers/`

HTTP, AMQP, and gRPC header registry — the closed set of D²-specific headers (`x-d2-trace-id`, `x-d2-correlation-id`, `x-d2-org-id`, etc.) used across all transport layers.

## Consumed by

- **.NET** — [`public/packages/dotnet/headers/source-gen/`](../../packages/dotnet/headers/source-gen/README.md) (Roslyn source-gen → per-transport header-name constants in `DcsvIo.D2.Headers.Common` / `.Http` / `.Grpc` / `.Amqp`)
- **TypeScript** — monorepo-private `private/tools/ts-codegen` › `headers-emit.ts` (not on public export) (→ matching per-transport header-name constants in the public `@dcsv-io/d2-headers-{common,http,amqp,grpc}` catalogs (composition route-guards live in private monorepo `@dcsv-io/d2-private-headers`))

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
