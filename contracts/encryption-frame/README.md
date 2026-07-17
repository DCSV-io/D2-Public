<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/encryption-frame/`

Encryption frame binary layout spec — the byte-offset positions and field lengths of the on-wire encryption envelope (version byte, domain identifier, key-id length prefix, IV, ciphertext, auth tag).

## Consumed by

- **.NET** — [`packages/dotnet/encryption/frame-source-gen/`](../../packages/dotnet/encryption/frame-source-gen/README.md) (Roslyn source-gen → `EncryptionFrameLayout` byte-offset + length constants in `DcsvIo.D2.Encryption`)
- **TypeScript** — constants/types in `@dcsv-io/d2-encryption-abstractions` (generated from this spec; sources committed)

## See also

- All contracts: [contracts catalog](../README.md)
