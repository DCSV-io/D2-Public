<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/auth-scopes/`

OAuth scope catalog - the closed set of permission scopes, each with its impersonation-blocked flag, its
workload-only `internal` flag and its grant matrix. A scope carries no action sensitivity: how dangerous an action is
belongs to the operation, declared there as `ActionSensitivity` (`DcsvIo.D2.Auth.Abstractions`) and carried as endpoint
metadata, because one scope can be granted to several operations.

## Namespace conventions (grant model)

- `anon.*` - anonymous (pre-auth) scopes, universally granted by namespace convention; they omit `grantedTo`.
- `internal.*` - internal service-to-service / in-process workload scopes (host-specific rows live in the host application's own scope-values catalog), granted by the internal transaction-token mint at the application boundary, never by the per-(`OrgType`, `Role`) grant matrix; they omit `grantedTo` and no user org-role can ever hold them (the intended reachability for a workload scope). An entry in this namespace declares `"internal": true`, and one outside it does not - a disagreement in either direction is `D2SCP010`.
- Every other scope requires a `grantedTo` matrix - a non-`anon.*` scope that does not declare `internal` and omits it is an unreachable-scope error (`D2SCP008`).

## Consumed by

- **.NET** - [`packages/dotnet/auth/scopes-source-gen/`](../../packages/dotnet/auth/scopes-source-gen/README.md) (Roslyn source-gen -> `Scopes` scope-tree constants in `DcsvIo.D2.Auth.Abstractions`, consumed by per-handler `RequiredScopes` options)
- **TypeScript** - constants/types in `@dcsv-io/d2-auth-abstractions` (generated from this spec; sources committed)
- **TypeSpec** - internal IDL tooling; not required for published packages

## See also

- All contracts: [contracts catalog](../README.md)
