<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Messaging.RabbitMq - end-to-end architecture

The path a message takes through [`DcsvIo.D2.Messaging.RabbitMq`](../README.md) from the publishing handler to the
consuming one, and the component that handles each hop.

```text
Producer service
  [MqPub(MqMessages.X)] class FooEvent
    -> IMessageBus (RabbitMqMessageBus) -> publish via channel pool -> RabbitMQ broker

RabbitMQ broker
  exchange: descriptor.Exchange
    -> queue: descriptor.QueueName
        -> DLX -> DLQ
        -> (optional) tier exchanges + queues

Consumer service
  RabbitMQ broker
    -> dedicated channel (BasicConsume) -> SubscriberChannel -> dispatch
    -> [MqSub(...)] class MyHandler : BaseHandler<...>
```

From spec to runtime (spec -> codegen -> registry -> runtime):

- Two JSON spec files in `contracts/`:
  - `mq-messages/mq-messages.spec.json` - every publishable message type
  - `mq-subscriptions/mq-subscriptions.spec.json` - every subscription contract
- The `DcsvIo.D2.Messaging.SourceGen` Roslyn analyzer reads both at build time
  and emits constants + immutable runtime registries into the
  `DcsvIo.D2.Messaging.Abstractions` assembly:
  - `MqMessages.X` (string constant) + `MqMessagesRegistry.ByConstant`
    (`Dictionary<string, MqMessageDescriptor>`)
  - `MqSubscriptions.X` (string constant) + `MqSubscriptionsRegistry.ByConstant`
- Both catalogs ship empty. An application builds a `MessagingCatalog` from its
  own message and subscription descriptor dictionaries and registers it with the
  host.
- A host registers one `MessagingCatalog` - the message and subscription
  descriptors the publisher, the consumer, the topology declarer and any outbox
  dispatcher all read.
  `MessagingCatalog.Default` wraps the registries in this package's abstractions
  and is the catalog a host that registers none uses.
- The producer marks each message class `[MqPub(...)]`; the publisher resolves
  `Type -> MqMessageDescriptor` through `MessagingCatalog.RequireDescriptor` and
  gets exchange / encryption / routing-key from the descriptor.
- The consumer marks each handler class `[MqSub(...)]`;
  `AddD2SubscribersFromAssembly(catalog, assembly)` scans, validates the
  handler's `BaseHandler<TSelf, TIn, Unit>` generic argument matches the spec's
  `messageType`, and registers an `ISubscriberRegistration`.

The full spec format and the diagnostic catalog of the source generator are documented in
`DcsvIo.D2.Messaging.SourceGen`. The
transport-agnostic public surface (`IMessageBus`, `[MqPub]`, `[MqSub]`,
descriptor records) is defined in
`DcsvIo.D2.Messaging.Abstractions`.

---
