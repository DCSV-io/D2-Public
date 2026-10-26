<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.EncryptionDomains.SourceGen

> Parent: [`packages/dotnet/`](../README.md)

**Input contract:** [`contracts/encryption-domains/`](../../../../contracts/encryption-domains/README.md)

A Roslyn incremental source generator that emits encryption-domain catalogs from
`contracts/encryption-domains/encryption-domains.spec.json`.

Assembly-name gate: the consuming assembly `DcsvIo.D2.Encryption` receives `EncryptionDomains`,
`EncryptionDomainMode` and `EncryptionDomainModes`, built from its own AdditionalFiles. Another assembly receives the
same types only when its project sets the MSBuild properties `D2EncryptionDomainsExtensionNamespace` and
`D2EncryptionDomainsExtensionClassPrefix` and declares each as a `CompilerVisibleProperty` item.
`D2EncryptionDomainsExtensionNamespace` sets the namespace, and `D2EncryptionDomainsExtensionClassPrefix` sets the
identifier placed before each type name. Those types cover the union of every `encryption-domains.spec.json` the
assembly lists in AdditionalFiles and are written to the prefixed `EncryptionDomains` name followed by `.g.cs`. The
public assembly ignores these properties, and an assembly that sets neither receives none of these types.

Convention: spec-driven Roslyn `IIncrementalGenerator` (netstandard2.0 analyzer; spec via `<AdditionalFiles>`).

## What this emits

When the consuming assembly is `DcsvIo.D2.Encryption`, the generator emits `EncryptionDomains.g.cs` containing:

- one `public const string` per spec entry plus an `AllDomains` membership list;
- `public enum EncryptionDomainMode { Symmetric, Sealed }` - the per-domain encryption mode;
- `public static class EncryptionDomainModes` with `ModeFor(string domain)` (unknown domain -> `Symmetric`, the
  documented default), `TryGetConsumerService(string domain, out string)`, and
  `IReadOnlyDictionary<string, string> ConsumerServiceByDomain` (sealed domains only).

The closed catalog includes the `PLAINTEXT` sentinel, so the `DcsvIo.D2.Messaging.SourceGen` cross-validation of
`mq-messages.spec.json:encryption` field values has one source for the valid values.

### Per-domain mode + consumer service

Each domain optionally declares `mode` (`symmetric` - the default when absent - or `sealed`) and, for a sealed domain, a
required `consumerService` (the single decrypting recipient's ServiceId, `[a-z0-9-]{1,64}`). Symmetric domains share a
keyring (v1 frame); sealed domains route to one recipient service's public seal key (v2 frame), and only that service
opens. The emitter fails the build if `mode` is unknown, a sealed domain omits `consumerService`, a non-sealed domain
declares one, or the `consumerService` grammar is violated.

## Why spec-drive this

A typo on the producer side or the consumer side causes a compile error, so no message is routed at runtime to a
keyring that does not exist. The spec is the closed catalog of valid identifiers, and the
generator enforces it at build time.

## Cross-language parity

The same spec drives `@dcsv-io/d2-encryption-abstractions` (sources committed). TypeScript code that reads the catalog
(ops tooling, RabbitMQ subscribers, encryption pipelines) uses the same identifier bytes as the .NET producers.

## Diagnostics

| ID        | Title                                      | Severity |
| --------- | ------------------------------------------ | -------- |
| `D2ED001` | Encryption domains spec is malformed       | Error    |
| `D2ED002` | Duplicate constName                        | Error    |
| `D2ED003` | Duplicate wire value                       | Error    |
| `D2ED004` | constName has invalid shape                | Error    |
| `D2ED005` | Empty wire value                           | Error    |
| `D2ED006` | Invalid mode (not symmetric/sealed)        | Error    |
| `D2ED007` | Sealed domain missing consumerService      | Error    |
| `D2ED008` | consumerService on a non-sealed domain     | Error    |
| `D2ED009` | consumerService violates `[a-z0-9-]{1,64}` | Error    |
| `D2ED010` | Invalid extension namespace                | Error    |
| `D2ED011` | Missing or invalid class prefix            | Error    |
| `D2ED012` | Class prefix without a namespace           | Warning  |
