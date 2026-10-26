<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/encryption-frame-sealed/`

Sealed encryption frame binary layout spec - the byte-offset positions and field lengths of the on-wire **sealed** (asymmetric, ECDH-ES hybrid) encryption envelope: version byte, recipient key-id length prefix + key id, 2-byte big-endian ephemeral-public-key length prefix + ephemeral P-256 public key (SPKI DER), AES-GCM nonce, ciphertext + auth tag.

Sibling of [`contracts/encryption-frame/`](../encryption-frame/README.md) (the symmetric version-1 frame). The
symmetric and sealed catalogs live in separate spec files, so a change to the sealed layout leaves the version-1 spec
and its generated artifacts byte-identical. The leading version byte (`1` symmetric, `2` sealed) is the wire
discriminator, and each decoder rejects the other family's version.

The `EPH_PUB` field introduces the `variable_binary_u16be` field kind: raw (non-UTF-8) binary bytes whose length is
declared by the immediately preceding 2-byte **big-endian** unsigned-integer length field. A P-256
SubjectPublicKeyInfo is about 91 bytes. The `maxEphPubLength` constraint is 256, more than the 1-byte length prefix of
the symmetric frame's kid can hold (255). The decoder checks the declared length against `maxEphPubLength` before it
allocates, so an attacker-controlled prefix cannot force a large allocation.

## Consumed by

- **.NET** - [`packages/dotnet/encryption/frame-source-gen/`](../../packages/dotnet/encryption/frame-source-gen/README.md) (Roslyn source-gen -> `SealedFrameLayout` byte-offset + length constants in `DcsvIo.D2.Encryption`)
- **TypeScript** - constants/types in `@dcsv-io/d2-encryption-abstractions` (generated from this spec; sources committed)

## See also

- Cross-language parity is checked by the shared sealed-frame fixtures and the package tests of both runtimes
- All contracts: [contracts catalog](../README.md)
