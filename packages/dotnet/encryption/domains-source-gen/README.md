<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.EncryptionDomains.SourceGen

> Parent: [`packages/dotnet/`](../README.md)

**Input contract:** [`contracts/encryption-domains/`](../../../../contracts/encryption-domains/README.md)

Roslyn incremental source generator that emits encryption-domain catalogs from `contracts/encryption-domains/encryption-domains.spec.json`.

**Dual-target** (assembly-name gate — public twin + optional host extensions assembly):

| Consuming assembly | Emitted types | Values |
| --- | --- | --- |
| `DcsvIo.D2.Encryption` | `EncryptionDomains` / `EncryptionDomainMode` / `EncryptionDomainModes` | public AdditionalFiles only |
| Host extensions assembly (when present) | host domain overlays under host namespace | public∪host AdditionalFiles |

Any other assembly → no emit. A host extensions assembly (when present) is named 1:1 with the public twin plus a `.Extensions` suffix.

**Convention**: spec-driven Roslyn `IIncrementalGenerator` (netstandard2.0 analyzer; spec via `<AdditionalFiles>`).

## What this emits

When the consuming assembly is `DcsvIo.D2.Encryption`, the generator emits `EncryptionDomains.g.cs` containing:

- one `public const string` per spec entry plus an `AllDomains` membership list;
- `public enum EncryptionDomainMode { Symmetric, Sealed }` — the per-domain encryption mode;
- `public static class EncryptionDomainModes` with `ModeFor(string domain)` (unknown domain → `Symmetric`, the documented default), `TryGetConsumerService(string domain, out string)`, and `IReadOnlyDictionary<string, string> ConsumerServiceByDomain` (sealed domains only).

The closed catalog includes the `PLAINTEXT` sentinel so the `MqMessages.SourceGen` cross-validation of `mq-messages.spec.json:encryption` field values has one unambiguous source of truth.

### Per-domain mode + consumer service

Each domain optionally declares `mode` (`symmetric` — the default when absent — or `sealed`) and, for a sealed domain, a required `consumerService` (the single decrypting recipient's ServiceId, `[a-z0-9-]{1,64}`). Symmetric domains share a keyring (v1 frame); sealed domains route to one recipient service's public seal key (v2 frame), and only that service opens. The emitter fails the build if `mode` is unknown, a sealed domain omits `consumerService`, a non-sealed domain declares one, or the `consumerService` grammar is violated.

## Why spec-drive this

A typo on either the producer or consumer side surfaces as a compile error rather than silently routing a message to a non-existent keyring at runtime. The spec is the closed catalog of valid identifiers, enforced at codegen time.

## Cross-language parity

The SAME spec drives `@dcsv-io/d2-encryption-abstractions` (sources committed). Any TS code reading the catalog (ops tooling, RabbitMQ subscribers, encryption pipelines) shares byte-equal identifiers with the .NET producers.

## Diagnostics

| ID        | Title                                | Severity |
| --------- | ------------------------------------ | -------- |
| `D2ED001` | Encryption domains spec is malformed | Error    |
| `D2ED002` | Duplicate constName                  | Error    |
| `D2ED003` | Duplicate wire value                 | Error    |
| `D2ED004` | constName has invalid shape          | Error    |
| `D2ED005` | Empty wire value                     | Error    |
| `D2ED006` | Invalid mode (not symmetric/sealed)  | Error    |
| `D2ED007` | Sealed domain missing consumerService | Error   |
| `D2ED008` | consumerService on a non-sealed domain | Error  |
| `D2ED009` | consumerService violates `[a-z0-9-]{1,64}` | Error |
