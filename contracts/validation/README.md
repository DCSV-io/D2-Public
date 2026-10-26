<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/validation/`

This directory holds the validation rule catalog and the cross-language parity fixtures. `field-constraints.spec.json`
defines field-length limits, the named taxonomy enums (`NamePrefix`, `NameSuffix`, `BiologicalSex`) and other
per-field constraints. The `fixtures/` subdirectory holds parity cases for the email, phone and postal-code
validators.

## Consumed by

- .NET: the Roslyn source generator in
  [`packages/dotnet/validation/source-gen/`](../../packages/dotnet/validation/source-gen/README.md) emits the
  `FieldConstraints` static class and the taxonomy enum types in `DcsvIo.D2.Validation.Abstractions`.
- TypeScript: `@dcsv-io/d2-validation-abstractions` contains constants and types generated from this spec. The
  generated sources are committed.

## See also

- All contracts: [contracts catalog](../README.md)
