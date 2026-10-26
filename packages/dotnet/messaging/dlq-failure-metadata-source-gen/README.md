<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Messaging.DlqMetadata.SourceGen

> Parent: [`packages/dotnet/`](../../README.md)

**Input contract:** [`contracts/dlq-failure-metadata/`](../../../../contracts/dlq-failure-metadata/README.md)

Roslyn incremental source generator that emits the dead-letter queue (DLQ) failure-metadata catalogs from
`contracts/dlq-failure-metadata/dlq-failure-metadata.spec.json`.

Convention: spec-driven Roslyn `IIncrementalGenerator` (netstandard2.0 analyzer; spec via `<AdditionalFiles>`).

## Multi-target dispatch

| Consuming assembly                 | Emits                           | Class name                                       |
| ---------------------------------- | ------------------------------- | ------------------------------------------------ |
| `DcsvIo.D2.Messaging.Abstractions` | `DlqFailureMetadataFields.g.cs` | `DlqFailureMetadataFields` (JSON property names) |
| `DcsvIo.D2.Messaging.RabbitMq`     | `DlqFailureCauses.g.cs`         | `DlqFailureCauses` (closed-enum cause strings)   |
| any other                          | (nothing)                       | -                                                |

The fields and the causes are in one spec because they describe the same wire shape. They are emitted into different
assemblies because `DlqFailureHeaderBuilder`, which produces the cause strings, is in the `DcsvIo.D2.Messaging.RabbitMq`
project, while `DlqFailureMetadata`, the record whose properties the field names match, is in the
`DcsvIo.D2.Messaging.Abstractions` project.

## What the catalog contains

- Fields: `CAUSE`, `ERROR_CODE`, `DETAIL`, `ATTEMPT_COUNT`, `TRACE_ID`, `NACKED_BY` (these match the `DlqFailureMetadata`
  record properties).
- Causes: `HANDLER_RESULT_FAILURE`, `HANDLER_EXCEPTION`, `DECRYPT_FAILURE`, `DESERIALIZE_FAILURE`, `RETRIES_EXHAUSTED`.

## Cross-language parity

The same spec drives `@dcsv-io/d2-messaging-abstractions` (`dlq-failure-metadata.g.ts`, sources committed). That package
exposes the same field-name and cause-string catalogs, so a TypeScript consumer, such as DLQ operations tooling or a
RabbitMQ subscriber, reads the same identifiers as the .NET producers.

## Diagnostics

| ID         | Title                                | Severity |
| ---------- | ------------------------------------ | -------- |
| `D2DLQ001` | Spec is malformed                    | Error    |
| `D2DLQ002` | Duplicate field constName            | Error    |
| `D2DLQ003` | Duplicate field value                | Error    |
| `D2DLQ004` | Duplicate cause (constName or value) | Error    |
| `D2DLQ005` | constName has invalid shape          | Error    |
| `D2DLQ006` | Empty wire value                     | Error    |
