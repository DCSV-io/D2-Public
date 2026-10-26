<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Messaging.Abstractions

Transport-agnostic abstractions for the D2 messaging stack. Domain code
references this package to mark messages with `[MqPub(MqMessages.X)]`, mark
handlers with `[MqSub(MqSubscriptions.X)]`, and depend on `IMessageBus` /
`IMessageIdempotencyStore`. It needs neither `RabbitMQ.Client` nor any
specific transport.

`DcsvIo.D2.Messaging.RabbitMq` is the default implementation.
An alternate transport, where one exists, is a sibling csproj that
uses the same surface.

## Install

```bash
dotnet add package DcsvIo.D2.Messaging.Abstractions
```

## How publishing + subscribing work end-to-end

1. Spec files: `contracts/mq-messages/mq-messages.spec.json` and
   `contracts/mq-subscriptions/mq-subscriptions.spec.json` declare every
   publishable message type and every subscription contract. The spec is the
   source of truth: exchange, encryption, queue topology and prefetch
   are defined there, not in code.
2. Codegen: this package references the analyzer-only package
   `DcsvIo.D2.Messaging.SourceGen` as an analyzer. It emits the generated files below into this assembly
   at build time, in the tracked `Generated/` directory (committed
   for inspection, IDE navigation, and PR diff review; re-emitted on every
   `dotnet build` from the spec; do not hand-edit):
   - `MqMessages.g.cs` - `public static partial class MqMessages` with one
     string `const` per spec entry, plus `MqMessagesRegistry.ByConstant`
     (`Dictionary<string, MqMessageDescriptor>`).
   - `MqSubscriptions.g.cs` - same shape for subscriptions.
3. The catalog: a host registers one `MessagingCatalog`, which holds the message and
   subscription descriptors it publishes and consumes against.
   `MessagingCatalog.Default` wraps the generated registries above, and is
   what a host that registers none gets. An application whose entries live in
   its own spec files registers the catalog generated from those instead.
4. Producer side: the message class has `[MqPub(MqMessages.X)]`.
   `MessagingCatalog.RequireDescriptor` looks the descriptor up through the
   attribute, and the transport reads exchange, encryption and default routing
   key from it. The lookup is cached per type on the catalog instance, so two
   catalogs in one process resolve independently.
5. Consumer side: the handler class has `[MqSub(MqSubscriptions.X)]`.
   `services.AddD2SubscribersFromAssembly(catalog, typeof(MyHandler).Assembly)`
   scans, validates that the handler's `BaseHandler<TSelf, TIn, Unit>` `TIn` matches
   the spec entry's `messageType`, and registers an `ISubscriberRegistration`
   that the transport's consumer host reads. The overload without a catalog
   reads `MessagingCatalog.Default`.

The runtime and operational details (per-delivery pipeline, DLQ shape, telemetry,
encryption posture, queue topology, channel lifecycle) are in
`DcsvIo.D2.Messaging.RabbitMq`, the
canonical operational reference. This package defines the transport-agnostic
contract.

## Contract-level anti-patterns

Each of these fails with an explicit error at startup or on the first call.
They are listed here so that they do not first appear during a deploy.

- Hand-registering `IMessageBus` or `ISubscriberRegistration` outside of
  `AddD2MessagingRabbitMq` / `AddD2SubscribersFromAssembly`. The codegen and
  scanner are the only supported paths.
- Using `[MqPub]` / `[MqSub]` on a class whose CLR fully qualified name does not match the
  spec entry's `messageType`. The resolver and registrar fail hard at build
  or startup. The spec-driven `[MqPub]` / `[MqSub]` attribute design prevents
  a silent mismatch.
- Putting identity (`UserId` / `OrgId` / scopes) into the
  `x-d2-context` propagated header. That header holds propagation-only
  context (request id, fingerprints, WhoIs hash). Identity is rebuilt from
  the JWT at every synchronous hop; consumer-side handlers operate without one.
  Headers stay plaintext at rest, and identity is never in headers.

  > This text duplicates the Encryption posture section of `DcsvIo.D2.Messaging.RabbitMq` so that the contract side
  > shows it at a glance. The canonical runtime-enforcement reference is in that section: update both in lockstep.

## Public surface

**`IMessageBus`** - `PublishAsync<TMessage>(message, options?, ct)` +
`WaitForReadyAsync(ct)`. The publish path resolves the type's descriptor
(throws on missing `[MqPub]` / unknown constant / FQN mismatch),
encrypts the body when the descriptor's encryption is non-`plaintext`,
attaches the canonical AMQP headers, and waits for publisher confirm
when configured. `WaitForReadyAsync` lets startup-time publishers (e.g.
key-rotation announcements from the host's lifecycle system) wait for the first connection.

**`MqPubAttribute`** - `[MqPub(MqMessages.X)]` on the message class.
Single-field attribute that holds the generated constant. Default-deny: a
class without `[MqPub]` throws `InvalidOperationException` from the
publisher's resolver, because every publishable type must have an explicit spec
entry.

**`MqSubAttribute`** - `[MqSub(MqSubscriptions.X)]` on the handler class
(must derive from `BaseHandler<TSelf, TIn, Unit>`).
`AddD2SubscribersFromAssembly` discovers it.

**`MqMessageDescriptor`** - generated record with the fields
`(Constant, MessageTypeName, Exchange, ExchangeType, Encryption,
EncryptionReason?, DefaultRoutingKey?)`. `MqMessageDescriptor.PLAINTEXT` is the sentinel
constant for the encryption field; `IsPlaintext` is a convenience predicate.

**`MqSubscriptionDescriptor`** - generated record with the fields
`(Constant, MessageTypeName, QueueName, Pattern, RoutingKeyBinding,
Prefetch, Idempotency, TieredRetry?)`.

**`MessagingCatalog`** - the message and subscription descriptors one host
publishes and consumes against, as `(Messages, Subscriptions)` dictionaries. A
host registers exactly one; the publisher, the consumer, the topology declarer
and every outbox dispatcher read that instance. `MessagingCatalog.Default` is
the catalog built from the registries in this assembly; a host that
registered none gets it. `RequireDescriptor(Type)` reads a message type's
`[MqPub]` constant, looks it up, cross-checks the entry's `messageType` against
the CLR name, and throws an exception that states the type and the constant when the
attribute is missing, the constant is unknown or the name does not match. Its
per-type resolution cache is per instance, so two catalogs never use each other's
resolved descriptors.

**`TieredRetryDescriptor`** - `(TimeSpan[] Tiers, int MaxAttempts)` for
the optional broker-level retry topology. An
`MqSubscriptionDescriptor` holds it when the spec entry has a `tieredRetry` block.

AMQP wire-protocol header constants are in
`DcsvIo.D2.Headers.Amqp` (generated
from `contracts/headers/headers.spec.json`). Cross-transport entries
(e.g. `traceparent`, `tracestate`, `x-d2-context`) have identical
wire values in `DcsvIo.D2.Headers.Common`.
Messages MUST NOT carry identity / raw PII in plaintext headers - only
routing, observability, and the small operational propagation subset
(`x-d2-context` is base64url-of-JSON of the hand-written `PropagatedContext`
record in `DcsvIo.D2.Context.Abstractions`). See
`DcsvIo.D2.Messaging.RabbitMq` for the
full runtime + wire-format contract.

**`QueuePattern`** - enum: `CompetingConsumer` / `FanoutExclusiveAutoDelete`
/ `DurableShared`. Selects the topology declared per subscriber. The transport's
host adds a per-process token to `FanoutExclusiveAutoDelete` queue names
so replicas of a service do not race on the broker's exclusive-queue
lock.

**`PublisherOptions`** - per-publish overrides (confirm wait, routing key
override, exchange override, max attempts).

**`IMessageIdempotencyStore`** - opt-in deduplication helper for subscribers. The default
implementation in the RabbitMQ package uses `IDistributedCache`. An operator can
register another implementation (e.g. an in-memory fake in tests), and the startup check
recognizes the operator-provided implementation.

**`SubscriberRegistry`** + **`ISubscriberRegistration`** - DI singleton
that collects every `AddD2Subscriber` / scanner-discovered registration.
The transport's consumer host reads it once at startup. Each registration holds
`(HandlerType, MessageType, Descriptor, ResolvedQueueName)`.

**`SubscriberRegistrar`** - internal helper used by both the assembly
scanner and the explicit programmatic `AddD2Subscriber<,>` helper. Resolves
descriptor by constant, validates handler-message pairing, applies the
per-process queue-name suffix for `FanoutExclusiveAutoDelete`, and adds
the registration to DI.

**`MessagingFailures`** - `D2Result` validation-failure helpers (mirrors
`InputFailures` in the cache abstractions).

**`MessagingJsonOptions`** - shared `JsonSerializerOptions` used to
(de)serialize the wire body and the `x-d2-failure-reason` DLQ header
payload. CamelCase, omits null fields on write, no pretty-printing.

**`DlqFailureMetadata`** - JSON shape attached to dead-lettered messages via
the `x-d2-failure-reason` header. Well-known causes:
`HANDLER_RESULT_FAILURE` / `HANDLER_EXCEPTION` / `DECRYPT_FAILURE` /
`DESERIALIZE_FAILURE` / `RETRIES_EXHAUSTED`.

### DI helpers

**`services.AddD2SubscribersFromAssembly(params Assembly[] assemblies)`** -
canonical registration path. Reflects over each assembly looking for
classes that have `[MqSub]`; for each match, validates and registers through
`SubscriberRegistrar`, looking each constant up in `MessagingCatalog.Default`.

**`services.AddD2SubscribersFromAssembly(MessagingCatalog catalog, params Assembly[] assemblies)`** -
the same scan against a supplied catalog. An application whose subscription
constants are in its own catalog passes that catalog here; a constant the
supplied catalog does not contain throws at composition, with a message that
states the handler and the constant.

**`services.AddD2Subscriber<THandler, TMessage>(MqSubscriptionDescriptor descriptor)`** -
programmatic alternative to the scanner. It helps integration tests that need
per-test queue names without adding them to the production spec. Production
code should prefer the scanner.

## Dependencies

- `DcsvIo.D2.Result` - every op returns `D2Result<T>` / `D2Result`.
- `DcsvIo.D2.I18n.Abstractions` - typed `TKMessage` for failure surfaces.
- `DcsvIo.D2.Handler` - `BaseHandler<THandler, TIn, Unit>` constraint on
  subscribers.
- `DcsvIo.D2.Encryption` - `EncryptionDomains` constants are the legal
  values for `MqMessageDescriptor.Encryption` (alongside the `"plaintext"`
  literal).
- `DcsvIo.D2.Utilities` - `Falsey()` / `Truthy()` extensions.
- Build-time analyzer ref to `DcsvIo.D2.Messaging.SourceGen` (zero runtime cost).

## References

- `DcsvIo.D2.Messaging.RabbitMq` - default RabbitMQ
  impl + canonical runtime / wire-format / header / queue / encryption /
  delivery-semantics / DLQ / startup-ordering reference.
- `DcsvIo.D2.Messaging.SourceGen` - codegen that
  emits the registries from the spec files; full spec format + diagnostic
  catalog + spec evolution rules.
