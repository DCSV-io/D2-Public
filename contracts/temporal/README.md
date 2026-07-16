<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/temporal/`

Temporal adversarial parity fixture — cross-language test cases covering DST transitions, ambiguous times, and leap-year boundaries that both .NET (`NodaTime`) and TypeScript (`@js-temporal/polyfill`) must resolve identically.

## Consumed by

- **TypeScript / TypeSpec** — monorepo-private TypeSpec emitters (`@dcsv-io/d2-private-typespec-emitters` under `private/packages/typescript/typespec-emitters/` — not on public export) byte-gate tests; `temporal-adversarial.fixture.json` drives cross-language DST + temporal-arithmetic parity assertions
- **.NET** — `public/packages/dotnet/tests/` temporal parity tests read this fixture to assert `NodaTime` produces the expected UTC instants for every adversarial case

This is a test-only parity fixture — no runtime library is generated from it.

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
