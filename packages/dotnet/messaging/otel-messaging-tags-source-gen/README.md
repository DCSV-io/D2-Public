<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.OtelMessagingTags.SourceGen

> Parent: [`packages/dotnet/`](../../README.md)

**Input contract:** [`contracts/otel-messaging-tags/`](../../../../contracts/otel-messaging-tags/README.md)

Roslyn incremental source generator that emits `MessagingActivityTags` - the closed catalog of OTel semantic-convention
messaging activity-tag attribute names - from `contracts/otel-messaging-tags/otel-messaging-tags.spec.json`.

> Placement: this generator is in `messaging/`, next to its consumer, and not under `telemetry/`, although the catalog is
> an observability concern. Its only consumer is
> [`DcsvIo.D2.Messaging.RabbitMq`](../rabbitmq/README.md), which references `MessagingActivityTags.*` on every
> publisher / consumer span. The README of [`telemetry/`](../../telemetry/README.md) links back here. The same spec also
> drives the TS-side telemetry tags - see [`telemetry/core/README.md`](../../telemetry/core/README.md).

Convention: spec-driven Roslyn `IIncrementalGenerator` (netstandard2.0 analyzer; spec via `<AdditionalFiles>`).

## Why spec-drive this catalog

The publisher (`RabbitMqMessageBus`) and the consumer (`SubscriberChannel`) both set the operation attribute through
`MessagingActivityTags.MESSAGING_OPERATION_TYPE`, which holds `messaging.operation.type`, the name the OTel messaging
conventions define. Both sides name the attribute through one constant, so a dashboard, collector filter or alert that
segments by operation reads the same attribute name on either side.

## What this emits

When the consuming assembly is `DcsvIo.D2.Messaging.RabbitMq`, the generator emits `MessagingActivityTags.g.cs`
containing constants for every entry in the spec.

## Cross-language parity

The same spec drives `@dcsv-io/d2-telemetry` -> `otel-messaging-tags.g.ts` (sources committed). It emits a
`MessagingActivityTags` catalog on the TS side with the same constants and the same OTel semantic-convention attribute
names, because both catalogs are generated from this one spec.

## Diagnostics

| ID         | Title                                 | Severity |
| ---------- | ------------------------------------- | -------- |
| `D2OMT001` | OTel messaging tags spec is malformed | Error    |
| `D2OMT002` | Duplicate constName                   | Error    |
| `D2OMT003` | Duplicate wire value                  | Error    |
| `D2OMT004` | constName has invalid shape           | Error    |
| `D2OMT005` | Empty wire value                      | Error    |
