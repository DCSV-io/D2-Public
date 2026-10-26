<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Messaging.RabbitMq - publisher and consumer paths

What a publish and a delivery do end to end in [`DcsvIo.D2.Messaging.RabbitMq`](../README.md): the publisher path
and its routing requirement, the consumer path and its acknowledgment rules, the dead-letter convention (a
dead-letter exchange, DLX, and a dead-letter queue, DLQ), the optional retry tier, the encryption posture, and the
defaults that apply with no configuration.

## Contents

- [Publisher path](#publisher-path)
- [Consumer path](#consumer-path)
- [DLX + DLQ convention](#dlx--dlq-convention)
- [Optional retry tier topology](#optional-retry-tier-topology)
- [Encryption posture](#encryption-posture)
- [Defaults that apply automatically](#defaults-that-apply-automatically)

## Publisher path

### Registration

```csharp
services.AddD2MessagingRabbitMq(
    configureConnection:  o => o.ConnectionUri = "amqps://...",
    configureChannelPool: o => o.PublishPoolSize = 8,
    configurePublisher:   o => { o.WaitForConfirm = true; o.MaxAttempts = 5; });

// Encryption (only for encrypted-domain messages):
services.AddD2EncryptionFor(EncryptionDomains.Audit, _ => new PayloadCryptoKeyring(...));
```

`AddD2MessagingRabbitMq`:

- Registers `ID2Connection`, `IChannelPool`, `IMessageBus` as singletons.
  The bus builds a transient DI scope on each `PublishAsync` to resolve the keyed
  `IPayloadCrypto` and the calling scope's `IRequestContext` snapshot,
  so background hosted services can publish.
- Validates that `RabbitMqPublisherOptions.WaitForConfirm == true` implies
  `ChannelPoolOptions.PublisherConfirmsEnabled == true` at composition time
  (`ValidateOnStart`). A mismatch is a startup failure and does not become a silent
  fire-and-forget publish.
- Validates at composition that `RabbitMqConnectionOptions.ConnectionUri`
  uses the `amqps` scheme, unless its host is loopback or
  `RabbitMqConnectionOptions.AllowUnencryptedTransport` is `true`. A plain
  `amqp://` address to another host is a startup failure.

### Calling the bus

```csharp
public sealed class KeyRotatedAnnouncer(IMessageBus bus) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await bus.WaitForReadyAsync(ct);     // see WaitForReadyAsync below
        var result = await bus.PublishAsync(new KeyRotatedEvent { ... }, ct: ct);
        if (result.Failed) { /* log + decide */ }
    }
}
```

### Resolution + composition pipeline

For each `PublishAsync<TMessage>`:

1. `MessageWireResolver.Resolve(catalog, typeof(TMessage))` looks up the
   descriptor via the type's `[MqPub]` attribute against the registered
   `MessagingCatalog`. Throws on a missing attribute, a constant the catalog
   does not contain, or a fully qualified name (FQN) mismatch. These are programmer errors, not runtime
   conditions.
2. `EncryptedBodyComposer.Compose(message, descriptor, sp)` produces the body
   bytes (plaintext JSON or encrypted frame) once. Body bytes are reused
   across retry attempts, so a retry does not re-encrypt with a freshly generated
   nonce.
3. A stable per-publish `message-id` (UUIDv7) is generated once.
4. `BuildPropagatedHeader` snapshots the calling scope's `IRequestContext`
   into `x-d2-context` (or null if no context registered).
5. `RetryHelper.RetryAsync` runs the publish with exponential backoff. Transient
   classification (`TransientPublishClassifier`) treats broker-NACK /
   `OperationInterrupted` / `BrokerUnreachable` / `BrokerUnavailable` /
   `AlreadyClosed` / `TimeoutException` / `PublishException` (including a
   `basic.return`) / standard transients as retryable; every other failure is returned
   as `D2Result.ServiceUnavailable`.
6. Each attempt acquires a channel from `BoundedChannelPool.AcquireAsync`. The
   pool evicts channels idle longer than `IdleTtl` (default 5 min) to avoid
   stale broker-side state under low-traffic services.
7. With confirms enabled, `BasicPublishAsync` returns when the broker acks. A
   bounded `ConfirmTimeout`, implemented with a linked `CancellationTokenSource`, turns a hung broker into a
   `TimeoutException`, which is transient.
8. The publish goes out `mandatory`, so a message that matches no queue is
   returned rather than dropped. See [Routing is not optional](#routing-is-not-optional).

### Telemetry

| Metric                                         | Type           | Description                                                                                                                                                                                             |
| ---------------------------------------------- | -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `d2.messaging.rabbitmq.publishes`              | Counter        | Total publish attempts (including retries).                                                                                                                                                             |
| `d2.messaging.rabbitmq.publish_failures`       | Counter        | Publishes that fail after the retry loop, retried or not. Tagged `outcome`: `publish_fail_unroutable` or `publish_fail_transport`.                                                                      |
| `d2.messaging.rabbitmq.publish_retries`        | Counter        | Publish retry attempts (transient -> backoff -> re-attempt).                                                                                                                                            |
| `d2.messaging.rabbitmq.publish_returns`        | Counter        | Publish attempts the broker returned because they matched no queue.                                                                                                                                     |
| `d2.messaging.rabbitmq.publish_duration`       | Histogram (ms) | Wall-clock duration of a publish operation, end-to-end.                                                                                                                                                 |
| `d2.messaging.rabbitmq.dlq_republish_failures` | Counter        | Consumer-side DLQ republish failures - falls back to `BasicNack-no-requeue` so the message still leaves the primary queue. Tagged `outcome`: `republish_fail_unroutable` or `republish_fail_transport`. |
| `d2.messaging.rabbitmq.ack_failures`           | Counter        | Post-handler-success ack failures (narrow catch around `BasicAckAsync`) - the idempotency mark prevents duplicate work on broker redelivery.                                                            |

A `publish {exchange}/{routingKey}` Producer-kind activity wraps the whole
call. Its tags are defined in
`contracts/otel-messaging-tags/otel-messaging-tags.spec.json`, which
`DcsvIo.D2.OtelMessagingTags.SourceGen` turns into `MessagingActivityTags`. The publisher and
the consumer both use it. The publisher emits:

- `MessagingActivityTags.MESSAGING_SYSTEM` (`messaging.system` = `"rabbitmq"`)
- `MessagingActivityTags.MESSAGING_DESTINATION_NAME`
  (`messaging.destination.name` = exchange)
- `MessagingActivityTags.MESSAGING_RABBITMQ_ROUTING_KEY`
  (`messaging.rabbitmq.routing_key`)
- `MessagingActivityTags.D2_MESSAGE_TYPE` (`d2.message_type` = CLR FullName)
- `MessagingActivityTags.D2_ENCRYPTION_KID` (`d2.encryption_kid`; null/absent
  for plaintext)
- `MessagingActivityTags.MESSAGING_MESSAGE_ID` (`messaging.message.id` =
  UUIDv7)
- `MessagingActivityTags.MESSAGING_OPERATION_TYPE`
  (`messaging.operation.type` = `"publish"`)

The OpenTelemetry semantic-convention attribute is `messaging.operation.type`, not
`messaging.operation`. Generating the catalog from one spec prevents the
publisher and the consumer from using different names.

### `WaitForReadyAsync`

`IMessageBus.WaitForReadyAsync(CancellationToken)` awaits
`ID2Connection.ReadyTask`, which completes when the first connection is established. Use it from
background hosted services that publish at startup, so a race with the broker at startup
does not produce a `ServiceUnavailable` on the first call.

### Routing is not optional

A publish that reaches no queue is a failure. Every publish goes out with the AMQP
`mandatory` flag set, so the broker returns a message it cannot route instead of dropping it
and confirming it. With publisher-confirmation tracking on - which the channel pool enables
alongside confirms - the client matches the `basic.return` back to the publish that caused it
by publish sequence number, so the return is raised inside the confirm await and that attempt
has failed.

A returned attempt is classified transient and retried on the normal backoff. That is what
makes the cross-host bind race self-healing: two hosts declaring the same binding at once can
get `bind-ok` back before the exchange index the router reads has been written, and a publish
in that window matches nothing. The window closes on its own, and the next attempt routes to the queue.

When every attempt is returned, `PublishAsync` fails with
`MessagingErrorCodes.LIB_MESSAGING_PUBLISH_UNROUTABLE` - a 503 in the
`infrastructure_unavailable` class, distinct from the `GEN_SERVICE_UNAVAILABLE` an unreachable
broker produces, because each condition needs a different response. An error log records the exchange and
the routing key, and `d2.messaging.rabbitmq.publish_failures` carries
`outcome=publish_fail_unroutable`.

Publishing to an exchange with no consumer therefore fails, by design. If a service
announces something nothing subscribes to yet, declare a queue bound to that exchange - even one
nothing drains - or stop publishing to it. A message that reaches no queue is the condition this
behavior exists to report. Treating it as success leaves a missing consumer unnoticed
until someone looks for data that was never sent.

Where `ChannelPoolOptions.PublisherConfirmsEnabled` is off there is no sequence number to match the return to, so the
publish has already completed and cannot be failed. The pool attaches a `basic.return` listener in that configuration so
the return is at least counted and logged; the message is still gone.

---

## Consumer path

### Registration

```csharp
services.AddD2Handler();
services.AddD2MessagingRabbitMq(...);
services.AddD2SubscribersFromAssembly(typeof(MyHandler).Assembly);
```

`AddD2SubscribersFromAssembly` reflects over the assembly looking for classes
with `[MqSub]`; for each match, `SubscriberRegistrar.Register`:

1. Looks up the descriptor in the supplied catalog's subscriptions. A missing
   constant throws.
2. Walks the handler's inheritance chain looking for
   `BaseHandler<TSelf, TInput, TOutput>` and validates
   `TInput.FullName == descriptor.MessageTypeName`. A mismatch throws.
3. Calls `ResolveQueueName(descriptor)` which appends a per-process 8-char
   Guid suffix when `descriptor.Pattern == FanoutExclusiveAutoDelete`, so
   replicas of a service do not contend for the broker's exclusive-queue lock.
4. Registers the handler `Transient`, registers an `ISubscriberRegistration`
   containing `(HandlerType, MessageType, Descriptor, ResolvedQueueName)`.

### Hosted-service ordering

| Order | Hosted service                   | Responsibility                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| ----- | -------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1     | `ConnectionStartupHostedService` | Starts the background reconnect loop.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| 2     | `IdempotencyStartupCheck`        | Fails startup when any subscription declares `idempotency=true` but no `IDistributedCache` is registered and no operator-provided `IMessageIdempotencyStore` is registered.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| 3     | `TopologyHostedService`          | Declares the topology in the background once `ID2Connection.ReadyTask` completes. A faulted declaration is logged via `TopologyLog.DeclarationFailed`, so a `PRECONDITION_FAILED` is not lost silently.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| 4     | `ConsumerHostedService`          | Awaits `ReadyTask`, then awaits `ITopologyDeclarer.DeclareAsync` before any `BasicConsume`. `DefaultTopologyDeclarer` declares once per instance: the first caller sends the declare and bind commands, and a second caller (this service or `TopologyHostedService`) awaits that same declaration instead of sending a second set. Then opens one `SubscriberChannel` per registration. For `FanoutExclusiveAutoDelete`, each `SubscriberChannel.StartAsync` re-declares the exclusive queue on the long-lived consumer channel (declare DLX + DLQ + primary queue via the same `DefaultTopologyDeclarer.QueueFlagsFor` flags, bind, then `BasicConsume`) so consume does not race a short-lived topology declare channel; exclusive queues are deleted when their declaring connection closes, not merely when a declare channel is disposed. Exposes `ReadyTask` (Task) that completes when every channel has finished `BasicConsume` - integration tests + ordered-startup callers can wait on it before publishing. |

### Per-delivery pipeline

For each delivery on a subscriber channel:

1. In-flight callback counter: `Interlocked.Increment` so `DisposeAsync`
   can drain in-flight handlers (bounded 30s) before closing the channel
   mid-ack.
2. Trace context: parse `traceparent` / `tracestate` headers via
   `ActivityContext.TryParse`; start a `Consumer`-kind activity
   `receive {queue}` with that as the parent context. Consumer-side activity
   tags (all spec-driven via `MessagingActivityTags`):
   - `MESSAGING_SYSTEM` (`messaging.system` = `"rabbitmq"`)
   - `MESSAGING_DESTINATION_NAME` (`messaging.destination.name` = queue)
   - `MESSAGING_OPERATION_TYPE` (`messaging.operation.type` = `"receive"`)
   - `MESSAGING_MESSAGE_ID` (`messaging.message.id` = producer-assigned
     UUIDv7)
   - `MESSAGING_RABBITMQ_DELIVERY_TAG` (`messaging.rabbitmq.delivery_tag`)
   - `MESSAGING_RABBITMQ_REDELIVERED` (`messaging.rabbitmq.redelivered`)
3. Per-message DI scope: `IServiceScopeFactory.CreateAsyncScope`. The
   scope owns the handler instance and a fresh `MutableRequestContext`.
4. Propagated context: read `x-d2-context`, decode it with
   `PropagatedContextSerializer`, and apply it to the scope's
   `MutableRequestContext`. Identity (UserId / OrgId / Scopes) is never in
   this header. A synchronous hop rebuilds identity from a JWT; for an asynchronous event
   the consumer-side handler has no JWT and should not claim the caller's
   identity.
5. Idempotency pre-check (when `descriptor.Idempotency`): `IMessageIdempotencyStore.HasSeenAsync(messageId)`. A hit
   sends `BasicAck` and returns without invoking the handler. ServiceUnavailable on the read path is fail-open: the
   message is processed, because a duplicate is preferable to a rejection during a short Redis outage, and handlers MUST
   be at-least-once-safe. The write path (after handler success) is different: a failed `MarkSeenAsync` would silently
   leave the dedup window unguarded for that message-id, so the consumer NACKs without requeue, which routes the message
   to the DLQ through `x-dead-letter-exchange`. That DLQ copy has no failure-metadata header and no `RETRIES_EXHAUSTED`
   cause, unlike a handler failure, which the consumer republishes to the DLQ with the header. The consumer also emits
   the `IdempotencyMarkFailed` log and the `ack_failures` counter so the operator sees the store-degradation impact.
6. Tiered-retry attempt-count check (when `descriptor.TieredRetry` is
   non-null): parse the `x-death` header, sum `count` across entries whose
   `reason` is `expired` (retry-tier TTL expiry, this package's retry path) or
   `rejected` (consumer NACK). Other reasons (`maxlen`, `delivery_limit`) are
   broker-side flow control, not consumer-side retries; counting them would
   trigger `RETRIES_EXHAUSTED` prematurely. If the filtered total >=
   `MaxAttempts`, route direct to DLQ with cause `RETRIES_EXHAUSTED` (no
   handler invocation). Without this, a permanently broken payload would
   cycle through the tier queues indefinitely.
7. Dispatch:
   `HandlerDispatcherFactory.GetForQueue(queue).DispatchAsync(scope.ServiceProvider, ea.Body, ct)`.
8. Result branch:
   - `MessageBodyDecodeException` -> DLQ with cause `DESERIALIZE_FAILURE` or
     `DECRYPT_FAILURE`.
   - Other handler exception -> DLQ with cause `HANDLER_EXCEPTION`.
     (BaseHandler's universal try/catch usually catches this and converts it to
     `D2Result.UnhandledException`, which takes the result-failure branch below.)
   - `result.Failed` -> DLQ with cause `HANDLER_RESULT_FAILURE`, errorCode =
     `result.ErrorCode`.
   - Success -> idempotency mark (if enabled), then `BasicAck`. Ack failures
     are caught in a narrow catch around the `BasicAckAsync` call only - they emit
     a structured log + `d2.messaging.rabbitmq.ack_failures` counter and rely
     on broker redelivery (the idempotency mark prevents duplicate work).
     Without the narrow catch, an ack-after-success failure would falsely
     route the already-processed message to DLQ.

### Idempotency contract (when `idempotency: true` in the spec)

1. The consumer pre-checks
   `IMessageIdempotencyStore.HasSeenAsync(messageId)` before invoking the
   handler. Hit -> ack-and-skip.
2. After a successful handler run, the consumer writes the mark via
   `MarkSeenAsync(messageId)` BEFORE `BasicAck`.
3. **Read-path** failure (HasSeen returns ServiceUnavailable) -> **fail-open**:
   process the message anyway. Handlers MUST be at-least-once-safe. The package accepts a
   possible duplicate rather than rejecting messages during a transient store outage on the read path.
4. **Write-path** failure (MarkSeen returns ServiceUnavailable) -> **NACK to
   DLQ**. Acking without a written mark would silently leave the dedup window
   unguarded; a redelivery of the already-processed message would re-run the
   handler. The `ack_failures` counter and the `IdempotencyMarkFailed` log report
   the period in which the store is degraded, so the operator can react.
5. Startup-check: `IdempotencyStartupCheck` hard-fails host startup when any
   subscription declares `idempotency: true` but neither `IDistributedCache`
   nor an operator-provided `IMessageIdempotencyStore` is registered. Without the check, the feature
   would do nothing and nothing would report it.

### Delivery semantics

- At-least-once is the default and only contract. Handlers MUST be
  idempotent. The opt-in `IMessageIdempotencyStore` (subscription-level
  `idempotency: true`) provides a 24-hour dedup window backed by
  `IDistributedCache`, but the handler's own state machine remains the authoritative record of whether a message was
  processed.
- `message-id` is UUIDv7, generated once per `PublishAsync` call so a
  publisher retry of an unconfirmed publish doesn't bypass the consumer's
  dedup window.
- Manual ack only. `autoAck: false` everywhere. Ack happens after the
  handler's work commits + the idempotency mark is written, not before.
- A crash between the mark and the ack is safe: the next redelivery sees the mark and
  skips the handler. The opposite ordering would let a
  handler-completed-but-pre-mark crash cause a duplicate run, which the
  redelivery could not detect.

### Queue patterns

| Pattern                     | When                                                                                            | Durability                                                                                                                                                                                  |
| --------------------------- | ----------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CompetingConsumer`         | commands / requests delivered to one consumer in the fleet                                      | durable, non-exclusive, non-autodelete                                                                                                                                                      |
| `DurableShared`             | persistent events (audit, file lifecycle) - survives restart, multiple consumers OK             | durable, non-exclusive, non-autodelete                                                                                                                                                      |
| `FanoutExclusiveAutoDelete` | per-instance broadcast (cache invalidation, keyring refresh) - every replica gets every message | non-durable, exclusive, auto-delete; consumer host auto-suffixes the queue name with a per-process token to avoid the broker's exclusive-queue lock collision in a multi-replica deployment |

### DLQ republish-with-failure-header

`PublishFailureHeaderAsync` republishes the original body to `{queue}.dlx`
with the failure-reason header attached, then `BasicAck`s the original
delivery on its original consumer channel. Each `SubscriberChannel` owns a lazy dedicated
DLQ connection and republish channel, separate from the shared connection. That channel
carries confirms and confirmation tracking, and the republish goes out
`mandatory`, so the original delivery is acked only after the broker has taken
the copy, so a dead-letter exchange with nothing bound to it makes the republish fail instead of
accepting and discarding the message. On republish failure: log +
`d2.messaging.rabbitmq.dlq_republish_failures` counter, tagged
`outcome=republish_fail_unroutable` when the dead-letter exchange routed
nowhere and `outcome=republish_fail_transport` otherwise, then fall back to
`BasicNack-no-requeue` (broker's `x-dead-letter-exchange` argument routes a
header-less copy - better than losing the message). The fallback cannot recover
an unroutable republish, because the broker's own dead-lettering targets the
same exchange. The tag distinguishes a topology fault from a transport one.

`RabbitMqSubscriberOptions.DlqRepublishTimeout` covers admission, connection/channel
acquisition and confirmed publication under one deadline, defaulting to 5 seconds.
`DlqCleanupTimeout` bounds the additional wait after forced termination, defaulting to
1 second. Both use `NodaTime.Duration` and accept positive whole milliseconds through
2147483647. DLQ operations are bounded by `DlqRepublishTimeout` and `DlqCleanupTimeout`
only, and `RabbitMqPublisherOptions.ConfirmTimeout` does not apply to them.

Timeout or shutdown closes the dedicated raw sockets before recovery. The republisher
retains acquisition/publication tasks and disposes late results without publishing or
acknowledging them. Pending cleanup prevents replacement transport allocation. Ordinary
consumer and publisher connections remain open. A failed operation attempts negative
acknowledgment without requeue only on its original open consumer channel; a closed
consumer channel leaves the delivery unsettled for broker recovery. A timed-out publication
may already have reached the broker, so the fallback can produce a duplicate in the DLQ.

### `DlqFailureMetadata` (the `x-d2-failure-reason` payload)

```jsonc
{
  "cause":        "HANDLER_EXCEPTION" | "HANDLER_RESULT_FAILURE" | "DECRYPT_FAILURE" | "DESERIALIZE_FAILURE" | "RETRIES_EXHAUSTED",
  "errorCode":    "<exception type FullName | result.ErrorCode>",
  "detail":       null | "<message-keys-joined>",     // see PII-safety below
  "attemptCount": 0,                                  // observed redelivery count
  "traceId":      "<W3C trace id, hex>",
  "nackedBy":     "<service name, optional>"
}
```

The wire shape is spec-driven via `contracts/dlq-failure-metadata/dlq-failure-metadata.spec.json`. The
`DcsvIo.D2.Messaging.DlqMetadata.SourceGen` multi-target source-gen emits `DlqFailureMetadataFields` (property names)
into `DcsvIo.D2.Messaging.Abstractions` and `DlqFailureCauses` (closed-enum cause strings) into
`DcsvIo.D2.Messaging.RabbitMq`. The `DlqFailureMetadata` record applies `[JsonPropertyName(DlqFailureMetadataFields.*)]`
attributes referencing the generated constants, so the wire shape and the spec cannot drift apart. The TS sibling
`@dcsv-io/d2-messaging-abstractions` emits identical constants, so any TS reader (DLQ ops tooling, RabbitMQ subscribers)
shares byte-equal field-name and cause-string identifiers with the .NET producers.

Personally identifiable information (PII) safety: `detail` is never built from
`exception.Message` (handler code can interpolate user input). For
result-failure cases it joins the result's `messages.Select(m => m.Key)` -
translation-token strings, developer-controlled, safe. For exception cases it
stays `null`. All log delegates that take an `Exception` log only
`SanitizedExceptionRender.TypeName(ex)` + `FirstFrame(ex)` - no
`ex.Message`, no full stack trace. Handlers MUST NOT include user input in
exception messages, and the DLQ header and the logs omit exception messages even when a handler makes that mistake.

---

## DLX + DLQ convention

Every primary queue `{q}` gets:

- **DLX** `{q}.dlx` - fanout exchange.
- **DLQ** `{q}.dlq` - durable queue bound to the DLX.

The primary queue is declared with
`x-dead-letter-exchange = {q}.dlx` and `x-dead-letter-routing-key = ""`.

On any handler / boundary failure, the consumer republishes the original
body to `{q}.dlx` with the `x-d2-failure-reason` header attached, then
`BasicAck`s the original delivery. A dedicated republish channel keeps
publish state out of the consume channel's delivery queue. On republish
failure: emits `d2.messaging.rabbitmq.dlq_republish_failures` counter and
falls back to `BasicNack-no-requeue` (the header-less copy reaches the DLQ via
`x-dead-letter-exchange`).

---

## Optional retry tier topology

When a subscription declares a `tieredRetry` block in its spec entry, the
topology declarer declares:

```text
{queue}             -> primary queue (binds to descriptor.Exchange)
                      x-dead-letter-exchange = {queue}.dlx
                      x-dead-letter-routing-key = ""

{queue}.dlx         -> fanout DLX
{queue}.dlq         -> bound to {queue}.dlx (the actual DLQ)

{queue}.retry.return -> fanout exchange bound BACK to {queue} (routes TTL'd messages back in)

For each tier i in TieredRetry.Tiers:
  {queue}.retry.{i}    -> single name used for BOTH the fanout retry-tier exchange AND its bound queue.
                         The exchange's binding routes to the same-name queue; the queue has
                         x-message-ttl = tiers[i] and x-dead-letter-exchange = {queue}.retry.return.
                         (Ops tooling that lists queues vs exchanges in the broker management
                         UI sees the same name in both lists - broker resource type
                         disambiguates which is which.)
```

In normal use, a transient handler failure NACKs to one of the retry-tier
exchanges (the driver is responsible for routing; the framework declares the
topology but the per-handler driver code wires the NACK explicitly - the
framework does not auto-route on the consumer's behalf). When the
message's TTL expires, it is dead-lettered to the retry-return exchange, and RabbitMQ routes it back to the
primary queue. The consumer's `x-death`-driven attempt counter caps the total
cycles via `MaxAttempts`.

---

## Encryption posture

- One keyring per encryption domain, registered as a keyed singleton with
  `services.AddD2EncryptionFor(domain, factory)`. The public domain
  constants live in `DcsvIo.D2.Encryption.EncryptionDomains` (`PLAINTEXT`,
  `FIXTURE_SEALED`); a host registers its own domains at composition time.
- The descriptor's `encryption` field is the domain string (or the literal
  `"plaintext"`). The publisher resolves `IPayloadCrypto` keyed by that
  string.
- Plaintext entries MUST document `encryptionReason` in the spec. The build
  does not enforce a non-empty string; the field is visible in code review, where a reader
  asks why the entry is not encrypted.
- AMQP headers stay plaintext at rest. Only the body is wrapped in the
  encryption frame. Identity (`UserId` / `OrgId` / scopes) is never in
  headers; the broker stores headers plaintext, and routing semantics need
  them readable.
- The `EncryptionDomains` catalog is itself spec-driven via
  `DcsvIo.D2.Encryption.Domains.SourceGen`;
  the binary frame layout is spec-driven via
  `DcsvIo.D2.Encryption.Frame.SourceGen`;
  per-keyring rotation lifecycle lives in
  `DcsvIo.D2.Encryption`.

---

## Defaults that apply automatically

- Publisher confirms: on. Disable per-call via
  `PublisherOptions.WaitForConfirm = false` for fire-and-forget.
- Confirm timeout: 5s. A broker that is slow to confirm produces a transient timeout, which is
  retried up to `MaxAttempts` (default 5).
- Retry backoff: 200ms -> 2x -> cap at 5s. Worst case before
  `ServiceUnavailable`: 5 attempts x 5s confirm + ~7s of backoff ~= 32s.
- Persistent message delivery mode (`DeliveryModes.Persistent`).
- AMQP body content-type: `application/octet-stream` - body is opaque
  (encrypted bytes, or JSON bytes; never a structured AMQP type).
- Channel pool idle TTL: 5 min, so broker-side state stays bounded under
  low-traffic services.
- Subscriber-disposal in-flight drain: 30s. Host shutdown waits at most this long, so handlers
  that finish within it ack cleanly.

---
