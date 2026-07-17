<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/mq-messages/`

Message-queue message registry — the closed set of RabbitMQ message type names with their exchange routing keys, encryption domain, and tiered-retry configuration.

## Consumed by

- **.NET** — [`packages/dotnet/messaging/source-gen/`](../../packages/dotnet/messaging/source-gen/README.md) (Roslyn `MqGenerator` → `MqMessages` routing constants + publisher descriptor registrations in `DcsvIo.D2.Messaging.Abstractions`)

No TypeScript package currently consumes this catalog as a source-gen input — message routing descriptors may be mirrored in `@dcsv-io/d2-messaging-abstractions` from related specs.

## See also

- All contracts: [contracts catalog](../README.md)
