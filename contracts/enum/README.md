<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/enum/`

Cross-language enum wire-parity fixture — defines sample enum types with their members and expected wire strings; both .NET and TypeScript enum-codec tests drive this fixture to assert identical round-trip serialization across all transports.

## Consumed by

- **TypeScript / TypeSpec** — monorepo-private TypeSpec emitters (`@dcsv-io/d2-private-typespec-emitters` under `private/packages/typescript/typespec-emitters/` — not on public export) byte-gate tests; the fixture feeds the enum round-trip parity tests that guard cross-language wire compatibility
- **.NET** — `public/packages/dotnet/tests/` enum round-trip tests read this fixture and assert matching behavior

This is a test-only parity fixture — no runtime library is generated from it.

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
