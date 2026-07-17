<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/mq-subscriptions/`

MQ subscription registry — the closed set of RabbitMQ queue bindings with their exchange, routing-key pattern, and subscriber handler mapping.

## Consumed by

- **.NET** — [`packages/dotnet/messaging/source-gen/`](../../packages/dotnet/messaging/source-gen/README.md) (Roslyn `MqGenerator` → `MqSubscriptions` subscription descriptor registrations in `DcsvIo.D2.Messaging.Abstractions`)

No TypeScript package currently consumes this catalog — subscription binding is primarily a .NET-side messaging concern.

## See also

- All contracts: [contracts catalog](../README.md)
