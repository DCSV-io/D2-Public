<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/mq-messages/`

Message-queue message registry - the closed set of RabbitMQ message type names with their exchange routing keys, encryption domain, and tiered-retry configuration.

The `messages` array is empty. This catalog is the framework mechanism: it fixes the entry shape, the diagnostics and the generated registry types. An application declares its own message types in its own `mq-messages.spec.json`, and the generator compiles both files together into that application's private registry.

## Consumed by

- **.NET** - [`packages/dotnet/messaging/source-gen/`](../../packages/dotnet/messaging/source-gen/README.md) (Roslyn `MqGenerator` -> `MqMessages` routing constants + publisher descriptor registrations in `DcsvIo.D2.Messaging.Abstractions`)
- **TypeScript** - `MqMessages`, `MqMessagesRegistry` and `MqMessagesCatalog` in `@dcsv-io/d2-messaging-abstractions`
  (defined in `packages/typescript/messaging-abstractions/src/mq-messages.g.ts`) (generated from this spec; sources
  committed)

## See also

- All contracts: [contracts catalog](../README.md)
