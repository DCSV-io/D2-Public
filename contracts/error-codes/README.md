<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/error-codes/`

Generic error-code catalog — the platform-wide set of cross-domain error codes (`NOT_FOUND`, `FORBIDDEN`, `VALIDATION_FAILED`, `CONFLICT`, etc.) with their HTTP status, error category, and user-message key. Also houses the canonical JSON schema (`error-codes.canonical.schema.json`) that every per-domain error-code spec validates against.

## Consumed by

- **.NET** — [`packages/dotnet/source-gen-shared/error-codes-source-gen/`](../../packages/dotnet/source-gen-shared/error-codes-source-gen/README.md) (Roslyn source-gen → `ErrorCodes` constants + `D2Result` semantic factories in `DcsvIo.D2.Result`). The merged cross-service registry is built by [`error-codes/registry-source-gen/`](../../packages/dotnet/error-codes/registry-source-gen/) (no README).
- **TypeScript** — constants/types in `@dcsv-io/d2-result` and the merged registry in `@dcsv-io/d2-error-codes-registry` (generated from this spec and sibling `*-error-codes` catalogs; sources committed)
- **TypeSpec** — internal IDL tooling; not required for published packages

## See also

- All contracts: [contracts catalog](../README.md)
