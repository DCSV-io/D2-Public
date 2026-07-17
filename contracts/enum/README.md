<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/enum/`

Cross-language enum wire-parity fixture — defines sample enum types with their members and expected wire strings; both .NET and TypeScript enum-codec tests drive this fixture to assert identical round-trip serialization across all transports.

## Consumed by

- **TypeScript** — parity fixtures for cross-language enum wire compatibility (committed under this tree / package tests)
- **.NET** — `packages/dotnet/tests/` enum round-trip tests read this fixture and assert matching behavior

This is a test-only parity fixture — no runtime library is generated from it.

## See also

- All contracts: [contracts catalog](../README.md)
