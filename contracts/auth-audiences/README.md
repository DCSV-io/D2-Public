<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/auth-audiences/`

Token-exchange target audience catalog — the per-service audience URLs used when the Edge mints scoped internal tokens via RFC 8693 exchange.

## Consumed by

- **.NET** — [`packages/dotnet/auth/audiences-source-gen/`](../../packages/dotnet/auth/audiences-source-gen/README.md) (Roslyn source-gen → `Audiences` constants in `DcsvIo.D2.Auth.Abstractions`)
- **TypeSpec** — internal IDL tooling; not required for published packages

No dedicated TypeScript const-object is generated from this catalog — audience names are validated at IDL compile time rather than as a published TS catalog.

## See also

- All contracts: [contracts catalog](../README.md)
