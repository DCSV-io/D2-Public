<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Messaging.RabbitMq

Default implementation of the `DcsvIo.D2.Messaging.Abstractions` contract on `RabbitMQ.Client 7.x`. It manages the
connection lifecycle and channel pooling with idle eviction. It declares the topology: exchanges, a dead-letter
exchange (DLX), a dead-letter queue (DLQ) and optional retry tiers. It publishes with publisher confirms and built-in
transient retry, encrypts payloads with `DcsvIo.D2.Encryption`, propagates the full W3C trace context, republishes
failed messages to the DLQ with a failure header, and propagates the cross-hop operational context (`x-d2-context`).

DLQ operations use `RabbitMqSubscriberOptions.DlqRepublishTimeout` (default 5 seconds)
from admission through confirmation, with a separate `DlqCleanupTimeout` (default 1 second).
Each subscription has its own lazily created dedicated DLQ connection. Timeout or shutdown terminates
that connection's sockets without closing the shared consumer/publisher connection.
Negative acknowledgment recovery can produce a duplicate when the broker already received
the republished copy. `RabbitMqPublisherOptions.ConfirmTimeout` controls ordinary publishing only.

The wire body is the serialized message, with no envelope wrapper. The
descriptor (`MqMessageDescriptor`, codegen-emitted from
`contracts/mq-messages/mq-messages.spec.json`) determines the encryption,
the exchange and the default routing key.

---

## Install

```bash
dotnet add package DcsvIo.D2.Messaging.RabbitMq
```

## Table of contents

- [Cross-hop propagation](#cross-hop-propagation)
- [Quick start](#quick-start)
- [End-to-end architecture](#end-to-end-architecture)
- [Public surface](#public-surface)
- [Wire format](#wire-format)
- [Publisher and consumer paths](#publisher-and-consumer-paths)
- [Architecture](#architecture)
- [Operational anti-patterns](#operational-anti-patterns)
- [Dependencies](#dependencies)
- [TypeScript twin - producer and consumer](#typescript-twin---producer-and-consumer)
- [References](#references)

---

## Cross-hop propagation

- Trace correlation: the full W3C `traceparent` (`00-{traceId}-{spanId}-{flags}`) and the optional `tracestate`
  travel as AMQP headers. The consumer parses them with `ActivityContext.TryParse` and starts a `Consumer`-kind
  span whose parent is the publish span, so any OpenTelemetry (OTel) backend can assemble the trace across hops.
- Operational subset: `RequestId`, `RequestPath`, the fingerprints, `WhoIsHashId` and the accumulated service call
  path (`CallPath`) are carried in the `x-d2-context` header (the `PropagatedContext` encoded as base64url of
  JSON; the shape is the same on every transport, and AMQP is not special-cased). The call path is also sent on
  every synchronous gRPC hop: a dedicated outbound client interceptor writes it, and an inbound establishment
  interceptor on the host auth stack establishes it.
  `PropagatedContextSerializer.TryDecode` enforces both a wire-level cap
  (`MAX_HEADER_LENGTH = 2 KiB`) and per-field length caps (RequestPath <=
  2048, RequestId <= 256, fingerprints <= 512, WhoIsHashId <= 128; `CallPath`
  entry count <= its spec `maxLength`). A forged header that fits under the wire cap but contains one oversized
  field is dropped wholesale. Propagation is optional, so `TryDecode` returns a null context for such a header
  and never a partial or sanitized one.
- Identity (UserId, OrgId, Scopes, ActorChain): messaging does not propagate it. Each synchronous hop
  re-validates a JWT and rebuilds identity from it. For an asynchronous event the consumer-side handler has no
  JWT and should not claim the caller's identity. Business identity that the consumer needs goes in the typed
  message body.

The public surface is small, and almost every type is internal.
A consumer registers the stack with one DI call (`AddD2MessagingRabbitMq`),
publishes through `IMessageBus`, and registers subscribers with
`AddD2SubscribersFromAssembly` (in the abstractions package). The hosted services in this package
read those registrations and declare the topology.

---

## Quick start

```csharp
services
    .AddD2EncryptionFor(EncryptionDomains.AUDIT, factory: ...)
    .AddD2MessagingRabbitMq(
        configureConnection: o =>
        {
            o.ConnectionUri = "amqps://audit-svc:" + secrets.RabbitMqPassword
                + "@rabbitmq.internal:5671/d2";
            o.ClientProvidedName = "audit-svc";
        });

services.AddD2Handler();
services.AddD2SubscribersFromAssembly(typeof(MyConsumerAssembly).Assembly);
```

```csharp
public sealed class PublishWidgetCreated(IMessageBus bus)
{
    public ValueTask<D2Result> RunAsync(WidgetCreated evt, CancellationToken ct)
        => bus.PublishAsync(evt, ct: ct);
}
```

The `[MqPub]` attribute links the message type to its spec entry:

```csharp
[MqPub(MqMessages.WidgetCreated)]
public sealed class WidgetCreated { /* ... */ }
```

A type without `[MqPub]` throws `InvalidOperationException` from the
publisher's resolver, so every publishable type must have a spec
entry under `contracts/mq-messages/`.

A handler is marked with `[MqSub]`:

```csharp
[MqSub(MqSubscriptions.WidgetCreatedAuditing)]
public sealed class WidgetCreatedAuditingHandler
    : BaseHandler<WidgetCreatedAuditingHandler, WidgetCreated, Unit>
{
    /* ExecuteAsync */
}
```

---

## End-to-end architecture

The path a message takes from the publishing handler to the consuming one, and the component that owns each hop,
is in `architecture.md`, in this package's documentation folder in the repository.

## Public surface

`MessagingRabbitMqServiceCollectionExtensions.AddD2MessagingRabbitMq(...)`
registers the connection, the channel pool, the bus, the topology declarer, the idempotency
startup check, and the hosted services that run them. It is idempotent. It validates at
composition time (`ValidateOnStart`) that `WaitForConfirm == true` implies `PublisherConfirmsEnabled == true`.
A mismatch is a startup failure, not a silent fire-and-forget publish that the operator
believed was confirmed. It also validates the broker address at composition: an address whose
scheme is not `amqps` is refused unless its host is loopback or
`RabbitMqConnectionOptions.AllowUnencryptedTransport` is `true`. The address
carries the broker credential and every message body crosses the connection,
so a plain `amqp://` address to another host is a startup failure. The
dead-letter transport copies the validated options, so the rule covers it too.

`RabbitMqConnectionOptions` has `ConnectionUri` (`amqps://...`, which contains host,
port, vhost and credentials), `AllowUnencryptedTransport` (default `false`;
set it only where nothing can observe the network path to the broker, such as
a local container network), `ClientProvidedName`, consumer-dispatch
concurrency and reconnect backoff. `ConnectionUri` can be set but not read: the
address is held in a private field, the type is marked
`[RedactData(Reason = RedactReason.SecretInformation)]`, and
`RevealConnectionUri()` is the only read of it.

`ChannelPoolOptions` has the publisher pool size (default 4), the acquire
timeout (default 30s), the publisher confirms toggle (default on) and
`IdleTtl` (default 5 min). A channel idle longer than `IdleTtl` is
disposed and replaced on the next acquire, to avoid stale broker-side
state under low-traffic services.

`RabbitMqPublisherOptions` has the confirm wait toggle, the confirm timeout
(default 5s), the maximum attempts (default 5) and the retry backoff (200ms -> 5s cap).

`RabbitMqSubscriberOptions` has independent DLQ operation and cleanup bounds, configured
through `AddD2MessagingRabbitMq(configureSubscriber: ...)`. Both are `NodaTime.Duration`
values and must be positive whole milliseconds no greater than 2147483647. Configuration
validation runs at startup; subscriptions capture the values before allocating transport.

`MessagingErrorCodes` and `MessagingTransportFailures` are generated from
`contracts/messaging-error-codes/messaging-error-codes.spec.json`. The catalog lists the codes a publish can fail with
when the transport could not carry the message: `LIB_MESSAGING_PUBLISH_UNROUTABLE`, described under "Routing is not
optional" in `publisher-and-consumer.md`, in this package's documentation folder in the repository.

`IMessageBus` is registered to `RabbitMqMessageBus`, which is
internal and not part of the public surface. The bus is a singleton.
It builds a transient DI scope on each `PublishAsync` call to resolve the keyed
`IPayloadCrypto` and the calling scope's `IRequestContext` snapshot, so
hosted services and other singletons can publish.

---

## Wire format

The envelope, its fields and the AMQP header contract another runtime has to honor are in
`wire-format.md`, in this package's documentation folder in the repository.

## Publisher and consumer paths

The publisher path and its routing obligation, the consumer path and its acknowledgment rules, the dead-letter
convention, the optional retry tier, the encryption posture and the defaults that apply with no configuration are
in `publisher-and-consumer.md`, in this package's documentation folder in the repository.

## Architecture

| Concern                    | Owner                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Connection lifecycle       | `Connection/RabbitMqConnection.cs` - singleton wrapper over `IConnection`; opens lazily; `RabbitMQ.Client` automatic recovery handles reconnects. Host stays up while broker is down (publishers return `ServiceUnavailable`; consumers idle).                                                                                                                                                                                                    |
| Connection startup         | `Connection/ConnectionStartupHostedService.cs` - non-blocking; starts the connection's reconnect loop.                                                                                                                                                                                                                                                                                                                                            |
| Channel pool               | `Channels/BoundedChannelPool.cs` - semaphore-bounded; recycles healthy channels on lease return; discards faulted channels; evicts channels idle longer than `IdleTtl`.                                                                                                                                                                                                                                                                           |
| Body composition           | `Encryption/EncryptedBodyComposer.cs` - JSON-serializes the typed message; encrypts via `IPayloadCrypto[domain]` when the descriptor's `Encryption` is non-`plaintext`. Validates encryption-frame version byte on decrypt.                                                                                                                                                                                                                       |
| Wire resolution            | `Encryption/MessageWireResolver.cs` - delegates `Type -> MqMessageDescriptor` to `MessagingCatalog.RequireDescriptor`, which reads the type's `[MqPub]` constant and caches per type on the catalog instance. Throws on a missing attribute, a constant the catalog does not contain or a fully qualified name (FQN) mismatch. A test that needs a fixture descriptor builds its own `MessagingCatalog` and passes it through the ordinary seams. |
| Publishing                 | `Publishing/RabbitMqMessageBus.cs` - `IMessageBus` implementation; uses the body composer, the channel pool, the retry helper and the transient classifier; emits OTel spans and metrics; `WaitForReadyAsync` for startup-time publishers.                                                                                                                                                                                                        |
| Transient classification   | `Publishing/TransientPublishClassifier.cs` - decides which failures are retried. Includes `PublishException` (when not a return-publish), `BrokerUnavailable`, `BrokerUnreachable`, `OperationInterrupted`, `AlreadyClosed`, `TimeoutException`, plus the standard transients.                                                                                                                                                                    |
| Topology                   | `Topology/DefaultTopologyDeclarer.cs` - idempotent declaration of exchanges + queues + DLX + DLQ + optional retry tiers (driven by `SubscriberRegistry`).                                                                                                                                                                                                                                                                                         |
| Topology startup           | `Topology/TopologyHostedService.cs` - non-blocking; awaits connection ready, then declares once. Logs `TopologyLog.DeclarationFailed` on background-task faults, so a `PRECONDITION_FAILED` is logged and does not go to `TaskScheduler.UnobservedTaskException`.                                                                                                                                                                                 |
| Consumer host              | `Subscribing/ConsumerHostedService.cs` - opens one `SubscriberChannel` per registration after awaiting the topology declaration; when `TopologyHostedService` started it first, this awaits that shared declaration instead of sending a second set of declare and bind commands. Exposes `ReadyTask` (Task) that completes when every channel has finished `BasicConsume`.                                                                       |
| Subscriber channel         | `Subscribing/SubscriberChannel.cs` - owns one consume channel + one lazy republish channel per subscription; per-delivery DI scope; trace-context parsing; tiered-retry attempt-count enforcement; idempotency pre-check; narrow-catch around `BasicAck`; in-flight callback drain on disposal.                                                                                                                                                   |
| Handler dispatch           | `Subscribing/HandlerDispatcherFactory.cs` - pre-builds typed dispatchers at startup from the registry; one dispatcher per registered queue.                                                                                                                                                                                                                                                                                                       |
| DLQ failure header         | `Subscribing/DlqFailureHeaderBuilder.cs` - JSON-encodes `DlqFailureMetadata`; contains no personally identifiable information (PII) because it drops `exception.Message`.                                                                                                                                                                                                                                                                         |
| Sanitized exception render | Consumes `DcsvIo.D2.Utilities.Diagnostics.SanitizedExceptionRender` (`TypeName` + `FirstFrame` only; never `ex.Message`). Used by every consumer-side log site that surfaces exception-derived strings (handler exceptions, ack failures, DLQ-republish failures, boundary failures).                                                                                                                                                             |
| Idempotency                | `Idempotency/CacheIdempotencyStore.cs` - `IMessageIdempotencyStore` impl backed by `IDistributedCache` with 24h TTL.                                                                                                                                                                                                                                                                                                                              |
| Idempotency startup check  | `Idempotency/IdempotencyStartupCheck.cs` - hard-fails host startup when any subscription has `idempotency: true` but no `IDistributedCache` and no operator-provided `IMessageIdempotencyStore`.                                                                                                                                                                                                                                                  |
| Telemetry                  | `Telemetry/MessagingTelemetry.cs` - static `ActivitySource` + `Meter` named `DcsvIo.D2.Messaging.RabbitMq`; counters for publishes, publish failures, publish retries, publish returns (`d2.messaging.rabbitmq.publish_returns`), ack failures and DLQ-republish failures, plus a publish-duration histogram.                                                                                                                                     |

---

## Operational anti-patterns

- Auto-ack. The pipeline always uses manual ack with `autoAck: false`. A need for
  auto-ack is a need for at-most-once delivery, which is a different problem.
- Sharing the same queue name across competing consumers in a multi-process
  layout when the spec pattern is `FanoutExclusiveAutoDelete`. Use the
  per-process suffix and do not override it.
- Putting identity (`UserId` / `OrgId` / scopes) into `x-d2-context`.
  That field carries propagation-only context (request id, fingerprints).
  Identity is rebuilt from the JWT at every hop; consumer-side handlers
  operate without one.
- Putting user input into exception messages. The DLQ failure-reason
  header drops `ex.Message` to keep user input out of it, and the logs drop it too. Do not work around this:
  put human-readable detail in the result `messages` (translation keys) instead.
- Publishing to an exchange nothing is bound to. That publish fails - see
  "Routing is not optional" in `publisher-and-consumer.md`, in this package's
  documentation folder. Bind a queue to it, even one nothing drains, or stop
  publishing to it.

> Contract-level anti-patterns are listed in `DcsvIo.D2.Messaging.Abstractions`: hand-registering `IMessageBus`
> outside the supported DI helpers, an FQN mismatch between `[MqPub]` and the spec entry, and
> `[MqSub]` without a matching handler type.

---

## Dependencies

- `DcsvIo.D2.Messaging.Abstractions` - interfaces, descriptor records,
  registry, failure helpers, attributes (`MqPub` / `MqSub`),
  `AmqpHeaders`, codegen-emitted `MqMessages.*` / `MqSubscriptions.*`.
- `DcsvIo.D2.Encryption` - `IPayloadCrypto` keyed per encryption domain;
  `EncryptionDomains` constants.
- `DcsvIo.D2.Caching.Abstractions` - `IDistributedCache` consumed by
  `CacheIdempotencyStore`.
- `DcsvIo.D2.Handler` - subscribers are `BaseHandler<TSub, TIn, Unit>`
  instances; the consumer wrapper invokes them via the standard pipeline.
- `DcsvIo.D2.Resilience` - `RetryHelper.RetryAsync` drives the publisher's
  built-in transient retry loop.
- `DcsvIo.D2.Result`, `DcsvIo.D2.Utilities` and `DcsvIo.D2.I18n.Abstractions`: the shared result types, utilities and
  translation-key abstractions.
- `DcsvIo.D2.Context.Abstractions` - for the consumer-side DI
  registration of `MutableRequestContext` / `IRequestContext` (handlers
  resolve `IRequestContext` through `HandlerContext`); the consumer
  applies the `x-d2-context`-decoded `PropagatedContext` onto the
  per-message scope's `MutableRequestContext`.
- `RabbitMQ.Client 7.x` - only this package references it; abstractions
  stay transport-free.

---

## TypeScript twin - producer and consumer

A service-agnostic Node runtime for both directions is published as the npm package
`@dcsv-io/d2-messaging-rabbitmq`. On the consume side it declares the same topology
(the primary queue, `{q}.dlx`, `{q}.dlq` and the retry tiers), consumes with manual acks,
republishes failures with the same `DlqFailureMetadata`, deduplicates through the same
5-point idempotency contract, and establishes the same per-delivery context: a consume span
whose parent is the `traceparent`, and the per-message operational context from `x-d2-context`.
It never takes identity or `RequestOrigin` from the wire. On the
publish side it fuses publishing with encryption in the same way as this package:
`createPublisher({ crypto, descriptors })` binds a compile-time type
witness, so a message can be published only when the catalog bound through
`descriptors` contains it and its domain is `plaintext` or one whose composer was
wired in. A runtime default-deny (`composeBody`) also covers the dynamic paths,
and there is no raw-bytes publish overload, so the composer for a domain is the only
path to the socket for that domain. The framework catalog it defaults to is
empty, so a host binds its own. Its Testcontainer suite replays golden
messages emitted by the .NET contract-fixture emitters and round-trips its
own published frames back through the consumer pipeline.

## References

- `DcsvIo.D2.Messaging.Abstractions` - the
  contract this package implements (transport-agnostic surface, attributes,
  descriptor records, registry, scanner, contract-level anti-patterns).
- `DcsvIo.D2.Messaging.SourceGen` - codegen
  emitting the `MqMessages` / `MqSubscriptions` registries; full spec
  format + diagnostic catalog + spec evolution rules.
- `DcsvIo.D2.Headers.Amqp` - codegen-emitted wire-value
  constants for the AMQP header catalog (`AmqpHeaders.MESSAGE_ID`, etc.).
- `DcsvIo.D2.Encryption` - encryption primitive (keyring,
  `IPayloadCrypto`, frame format, rotation lifecycle).
- `DcsvIo.D2.Encryption.Domains.SourceGen`
  - the `EncryptionDomains` catalog spec.
- `DcsvIo.D2.Encryption.Frame.SourceGen`
  - the binary frame layout byte-offset spec.
