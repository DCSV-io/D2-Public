<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/messages/`

i18n message catalog — one JSON file per supported locale (`en-US`, `en-GB`, `en-CA`, `fr-FR`, `fr-CA`, `de-DE`, `es-ES`, `es-MX`, `it-IT`, `ja-JP`) containing the full set of user-facing translation keys and their localized string values.

## Consumed by

- **.NET** — [`packages/dotnet/i18n/source-gen/`](../../packages/dotnet/i18n/source-gen/README.md) (Roslyn `TKGenerator` → decomposes every key in `en-US.json` into a nested `TK.<domain>.<category>.<CONSTANT>` const tree in `DcsvIo.D2.I18n.Keys`)
- **TypeScript** — constants/types in `@dcsv-io/d2-i18n` / `@dcsv-io/d2-i18n-keys` (generated from this spec; sources committed)
- **Paraglide** (or equivalent host i18n compilers) — may compile the same locale files into optimized per-locale message modules at build time

## See also

- All contracts: [contracts catalog](../README.md)
