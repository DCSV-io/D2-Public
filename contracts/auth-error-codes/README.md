<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/auth-error-codes/`

Auth error-code catalog — the closed set of authentication and authorization failure codes (bearer missing, JWT expired, scope insufficient, session revoked, etc.) with their HTTP status, error category, and user-message key.

## Consumed by

- **.NET** — monorepo-private `private/packages/dotnet/auth/error-codes-source-gen/` (not on public export) (Roslyn source-gen → `AuthErrorCodes` constants + `AuthFailures` typed `D2Result` factories in `DcsvIo.D2.Private.Auth`; monorepo product — not public SoT)
- **TypeScript** — monorepo-private `private/tools/ts-codegen` › `error-codes-emit.ts` (not on public export) (→ `AuthErrorCodes` constants + `AuthFailures.*` factories in `@dcsv-io/d2-auth-abstractions`)
- **TypeSpec** — monorepo-private TypeSpec decorators (`@dcsv-io/d2-private-typespec-decorators` under `private/packages/typescript/typespec-decorators/` — not on public export) reads every `*-error-codes.spec.json` to validate decorator arguments at compile time

This catalog is also merged into the cross-service registry — see [`contracts/error-codes/`](../error-codes/README.md).

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
