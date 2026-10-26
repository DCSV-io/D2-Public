<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# ADR-0009: Async messaging - transport-agnostic `IMessageBus`, default-deny `[MqPub]`/`[MqSub]`, encrypted payload frames, DLQ + idempotency

- **Status**: Accepted
- **Date**: 2026-05-30

## Context

D2 needs durable async event delivery between services (audit recording, keyring-refresh broadcast, notification
dispatch, outbound message delivery). Forces:

1. **Transport coupling risk.** Direct `RabbitMQ.Client` use in domain code couples business logic to AMQP; a transport
   swap or test isolation then requires changes in every publisher and subscriber.
2. **Silent misconfiguration.** Without a registry contract, a mistyped exchange name or unintended plaintext body
   compiles and is found only in production.
3. **PII on the wire.** Audit, notification, and outbound-delivery payloads contain actor identities, addresses,
   financial figures, verification codes. TLS in transit is necessary but not sufficient: broker-at-rest, DLQ
   archives, and ops tooling all see plaintext unless the body itself is encrypted.
4. **At-least-once reality.** AMQP gives at-least-once; exactly-once is not available. The practical answers are
   idempotent handlers + a DLQ with a structured failure envelope.
5. **Cross-language parity.** TS services consume keyring events and ops reads DLQ headers; byte-level specs (frame layout, field names, cause strings, domain ids) must be shared by mechanical generation.

The spec-driven codegen decision (ADR-0002), the abstractions/implementation split (ADR-0006), errors-as-values (ADR-0003), and the caching abstractions (ADR-0008, which backs the idempotency store) are direct predecessors.

## Decision

### 1. Transport-agnostic `IMessageBus` + default-deny `[MqPub]`/`[MqSub]`

`IMessageBus.PublishAsync<TMessage>` (returns `D2Result`) is the only publish surface in domain code; broker, confirms,
channel pooling, and retry are hidden behind it. Every publishable type must have `[MqPub(<constant>)]`; the publisher
looks the constant up in the one `MessagingCatalog` the host registers, which the consumer, the topology declarer and
every outbox dispatcher read as well. A type with no attribute, an unrecognized constant, or a CLR FQN that does not
match the spec entry throws at the first publish - the **default-deny** posture (a silently-routed message is the worst
configuration-drift failure). Subscriber handlers have `[MqSub(<constant>)]`; `AddD2SubscribersFromAssembly` validates
each derives from `BaseHandler` with the matching input type and registers it - a missing constant or FQN mismatch
throws at composition time. `mq-messages` and `mq-subscriptions` spec files drive `DcsvIo.D2.Messaging.SourceGen` to
emit the constants, descriptor records and registries. The library's own catalogs (`contracts/mq-messages/`,
`contracts/mq-subscriptions/`) are mechanism and declare no entries: message vocabulary belongs to the host that owns
the messages. A host declares its entries in its own spec files, and the generator emits them into a consumer-owned
catalog assembly over the union of every spec file that assembly lists. A constant declared in two of those files is a
build error (`D2MQ013`), not a merge decided by file order. A host that registers no catalog of its own gets
`MessagingCatalog.Default`, built from the library's empty registries, and its first publish of a host message throws.
Domain code references only the constants and never imports `RabbitMQ.Client`. The codegen-declared queue patterns are
`CompetingConsumer`, `DurableShared` and `FanoutExclusiveAutoDelete` (per-replica, suffixed with a short per-process
token - an 8-char UUIDv7 fragment).

### 2. Sensitive payloads encrypted in a self-describing binary frame; the encryption primitive is domain-agnostic

The wire body is either raw UTF-8 JSON or an AES-256-GCM encrypted frame. The choice is determined entirely by
`MqMessageDescriptor.Encryption`: an `EncryptionDomains` constant means encrypted; the literal `"plaintext"` means not.
The frame (`encryption/core/EncryptionFrame.cs`) is `[version=1][kid_len][kid][nonce:12][ciphertext+16-byte GCM tag]` -
self-describing: a receiver needs only the bytes and a keyring containing the kid, with no envelope wrapper or
per-message key negotiation. The kid is also copied into an `x-d2-encryption-kid` AMQP header so DLQ ops can identify
the archive key without decrypting. Frame byte offsets/constraints are spec-driven (`contracts/encryption-frame/`), with
a TS mirror (`@dcsv-io/d2-encryption-abstractions`) that uses the same byte offsets in both languages.

`DcsvIo.D2.Encryption` is a standalone library with no dependency on messaging/domains/key-fetching: a
`PayloadCryptoKeyring` (JWKS-style, immutable, `IDisposable` zeroes key bytes via `CryptographicOperations.ZeroMemory`),
`PayloadCrypto` (per-call `AesGcm` for thread safety), and the frame codec. AAD is bound to the keyring's context bytes,
so cross-domain ciphertext replay is structurally impossible at the AEAD layer. Keyrings register as keyed singletons
(`AddD2EncryptionFor`); the bus resolves `IPayloadCrypto` keyed by the descriptor's domain in a transient per-publish
scope. The `EncryptionDomains` catalog (a `PLAINTEXT` sentinel, a framework fixture sealed domain, and the domains a
host registers at composition time) is itself codegen'd, and the message source-gen cross-validates each `mq-messages`
encryption value against it at build time, so a typo cannot route to a non-existent keyring. AMQP headers stay
plaintext but contain only routing metadata + W3C trace context + the propagation-safe `x-d2-context` subset
(ADR-0007) - never identity or payload fragments.

#### Per-domain **sealed** mode: a per-consumer-service capability split

An encryption domain has one of two **modes**: **`symmetric`**, the shared-keyring AES-256-GCM frame described in
decision section 2, and **`sealed`**. A domain declares its mode in `contracts/encryption-domains`
(`mode: symmetric | sealed`, default `symmetric`; a sealed domain also declares `consumerService`, the single
decryptor's ServiceId). `plaintext` is a no-encrypt sentinel. The mode is a single-source domain fact read from the
generated catalog (`EncryptionDomainModes` / `MqMessageDescriptor.IsSealed` + `.ConsumerService`) - never a second
generated surface.

- **Construction.** Ephemeral-static ECDH-ES over P-256 -> HKDF-SHA256 (service-bound, length-delimited `info`; salt =
  AAD = `UTF-8(consumerServiceId)`; frozen values) -> per-message AES-256-GCM. Forward secrecy comes from the
  per-message ephemeral keypair. One auto-provisioned ECDH keypair per consumer service (distinct from its mTLS leaf -
  key separation), rotated on the host's key-lifecycle system.
- **Wire.** A **version-2** frame
  (`[version=2][recipient_kid_len][recipient_kid][eph_pub_len:2 BE][eph_pub:SPKI][nonce:12][ciphertext+tag]`),
  version-dispatching decoders (a v1 frame arriving on a sealed domain is a hard version-mismatch -> DLQ).
  `x-d2-encryption-kid` contains the recipient kid for DLQ triage. The v2 layout is spec-driven
  (`contracts/encryption-frame`) with a byte-identical TS mirror, pinned by known-answer tests in both directions.
- **Capability split.** `IPayloadSealer` (public-key, **encrypt-only** - no `Open` member) vs `IPayloadOpener`
  (private-key, **decrypt-only** - no `Seal` member). A producer holding only a sealer **cannot open any sealed frame,
  including its own output**. The private key leaves the host key-lifecycle authority only to its owning service,
  selected purely by authenticated mTLS peer identity (a targetless op) - the enforcement is on the authority side,
  not in the DI shape.
- **Auto-wiring.** The composer resolves domain -> `consumerService` -> the keyed sealer (publish) / opener (consume),
  keyed by the recipient service (sealed domains that share a consumer share one sealer/opener). Zero per-message
  hand-code. One host registration per service (host-supplied key-lifecycle integration) wires a sealer for every
  sealed-domain consumer (lazy public-key fetch) and the private-key opener only when the service is itself a sealed
  consumer; a rabbitmq-lib boot check crashes a host whose sealed subscriber has no matching opener (this catches a
  forgotten registration call). The TS twin enforces the same fusion structurally (a compile-time publisher
  type-witness + a runtime default-deny check that applies the rule a second time - referenced here, not
  co-specified).

### 3. At-least-once: idempotency store + DLQ + tiered retry + `x-death` exhaustion

A stable UUIDv7 `message-id` is generated once per publish and reused across retry attempts; body bytes are composed
once and reused (a retry does not re-encrypt with a new nonce). Publisher confirms are on by default (bounded retry via
a transient classifier distinguishing retriable broker conditions from terminal ones).

**A publish routes to at least one queue, or it failed.** Every publish goes out `mandatory`, so the broker returns a
message it cannot route instead of confirming and dropping it, and the client completes that publish with a
`PublishException` whose `IsReturn` is set. The classifier treats every `PublishException`, a return included, as
transient: when two processes declare the same binding at once, the broker can respond with `bind-ok` before its routing
index contains the binding, and a publish in that window matches no queue and succeeds on the next attempt. A publish
still unroutable after its retries fails with `LIB_MESSAGING_PUBLISH_UNROUTABLE`, which an operator can tell apart from
an unreachable broker (`GEN_SERVICE_UNAVAILABLE`). A host that publishes to an exchange with no bound queue therefore
sees a failure, by design.

Subscriptions declaring `idempotency: true` get an `IMessageIdempotencyStore` pre-check; the default
`CacheIdempotencyStore` uses `IDistributedCache` (ADR-0008) with a 24-hour TTL and `SetNx` semantics - check before
dispatch, mark after success before `BasicAck`; read-path failure is fail-open (handlers are at-least-once-safe),
write-path failure is NACK-to-DLQ. `IdempotencyStartupCheck` hard-fails host startup if a subscription declares
idempotency but no store/cache is registered (a silent no-op on a safety feature is the worst default). Every primary
queue gets a DLX + DLQ; on any handler-boundary failure the consumer republishes the original body to the DLX with an
`x-d2-failure-reason` header containing a JSON `DlqFailureMetadata` record. The republish contains the original bytes
and AMQP metadata, goes out `mandatory` and confirmed, and only a confirmed, routable republish acks the original; every
other outcome NACKs it without requeue on its consumer channel. Each subscriber republishes over a dead-letter
connection it owns rather than the process connection. `RabbitMqSubscriberOptions.DlqRepublishTimeout` (default five
seconds) bounds a republish from admission through confirm; a timeout or shutdown terminates only that dedicated
connection and waits up to `DlqCleanupTimeout` (default one second) for its cleanup. Cleanup still pending after that
wait stays owned and blocks a replacement connection until it completes. `DlqFailureMetadata` property names are
codegen-bound (`contracts/dlq-failure-metadata/`), and `detail` is PII-safe by construction (populated only from result
message keys, null for exceptions; log delegates emit only `SanitizedExceptionRender.TypeName(ex)` + `FirstFrame(ex)`,
never `ex.Message` - see ADR-0011). Tiered retry creates per-tier TTL queues + a return exchange; the consumer
enforces a hard total-attempt cap by summing `x-death` counts (filtered to `expired`/`rejected` reasons) and routes
straight to DLQ with cause `RETRIES_EXHAUSTED` when the cap is met.

**The TypeScript twin owns its connection the same way.** `createConnection(options, logger)` requires an `ILogger`
and returns a managed `D2Connection`, not the vendor connection. `subscribe` and `createPublisher` reject a raw vendor
handle, and `connection.close()` stops the registered publishers and subscribers before it closes the shared
transport. The connection attaches a sanitized, non-throwing error listener as soon as the vendor connection exists and
keeps it for the connection's lifetime: the vendor client can emit an error before any resource is registered, and
Node throws an error event that has no listener as an uncaught exception. Each subscriber owns a dedicated dead-letter
connection under the same republish and cleanup deadlines, waits for the broker's return or confirm inside the
publication deadline, and copies the original bytes and AMQP metadata before it calls an opener or a handler. The
original bytes come from a pinned vendor client that contains an additive raw-body patch, which the published package
bundles; a delivery that arrives without them is a transport error, never a body to reconstruct or acknowledge.

### 4. Spec-driven registries and catalogs - cross-language

All contracts that cross an assembly or language boundary live in `contracts/` and are consumed only through codegen output: `mq-messages` / `mq-subscriptions` (constants + descriptors + registries), `encryption-domains` and `encryption-frame` (both with TS mirrors). Hand-writing any of these constants is forbidden.

## Consequences

**Positive.**

- Domain services publish via `IMessageBus` and subscribe via `[MqSub]` with no AMQP knowledge; swapping transport replaces only the RabbitMQ assembly.
- A message lacking `[MqPub]`, referencing a nonexistent constant, or with an FQN mismatch fails with an error at first publish
  (or composition time for subscribers) - silent misrouting eliminated.
- A message that reaches no queue is a failure the publisher returns, not a broker counter nobody reads, and an
  unroutable failure is distinguishable from an unreachable broker.
- A timed-out or stopped dead-letter republish ends one subscriber's own connection, never the connection every
  publisher shares.
- PII-bearing bodies are encrypted in transit, at-rest in the broker, and in DLQ archives; a broker compromise does not
  expose payload content. Headers (broker-plaintext) contain only routing + trace context.
- The self-describing frame supports key rotation: in-flight messages under the retiring kid decrypt from the same keyring during overlap; the broker needs no rotation awareness.
- The 24-hour idempotency window covers the realistic redelivery window and is bounded; the startup hard-fail prevents silent safety no-ops.
- `DlqFailureMetadata` gives ops structured triage (cause, errorCode, traceId, archive-key kid) without decrypting the body.

**Negative / risks.**

- The default-deny `[MqPub]` check is runtime, not compile-time: a missing attribute fails at first publish if the path is not exercised by integration tests.
- A publish to an exchange with no bound queue fails after its retries instead of succeeding. A host that announces
  something no consumer listens for yet has to bind a queue or stop publishing it.
- Each subscriber holds a second broker connection for its dead-letter republishes.
- The TypeScript package bundles a patched vendor client, so a plain `pnpm pack` of the package is refused and the
  package is packed through its own patched-pack script.
- The cache-backed idempotency store couples idempotency to the cache tier: a Redis outage during the write path causes
  NACK-to-DLQ for already-processed messages - ops must monitor ack-failure metrics.
- Per-call `AesGcm` is concurrency-safe but allocates/frees native memory per op; at high sustained throughput this may
  add GC pressure - benchmark before scaling a high-volume encrypted domain.
- For a **symmetric** domain, encryption is domain-agnostic: every publisher/consumer of the domain shares its keyring;
  there is no encrypt-only/decrypt-only capability split within the domain (payload minimization is the compensating
  control). This bullet covers symmetric domains only - a **sealed** domain has a per-consumer-service capability split
  (sealer vs opener), so it may contain fully denormalized content (no consumer-side DB lookup / callback). Sealed
  domains add these negatives: a per-message ECDH agreement (CPU cost - benchmark a high-volume sealed domain); a
  single-point decrypt authority per consumer service (the key-rotation / compromise runbook applies); first-seal
  lazy-provision latency; and a second frame version every decoder must handle.
- A **sealed**-mode domain has no symmetric keyring/lifecycle existence: a sealed domain is not in the symmetric
  payload catalog at all (one-way by construction - its per-service sealing keys are kept by the host-supplied
  key-lifecycle integration instead). The symmetric machinery (the host-supplied key lifecycle and the consumer
  runtime) remains domain-generic; the payload catalog is empty until a symmetric-mode domain is declared.
- `FanoutExclusiveAutoDelete` queues are non-durable: a broker restart drops in-flight messages for those subscriptions (intentional for keyring-refresh, which re-syncs on reconnect, but must be documented per subscription).

## Alternatives considered

**Direct `RabbitMQ.Client` use in domain code.** Domain services would import AMQP types, requiring a live/mock broker for every publisher/subscriber test, and transport evolution would touch every service. The `IMessageBus` + attribute contract keeps transport behind the library boundary.

**Envelope wrapper format.** A typed envelope (`{type, correlationId, payload}`) co-locates routing metadata and
business data but adds parse overhead, inflates size, and forces every decoder to understand the envelope before
deserializing. The chosen approach puts routing metadata in AMQP headers (broker reads natively) and keeps the body as
the serialized message - or, when encrypted, the opaque frame.

**Encrypting at rest only, or TLS only.** Storage-layer or TLS-only encryption does not protect broker memory, DLQ archive blobs, log captures, or management-API observers. Body-level AES-256-GCM means plaintext is never visible to the broker; TLS remains required for defense in depth but is not the primary payload-confidentiality control.

**No idempotency store / exactly-once delivery.** Exactly-once is unachievable on AMQP without a transactional log
broker. The alternative to a store is requiring every handler to implement transactional dedup (a DB UNIQUE on
`message_id`) - the right answer for handlers that already transact, and the store's docs note this. The cache-backed
store exists for the idempotent-side-effect case (external API calls, email sends) where a DB transaction does not help;
offering nothing would force every subscriber to re-implement a 24-hour keyed dedup cache.

**Publish without `mandatory`, and treat a returned publish as terminal.** The broker confirms
and drops a message it cannot route, the client reports success, and the only trace is the broker's own unroutable
counter. Classifying a return as a routing bug that a retry cannot fix is also wrong for the case that loses messages:
the window between a concurrent binding's `bind-ok` and the broker's index write closes within milliseconds, and the
retry succeeds.

**Product entries in the library's own catalogs.** A host's message vocabulary is not library mechanism, and an
Apache-2.0 contract tree is the wrong home for it. The library keeps the catalog shape with no entries so its own
packages compile, and every host generates its own catalog.

**Dead-letter republish over the shared process connection.** Canceling a republish does not end an open request the
client has already sent, and a failed acquisition left nothing owning what it had started, on the connection every
publisher uses. A connection the subscriber owns can be terminated on a deadline without touching anyone else's.

## References

- `packages/dotnet/messaging/abstractions/` - `IMessageBus.cs`, `MessagingCatalog.cs`,
  `MqPubAttribute.cs`/`MqSubAttribute.cs`, descriptor records, `SubscriberRegistrar.cs`/`SubscriberRegistry.cs`,
  `IMessageIdempotencyStore.cs`, `DlqFailureMetadata.cs`, `TieredRetryDescriptor.cs`, `QueuePattern.cs`.
- `packages/dotnet/messaging/rabbitmq/` - the README (canonical async-messaging reference) +
  `Encryption/EncryptedBodyComposer.cs`, `Encryption/MessageWireResolver.cs`, `Publishing/RabbitMqMessageBus.cs`,
  `Publishing/TransientPublishClassifier.cs`, `Subscribing/RabbitMqSubscriberOptions.cs`,
  `Idempotency/CacheIdempotencyStore.cs` + `Idempotency/IdempotencyStartupCheck.cs`, `Subscribing/SubscriberChannel.cs`
  (`x-death` enforcement + DLQ republish).
- `packages/dotnet/encryption/core/` - `IPayloadCrypto.cs`, `PayloadCrypto.cs`, `PayloadCryptoKeyring.cs`,
  `EncryptionFrame.cs`, `EncryptionServiceCollectionExtensions.cs`, README.
- `packages/typescript/messaging/rabbitmq/` - the README and `src/connection/` (`createConnection` and the
  managed `D2Connection`).
- `contracts/mq-messages/`, `contracts/mq-subscriptions/`, `contracts/messaging-error-codes/`,
  `contracts/encryption-frame/`, `contracts/encryption-domains/`.
- [ADR-0002](0002-spec-driven-codegen.md) (codegen), [ADR-0006](0006-abstractions-implementation-split.md) (abstractions split + keyed DI), [ADR-0003](0003-d2result-errors-as-values.md) (`IMessageBus` returns `D2Result`), [ADR-0008](0008-caching-marker-interfaces.md) (`IDistributedCache` backs `CacheIdempotencyStore`), [ADR-0007](0007-request-context-propagation.md) (`x-d2-context` header), [ADR-0011](0011-pii-redaction-logging-safety.md) (PII-safe log delegates).
