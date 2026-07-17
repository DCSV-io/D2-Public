<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/auth-protocol-audiences/`

Protocol audience catalog — the two fixed internal receive audiences (`d2.internal` universal forward-unchanged audience, `d2-edge` Edge self-audience) that distinguish internal-transaction tokens from user-facing tokens.

## Consumed by

- **.NET** — [`packages/dotnet/auth/protocol-audiences-source-gen/`](../../packages/dotnet/auth/protocol-audiences-source-gen/) (Roslyn source-gen → `WellKnownAudiences` constants in `DcsvIo.D2.Auth.Abstractions`; no README)
- **TypeScript** — constants/types in `@dcsv-io/d2-auth-abstractions` (generated from this spec; sources committed)
- **TypeSpec** — internal IDL tooling; not required for published packages

## See also

- All contracts: [contracts catalog](../README.md)
