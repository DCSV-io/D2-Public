<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `DcsvIo.D2.Pagination.ErrorCodes.SourceGen`

For a contributor changing the pagination error-code catalog. States where `PaginationErrorCodes` and
`PaginationFailures` come from, and what fails the build when the catalog is wrong.

## Purpose

A Roslyn incremental source generator that emits the pagination catalog's constants class and failure factories into
`DcsvIo.D2.Pagination` from `contracts/pagination-error-codes/pagination-error-codes.spec.json`.

It contains no generation logic. Every generation step - spec loading, validation, constants emission, failure
emission - lives in the shared error-codes engine under `packages/dotnet/source-gen-shared/error-codes-emit/`. This
project supplies one `CatalogConfig` and its own analyzer identity, because the engine's catalog identity is a value it
is handed rather than one it hard-codes.

The project directory is beside `DcsvIo.D2.Pagination`, the package it emits into.

## Public API

`ErrorCodesGenerator` - the `[Generator]` entry point. It has one public entry point, `Initialize`, which the Roslyn
host calls.

## Configuration

None at runtime. The catalog identity is compiled in:

| Field             | Value                                         |
| ----------------- | --------------------------------------------- |
| Target assembly   | `DcsvIo.D2.Pagination`                        |
| Spec file         | `pagination-error-codes.spec.json`            |
| Emitted namespace | `DcsvIo.D2.Pagination.Errors`                 |
| Constants class   | `PaginationErrorCodes`                        |
| Failures classes  | `PaginationFailures`, `PaginationFailures<T>` |
| Wire prefix       | `LIB_PAGINATION_`                             |

The consuming project passes the spec, `error-category.spec.json` and `messages/en-US.json` to the generator through
`AdditionalFiles`.

## Dependencies

- `Microsoft.CodeAnalysis.CSharp`
- `Microsoft.CodeAnalysis.Analyzers`
- `System.Text.Json`

The shared source-gen scaffolding and the error-codes engine are compiled in as linked sources rather than referenced as
assemblies, because a Roslyn component ships as one analyzer file.

## Usage examples

A consuming project wires the generator as an analyzer and hands it the specs:

```xml
<ProjectReference Include="error-codes-source-gen\DcsvIo.D2.Pagination.ErrorCodes.SourceGen.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
<AdditionalFiles Include="$(D2PublicContractsRoot)pagination-error-codes\pagination-error-codes.spec.json" />
```

## Telemetry

None. A source generator runs at compile time and emits no runtime signal.

## Edge cases

| Diagnostic | Fires on                                                                            |
| ---------- | ----------------------------------------------------------------------------------- |
| `D2PGN001` | The spec is malformed JSON or violates its schema                                   |
| `D2PGN002` | An entry's `category` is outside the closed set `error-category.spec.json` declares |
| `D2PGN003` | Two entries declare the same `code`                                                 |
| `D2PGN004` | Two entries declare the same `factoryName`                                          |
| `D2PGN005` | An entry's `httpStatus` is not one the codegen mapping supports                     |

The engine's own diagnostics also apply: `D2ERC001` on a code that does not have the `LIB_PAGINATION_` prefix,
`D2ERC002` on a `userMessageKey` that resolves to no key in `messages/en-US.json`, and `D2ERC003` on an unsupported
`factoryShape`.

When `error-category.spec.json` or `messages/en-US.json` is not passed in, the checks that read them degrade to no-ops
rather than firing false positives.

## Validation ledger

| Emitter            | Validated against                                                                                                                                                                                      | Test double stands in for                                  | What retires the double              |
| ------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------- | ------------------------------------ |
| Constants emission | The real shared engine emitters driven with this catalog's own `CatalogConfig` and the committed spec, asserting the emitted constants, `AllCodes` and `GetHttpStatus` table against the spec entries. | Nothing. The engine, the config and the spec are all real. | Not applicable - there is no double. |
| Failure emission   | The compiled `PaginationFailures` and `PaginationFailures<T>` in `DcsvIo.D2.Pagination`, called from the pagination test suite and asserted on error code, HTTP status and category.                   | Nothing. The emitted classes are compiled and invoked.     | Not applicable - there is no double. |
