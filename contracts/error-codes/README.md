<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/error-codes/`

Generic error-code catalog — the platform-wide set of cross-domain error codes (`NOT_FOUND`, `FORBIDDEN`, `VALIDATION_FAILED`, `CONFLICT`, etc.) with their HTTP status, error category, and user-message key. Also houses the canonical JSON schema (`error-codes.canonical.schema.json`) that every per-domain error-code spec validates against.

## Consumed by

- **.NET** — [`public/packages/dotnet/source-gen-shared/error-codes-source-gen/`](../../packages/dotnet/source-gen-shared/error-codes-source-gen/README.md) (Roslyn source-gen → `ErrorCodes` constants + `D2Result` semantic factories in `DcsvIo.D2.Result`). The merged cross-service registry is built by [`error-codes/registry-source-gen/`](../../packages/dotnet/error-codes/registry-source-gen/) (no README).
- **TypeScript** — monorepo-private `private/tools/ts-codegen` › `error-codes-emit.ts` (not on public export) (→ generic error-code constants + base `D2Result` factories in `@dcsv-io/d2-result`); monorepo-private `private/tools/ts-codegen` › `error-codes-registry-emit.ts` (not on public export) globs every `*-error-codes.spec.json` (including this one) into the merged registry in `@dcsv-io/d2-error-codes-registry`
- **TypeSpec** — monorepo-private TypeSpec decorators (`@dcsv-io/d2-private-typespec-decorators` under `private/packages/typescript/typespec-decorators/` — not on public export) reads every `*-error-codes.spec.json` to validate decorator arguments at compile time

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
