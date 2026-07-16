<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/resilience/`

Resilience predicate parity fixtures — cross-language test cases that feed identically-shaped `D2Result` values to the emitted C# and TypeScript `@d2Resilience` retry/fail predicates and assert both produce the same boolean outcomes.

## Consumed by

- **TypeSpec** — monorepo-private TypeSpec decorators (`@dcsv-io/d2-private-typespec-decorators` under `private/packages/typescript/typespec-decorators/` — not on public export) reads the resilience predicate specs to validate `@d2Resilience` decorator arguments at compile time
- **TypeScript** — monorepo-private TypeSpec emitters (`@dcsv-io/d2-private-typespec-emitters` under `private/packages/typescript/typespec-emitters/` — not on public export) byte-gate tests; `predicate-parity.fixture.json` and `predicate-parity-nested.fixture.json` drive cross-language predicate parity assertions

These are test-only parity fixtures — no runtime library is generated from them.

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
