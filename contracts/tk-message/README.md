<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/tk-message/`

This directory holds the wire-shape catalog of `TKMessage`: the JSON property names (`key`, `params`) of the
translation-key message object. The object appears in the `messages` array of the `D2Result` envelope and in the
`inputErrors[*].errors` arrays.

## Consumed by

- .NET: the Roslyn source generator in
  [`packages/dotnet/source-gen-shared/wire-shapes-source-gen/`](../../packages/dotnet/source-gen-shared/wire-shapes-source-gen/README.md)
  emits the `TkMessageWireShape` property-name constants in `DcsvIo.D2.I18n.Abstractions`.
- TypeScript: `@dcsv-io/d2-i18n-abstractions` contains constants and types generated from this spec. The generated
  sources are committed.

## See also

- All contracts: [contracts catalog](../README.md)
