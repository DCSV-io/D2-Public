<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/mq-subscriptions/`

MQ subscription registry - the closed set of RabbitMQ queue bindings with their exchange, routing-key pattern, and subscriber handler mapping.

The `subscriptions` array is empty. This catalog is the framework mechanism: it fixes the entry shape, the diagnostics and the generated registry types. An application declares its own subscriptions in its own `mq-subscriptions.spec.json`, and the generator compiles both files together into that application's private registry.

## Consumed by

- **.NET** - [`packages/dotnet/messaging/source-gen/`](../../packages/dotnet/messaging/source-gen/README.md) (Roslyn `MqGenerator` -> `MqSubscriptions` subscription descriptor registrations in `DcsvIo.D2.Messaging.Abstractions`)

No TypeScript package consumes this catalog as a generator input.

## See also

- All contracts: [contracts catalog](../README.md)
