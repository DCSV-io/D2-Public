<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/encryption-domains/`

Encryption domain registry — the closed set of public keyring domains (`plaintext` sentinel + optional framework fixture sealed domain). Hosts may register additional product sealed domains (for example audit / notifications / courier keyrings) outside this package to identify which keyring encrypts a given RabbitMQ payload.

## Per-domain mode

Each domain optionally declares a `mode`:

- **`symmetric`** (the default when the field is absent — strict back-compat) — a shared keyring AES-256-GCM (version-1 frame); every grant-holder both encrypts and decrypts.
- **`sealed`** — per-consumer-service ephemeral-static ECDH (version-2 frame); producers seal to the recipient service's public key and only that one service opens. A sealed domain MUST declare `consumerService` (the single decryptor's ServiceId, `[a-z0-9-]{1,64}`); a non-sealed domain MUST NOT. This public catalog carries the `plaintext` sentinel (no mode) plus a framework fixture sealed domain for Shared.Tests; hosts extend with product sealed domains at composition time.

Both emitters fail the build on an inconsistent `mode` / `consumerService` pair.

## Consumed by

- **.NET** — [`packages/dotnet/encryption/domains-source-gen/`](../../packages/dotnet/encryption/domains-source-gen/README.md) (Roslyn source-gen → `EncryptionDomains` constants in `DcsvIo.D2.Encryption`)
- **TypeScript** — constants/types in `@dcsv-io/d2-encryption-abstractions` (generated from this spec; sources committed)

## See also

- All contracts: [contracts catalog](../README.md)
