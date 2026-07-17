<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/dlq-failure-metadata/`

Dead-letter queue failure metadata catalog — the AMQP header field names and structured cause codes written to DLQ entries by the RabbitMQ subscriber on message failure.

## Consumed by

- **.NET** — [`packages/dotnet/messaging/dlq-failure-metadata-source-gen/`](../../packages/dotnet/messaging/dlq-failure-metadata-source-gen/README.md) (Roslyn source-gen → `DlqFailureMetadataFields` + `DlqFailureCauses` constants in `DcsvIo.D2.Messaging.Abstractions`)
- **TypeScript** — constants/types in `@dcsv-io/d2-messaging-abstractions` (generated from this spec; sources committed)

## See also

- All contracts: [contracts catalog](../README.md)
