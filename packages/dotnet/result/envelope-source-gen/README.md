<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Result.Envelope.SourceGen

> Parent: [`packages/dotnet/`](../../README.md)

**Input contract:** [`contracts/d2result-envelope/`](../../../../contracts/d2result-envelope/README.md)

Roslyn incremental source generator that emits the `D2ResultEnvelopeFieldNames` JSON property-name catalog from
`contracts/d2result-envelope/d2result-envelope.spec.json`, the source of truth for the D2Result Shape B wire envelope
(the gateway response shape a host serializes to its clients).

Convention: the generator follows the spec-driven Roslyn `IIncrementalGenerator` pattern (file layout, diagnostic ID
convention, generator anatomy, `<AdditionalFiles>` wiring).

## Single-target dispatch

| Consuming assembly | Emits                             | Class name                   |
| ------------------ | --------------------------------- | ---------------------------- |
| `DcsvIo.D2.Result` | `D2ResultEnvelopeFieldNames.g.cs` | `D2ResultEnvelopeFieldNames` |
| any other          | (nothing)                         | -                            |

## What the catalog contains

The field-name constants:

| `constName`    | wire value    | role                                                     |
| -------------- | ------------- | -------------------------------------------------------- |
| `SUCCESS`      | `success`     | boolean success flag                                     |
| `DATA`         | `data`        | result payload (generic over TData)                      |
| `MESSAGES`     | `messages`    | `TKMessage[]` translation messages                       |
| `INPUT_ERRORS` | `inputErrors` | `InputError[]` per-field validation errors               |
| `ERROR_CODE`   | `errorCode`   | standardized error code string                           |
| `TRACE_ID`     | `traceId`     | W3C trace id for log correlation                         |
| `STATUS_CODE`  | `statusCode`  | HTTP status code integer                                 |
| `CATEGORY`     | `category`    | closed `ErrorCategory` classification (absent when null) |

## How D2Result consumes the catalog

The hand-written `D2Result` / `D2Result<TData>` classes have `[JsonPropertyName(D2ResultEnvelopeFieldNames.SUCCESS)]`
(etc.) attributes on each property, so the generated constants are the single source of truth for the wire-key strings.
The property name is set explicitly on each property and is not taken from the calling endpoint's
`JsonSerializerOptions`: the envelope keys render as camelCase regardless of whether the caller configures
`JsonNamingPolicy.CamelCase`.

This pattern (codegen emits constants; hand-applied attributes on the type) is the same pattern as the
`DlqFailureMetadata` integration of `DcsvIo.D2.Messaging.DlqMetadata.SourceGen`. Applying the attributes directly keeps
the code simple, references each key as a typed constant, and gives the correct wire name for the mixed-type properties
(`bool` / `T?` / `IReadOnlyList<TKMessage>` / `HttpStatusCode` / `string?`). A C# 13 partial-property
`[JsonPropertyName]` split would be fragile across those types.

## Cross-language parity

The same spec drives the TS-side `@dcsv-io/d2-result` catalog (generated sources committed). Both sides emit the same
property names, byte for byte, because both are generated from this spec. A host's gateway parsers should read the names
through the generated constants.

## Diagnostics

| ID         | Title                       | Severity |
| ---------- | --------------------------- | -------- |
| `D2DRE001` | Spec is malformed           | Error    |
| `D2DRE002` | Duplicate field constName   | Error    |
| `D2DRE003` | Duplicate field value       | Error    |
| `D2DRE004` | constName has invalid shape | Error    |
| `D2DRE005` | Empty wire value            | Error    |

---

## Validation ledger

| Emitter                      | Validated against                                                                                                                                                                                            | Test double stands in for                                                                      | What retires the double              |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------- | ------------------------------------ |
| `D2ResultEnvelopeEmitter`    | `D2ResultEnvelopeEmitterTests` drives the real emitter over spec entries and asserts the emitted source; `D2ResultEnvelopeGeneratorTests` runs the real `IIncrementalGenerator` through a `GeneratorDriver`. | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| `D2ResultEnvelopeSpecLoader` | `D2ResultEnvelopeSpecLoaderTests` parses spec JSON through the real loader, including the malformed and schema-violating shapes.                                                                             | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| Diagnostics                  | `D2ResultEnvelopeDiagnosticIdsTests` asserts each declared descriptor's id, severity and category.                                                                                                           | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
