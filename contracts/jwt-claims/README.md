<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/jwt-claims/`

JWT claim-name catalog — the closed set of standard and `d2_`-prefixed custom JWT claim names parsed by the auth middleware into `IAuthContext` properties.

## Consumed by

- **.NET** — [`packages/dotnet/auth/jwt-claims-source-gen/`](../../packages/dotnet/auth/jwt-claims-source-gen/README.md) (Roslyn source-gen → `JwtClaimTypes` constants in `DcsvIo.D2.Auth.Abstractions`)
- **TypeScript** — constants/types in `@dcsv-io/d2-auth-abstractions` (generated from this spec; sources committed)

## See also

- All contracts: [contracts catalog](../README.md)
