<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/auth-context/`

`IAuthContext` interface spec — the identity and authorization context fields derived from the bearer JWT and surfaced to every handler and middleware across all transports.

## Consumed by

- **.NET** — [`packages/dotnet/context/source-gen/`](../../packages/dotnet/context/source-gen/README.md) (Roslyn source-gen → `PropagatedContext` + serializer in `DcsvIo.D2.Context.Abstractions`; the same generator also emits the request-context layer)
- **TypeScript** — constants/types in `@dcsv-io/d2-auth-context-abstractions` (generated from this spec; sources committed)

## See also

- All contracts: [contracts catalog](../README.md)
