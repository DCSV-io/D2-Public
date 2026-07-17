<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/resilience/`

Resilience predicate parity fixtures — cross-language test cases that feed identically-shaped `D2Result` values to the emitted C# and TypeScript `@d2Resilience` retry/fail predicates and assert both produce the same boolean outcomes.

## Consumed by

- **TypeSpec** — internal IDL tooling; not required for published packages
- **TypeScript** — `@dcsv-io/d2-resilience` package tests; `predicate-parity.fixture.json` and `predicate-parity-nested.fixture.json` drive cross-language predicate parity assertions

These are test-only parity fixtures — no runtime library is generated from them.

## See also

- All contracts: [contracts catalog](../README.md)
