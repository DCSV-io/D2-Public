<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/location/`

Location parity-fixture - hand-maintained cross-language hash-determinism test cases for `DcsvIo.D2.Location` value objects (`Coordinates`, `StreetAddress`, `AdminLocation`, and composed location hashes). Each case pins an expected SHA-256 hash ID.

## Consumed by

- **.NET** - [`packages/dotnet/tests/Unit/Location/LocationHashDeterminismTests.cs`](../../packages/dotnet/tests/Unit/Location/LocationHashDeterminismTests.cs) loads `parity-fixtures.json` at test runtime and asserts byte-identical hash output for every case

This is a .NET-only parity fixture - no library is generated from it. No TypeScript `@dcsv-io/d2-location` package
and no fixture emitter exist. Update this file by hand when adding new hash-determinism cases.

## See also

- All contracts: [contracts catalog](../README.md)
