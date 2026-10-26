<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.EncryptionFrame.SourceGen

> Parent: [`packages/dotnet/`](../README.md)

**Input contracts:** [`contracts/encryption-frame/`](../../../../contracts/encryption-frame/README.md) (symmetric,
version 1) + [`contracts/encryption-frame-sealed/`](../../../../contracts/encryption-frame-sealed/README.md) (sealed,
version 2)

Roslyn incremental source generators <!-- tally:exempt-line: wording kept -->
in one analyzer project (`EncryptionFrameGenerator` and `SealedFrameGenerator`)
that emit the closed catalogs of binary-layout offsets, byte lengths, and constraints for the D2 on-wire encryption
frames: `EncryptionFrameLayout` from `contracts/encryption-frame/encryption-frame.spec.json` (the symmetric version-1
frame) and `SealedFrameLayout` from the sibling `contracts/encryption-frame-sealed/encryption-frame-sealed.spec.json`
(the sealed version-2 frame). The generators <!-- tally:exempt-line: wording kept -->
filter on different spec file names, so neither reads the other's catalog and the version-1 artifacts stay
byte-identical while the sealed layout changes.

Convention: spec-driven Roslyn `IIncrementalGenerator` (netstandard2.0 analyzer; spec via `<AdditionalFiles>`).

## What this emits

When the consuming assembly is `DcsvIo.D2.Encryption`:

- `EncryptionFrameGenerator` emits `EncryptionFrameLayout.g.cs` containing a `CURRENT_VERSION` constant, per-field
  `*_OFFSET` + `*_LENGTH` constants, and frame-level constraint constants (`CONSTRAINT_MIN_KID_LENGTH`,
  `CONSTRAINT_MAX_KID_LENGTH`, `CONSTRAINT_NONCE_LENGTH`, `CONSTRAINT_TAG_LENGTH`, `CONSTRAINT_MIN_FRAME_SIZE`). The
  `CONSTRAINT_` prefix separates the frame-level constraint constants from the per-field `*_LENGTH` constants (for
  example, `NONCE_LENGTH` is the byte length declared for the `NONCE` field, and `CONSTRAINT_NONCE_LENGTH` is the
  frame-level AES-GCM value that the field must equal).
- `SealedFrameGenerator` emits `SealedFrameLayout.g.cs` - same constants-only shape plus the sealed-only constraints
  `CONSTRAINT_EPH_PUB_LENGTH_PREFIX_SIZE` (the 2-byte big-endian length-prefix width in front of the ephemeral public
  key) and `CONSTRAINT_MAX_EPH_PUB_LENGTH` (the allocation cap on the declared key length).

## The `variable_binary_u16be` field kind (sealed spec only)

The sealed schema adds a field kind the symmetric catalog does not need: **`variable_binary_u16be`** - raw (non-UTF-8)
binary bytes whose length is declared by the immediately preceding 2-byte big-endian unsigned-integer length field. The
sealed frame's ephemeral public key uses it. A P-256 SubjectPublicKeyInfo
is about 91 bytes. <!-- tally:exempt-line: a key size, not a set count -->
The `maxEphPubLength` cap in `contracts/encryption-frame-sealed/encryption-frame-sealed.spec.json` is 256, more than the
1-byte length prefix the kid uses can declare (255). The emitter enforces the structural rule at build time: a
`variable_binary_u16be` field that is not immediately preceded by a `byte_fixed` field of exactly the declared prefix
width fails with `D2EF012`, so the build fails before the codec sees a spec it cannot parse.

## Why spec-drive this

The TypeScript package `@dcsv-io/d2-encryption-abstractions` exposes the same binary frame-layout constants as the .NET
codecs. One spec catalog per frame version drives both sides, so a TypeScript reader and the .NET codec use identical
byte offsets and lengths, and neither side keeps a separate constant catalog that can drift when the frame version
changes.

## Cross-language parity

The same specs drive `@dcsv-io/d2-encryption-abstractions` (v1 + v2 sealed layouts; sources committed). Both sides use
identical offsets + lengths because both are generated from the same specs.

## Diagnostics

| ID        | Title                                                                            | Severity |
| --------- | -------------------------------------------------------------------------------- | -------- |
| `D2EF001` | Encryption-frame spec is malformed                                               | Error    |
| `D2EF002` | Duplicate field constName                                                        | Error    |
| `D2EF003` | Fixed-offset fields overlap                                                      | Error    |
| `D2EF004` | Field has invalid length                                                         | Error    |
| `D2EF005` | Spec version is invalid                                                          | Error    |
| `D2EF006` | Sealed encryption-frame spec is malformed                                        | Error    |
| `D2EF007` | Duplicate sealed field constName                                                 | Error    |
| `D2EF008` | Sealed fixed-offset fields overlap                                               | Error    |
| `D2EF009` | Sealed field has invalid length                                                  | Error    |
| `D2EF010` | Sealed spec version is invalid (must be >= 2 - version 1 is the symmetric frame) | Error    |
| `D2EF011` | Sealed field kind is unknown                                                     | Error    |
| `D2EF012` | `variable_binary_u16be` field lacks its preceding 2-byte length prefix           | Error    |
