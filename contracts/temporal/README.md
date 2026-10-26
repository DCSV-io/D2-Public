<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/temporal/`

This directory holds a test fixture of adversarial temporal cases for cross-language parity. The cases cover DST
transitions, ambiguous local times and leap-year boundaries. Both .NET (`NodaTime`) and TypeScript
(`temporal-polyfill`) must resolve each case to the same instant.

## Consumed by

- TypeScript: the tests of the `@dcsv-io/d2-time` package read `temporal-adversarial.fixture.json` and use it for
  cross-language parity assertions on DST and temporal arithmetic.
- .NET: the temporal parity tests in `packages/dotnet/tests/` read this fixture and assert that `NodaTime` produces the
  expected UTC instant for every adversarial case.

No runtime library is generated from this fixture. Only tests read it.

## See also

- All contracts: [contracts catalog](../README.md)
