<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/error-codes/`

Generic (owner **gen**) error-code catalog - platform-wide cross-domain codes with wire prefix **`GEN_*`**
(`GEN_NOT_FOUND`, `GEN_FORBIDDEN`, `GEN_VALIDATION_FAILED`, `GEN_CONFLICT`, etc.) plus HTTP status, error category, and
user-message key. This folder also contains the canonical JSON schema (`error-codes.canonical.schema.json`) that every
per-domain error-code spec validates against.

## Consumed by

- **.NET** - [`packages/dotnet/source-gen-shared/error-codes-source-gen/`](../../packages/dotnet/source-gen-shared/error-codes-source-gen/README.md) (Roslyn source-gen -> `ErrorCodes` constants + `D2Result` semantic factories in `DcsvIo.D2.Result`). The public merged registry is built by [`error-codes/registry-source-gen/`](../../packages/dotnet/error-codes/registry-source-gen/).
- **TypeScript** - constants/types in `@dcsv-io/d2-result` and the public merged registry in `@dcsv-io/d2-error-codes-registry` (generated from this spec and sibling public `*-error-codes` catalogs; sources committed)
- **TypeSpec** - internal IDL tooling; not required for published packages

## See also

- All contracts: [contracts catalog](../README.md)
