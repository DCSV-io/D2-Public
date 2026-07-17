<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/input-error/`

`InputError` wire-shape catalog — the JSON property names (`field`, `errors`) of the per-field validation error object nested inside a `D2Result` envelope's `inputErrors` array.

## Consumed by

- **.NET** — [`packages/dotnet/source-gen-shared/wire-shapes-source-gen/`](../../packages/dotnet/source-gen-shared/wire-shapes-source-gen/README.md) (Roslyn source-gen → `InputErrorWireShape` property-name constants in `DcsvIo.D2.Result`)
- **TypeScript** — constants/types in `@dcsv-io/d2-result` (generated from this spec; sources committed)

## See also

- All contracts: [contracts catalog](../README.md)
