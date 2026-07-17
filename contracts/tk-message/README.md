<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/tk-message/`

`TKMessage` wire-shape catalog — the JSON property names (`key`, `params`) of the translation-key message object used throughout the `D2Result` envelope's `messages` and `inputErrors[*].errors` arrays.

## Consumed by

- **.NET** — [`packages/dotnet/source-gen-shared/wire-shapes-source-gen/`](../../packages/dotnet/source-gen-shared/wire-shapes-source-gen/README.md) (Roslyn source-gen → `TkMessageWireShape` property-name constants in `DcsvIo.D2.I18n.Abstractions`)
- **TypeScript** — constants/types in `@dcsv-io/d2-i18n-abstractions` (generated from this spec; sources committed)

## See also

- All contracts: [contracts catalog](../README.md)
