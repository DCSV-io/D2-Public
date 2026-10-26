<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `DcsvIo.D2.Messaging.RabbitMq.ErrorCodes.SourceGen`

For a contributor changing the messaging error-code catalog. States where `MessagingErrorCodes` and
`MessagingTransportFailures` come from, and what fails the build when the catalog is wrong.

## Purpose

A Roslyn incremental source generator that emits the messaging catalog's constants class and failure factories into
`DcsvIo.D2.Messaging.RabbitMq` from `contracts/messaging-error-codes/messaging-error-codes.spec.json`.

It contains no generation logic. Every generation step - spec loading, validation, constants emission, failure
emission - is implemented in the shared error-codes engine under
`packages/dotnet/source-gen-shared/error-codes-emit/`. This project supplies one
`CatalogConfig` and its own analyzer identity, because the engine receives its catalog identity as a value and does
not hard-code it.

This project is in a subfolder of the project folder of `DcsvIo.D2.Messaging.RabbitMq`, the package it emits into.

## Public API

`ErrorCodesGenerator` - the `[Generator]` entry point. It has one public entry point, `Initialize`, which the Roslyn
host calls.

## Configuration

None at runtime. The catalog identity is compiled in:

| Field             | Value                                                         |
| ----------------- | ------------------------------------------------------------- |
| Target assembly   | `DcsvIo.D2.Messaging.RabbitMq`                                |
| Spec file         | `messaging-error-codes.spec.json`                             |
| Emitted namespace | `DcsvIo.D2.Messaging.RabbitMq.Errors`                         |
| Constants class   | `MessagingErrorCodes`                                         |
| Failures classes  | `MessagingTransportFailures`, `MessagingTransportFailures<T>` |
| Wire prefix       | `LIB_MESSAGING_`                                              |

The consuming project supplies the spec, `error-category.spec.json` and `messages/en-US.json` through `AdditionalFiles`.

## Dependencies

- `Microsoft.CodeAnalysis.CSharp`
- `Microsoft.CodeAnalysis.Analyzers`
- `System.Text.Json`

The shared source-gen scaffolding and the error-codes engine are compiled in as linked sources rather than referenced as
assemblies, because a Roslyn component ships as one analyzer file.

## Usage examples

A consuming project adds the generator as an analyzer and passes it the specs:

```xml
<ProjectReference Include="error-codes-source-gen\DcsvIo.D2.Messaging.RabbitMq.ErrorCodes.SourceGen.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
<AdditionalFiles Include="$(D2PublicContractsRoot)messaging-error-codes\messaging-error-codes.spec.json" />
```

## Telemetry

None. A source generator runs at compile time and emits no runtime signal.

## Edge cases

| Diagnostic | Fires on                                                                            |
| ---------- | ----------------------------------------------------------------------------------- |
| `D2MEC001` | The spec is malformed JSON or violates its schema                                   |
| `D2MEC002` | An entry's `category` is outside the closed set `error-category.spec.json` declares |
| `D2MEC003` | Two entries declare the same `code`                                                 |
| `D2MEC004` | Two entries declare the same `factoryName`                                          |
| `D2MEC005` | An entry's `httpStatus` is not one the codegen mapping supports                     |

The engine's own diagnostics also apply: `D2ERC001` on a code that does not start with the `LIB_MESSAGING_` prefix,
`D2ERC002` on a `userMessageKey` that resolves to no key in `messages/en-US.json`, and `D2ERC003` on an unsupported
`factoryShape`.

When `error-category.spec.json` or `messages/en-US.json` is not supplied, the checks that read them do nothing
and do not report false positives.

## Validation ledger

| Emitter            | Validated against                                                                                                                                                                                           | Test double stands in for                                  | What retires the double              |
| ------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------- | ------------------------------------ |
| Constants emission | The real shared engine emitters driven with this catalog's own `CatalogConfig` and the committed spec, asserting the emitted constants, `AllCodes` and `GetHttpStatus` table against the spec entries.      | Nothing. The engine, the config and the spec are all real. | Not applicable - there is no double. |
| Failure emission   | The compiled `MessagingTransportFailures` and `MessagingTransportFailures<T>` in `DcsvIo.D2.Messaging.RabbitMq`, called from the messaging test suite and asserted on error code, HTTP status and category. | Nothing. The emitted classes are compiled and invoked.     | Not applicable - there is no double. |
