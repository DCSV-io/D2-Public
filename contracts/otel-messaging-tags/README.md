<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/otel-messaging-tags/`

OpenTelemetry messaging tag catalog — the closed set of OTel semantic-convention attribute names written by the RabbitMQ publisher and consumer for distributed tracing of message operations.

## Consumed by

- **.NET** — [`packages/dotnet/messaging/otel-messaging-tags-source-gen/`](../../packages/dotnet/messaging/otel-messaging-tags-source-gen/README.md) (Roslyn source-gen → `MessagingActivityTags` attribute-name constants in `DcsvIo.D2.Messaging.RabbitMq`)
- **TypeScript** — constants/types in `@dcsv-io/d2-telemetry` (generated from this spec; sources committed)

## See also

- All contracts: [contracts catalog](../README.md)
