<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-messaging-rabbitmq

The TypeScript runtime twin of the .NET `DcsvIo.D2.Messaging.RabbitMq` consumer path. A service author building a Node
service that consumes messages a .NET service publishes uses this package: a service-agnostic RabbitMQ subscriber with
the same topology, same DLQ convention, and same cross-hop context and trace propagation, so a Node consumer and a
.NET consumer are interchangeable on the wire.

Built on `rabbitmq-client` (zero-dep, auto-reconnecting), pinned exact with an additive raw-body patch. The published
package bundles that patched client and its MIT license. Consumers need no patch configuration or installation hook.

---

## Install

```bash
pnpm add @dcsv-io/d2-messaging-rabbitmq
```

## Publish/encrypt fusion

Publishing and encryption are fused: the composer for a domain is the only path to the socket for that domain. This
is the TS twin of .NET's spec-driven composer + DI. `createPublisher({ crypto, descriptors })` binds a compile-time
type witness: `publish(key, message)` accepts only a message the bound catalog contains whose encryption domain is
`plaintext` or was wired into `crypto`. Publishing to an unwired encrypted domain is a compile error, and there is no
raw-bytes publish overload. A sealed domain's slot only accepts an `IPayloadSealer`; a symmetric slot only an
`IPayloadCrypto` (mode-branded by the generated `EncryptionDomainModes` literals, and by `modes` for the application's
own domains). The compile witness is pinned by `tests/publisher-type-witness.compile.ts` (`@ts-expect-error` proofs
under the type-check gate).

`descriptors` is the catalog binding, and one option drives both the runtime descriptor lookup that picks the exchange
and the composer, and the compile-time key constraint. Pass the `as const` catalog the application's own
catalog package exports. The framework catalog this package defaults to is empty, so a publisher that binds none
accepts no key at all and every `publish` call is a compile error - an application binds its own.

`modes` declares the application's own encrypted domains - each domain its
catalog publishes on that the framework `EncryptionDomainModes` catalog does not
contain - as `sealed` or `symmetric`. Each declared mode fixes the composer that
domain's `crypto` slot accepts, and the runtime check resolves the domain's mode
from the same map. A domain in neither `modes` nor the framework catalog has no
`crypto` slot and is refused at runtime. `createPublisher` validates `modes`
through `resolveDomainModes` before it opens the confirm-publisher, and throws on
an empty domain name, a mode for `plaintext`, a mode other than `sealed` or
`symmetric`, or a framework domain declared with a mode other than its own.
This is the TypeScript counterpart of .NET `EncryptionDomainModeCatalog`, which
registers only sealed domains because an unregistered domain resolves to
symmetric there; here a symmetric application domain is declared as well.
The map's type is `ApplicationDomainModes`; `DomainCryptoMapOf<TModes>` is the
wiring map it produces (the framework `DomainCryptoMap` plus one slot per
application domain), and `ComposerForMode<M>` is the composer a mode requires.

```ts
import {
  createConnection,
  createPublisher,
  RabbitMqConnectionOptions,
} from "@dcsv-io/d2-messaging-rabbitmq";
import { AppMqMessages, AppMqMessagesCatalog } from "@your-scope/app-messaging-catalog";

const connection = createConnection(new RabbitMqConnectionOptions({
  connectionUri: process.env.D2_RABBITMQ_URI!,
}), logger);

const publisher = createPublisher(connection, {
  logger,
  modes: { orders: "sealed" }, // the application's own encrypted domains
  crypto: { orders: ordersSealer }, // one entry per encrypted domain published to
  descriptors: AppMqMessagesCatalog,
});

await publisher.publish(AppMqMessages.OrderPlaced, { actorId });
```

A runtime default-deny check (`composeBody`) covers dynamic / fixture paths, in addition to the compile-time
constraint: the descriptor's domain mode is consulted unconditionally, and a missing composer for an encrypted domain,
or an unknown domain, throws an error before any socket write. The body is composed once (a resend reuses the exact
bytes - no re-encrypt under a fresh nonce). The host wires composer instances into
`createPublisher({ crypto, descriptors })` (and `CryptoBodyOpener` on the consume path) using
`@dcsv-io/d2-encryption` sealer / opener / symmetric ports.

On the consume side, `CryptoBodyOpener` (sealed / symmetric) is the decrypting opener for the body-decompose seam: a
wrong-version frame, a plaintext body on an encrypted domain, tampering, or an unknown kid all DLQ with
`DECRYPT_FAILURE` (never a silent mis-decode); `assertOpenerMatchesDomain(domain, opener, modes)` is the consumer-side
subscriber-vs-opener cross-check. Pass it the same `modes` as the publisher: a sealed domain needs a sealed
`CryptoBodyOpener` for that domain, a symmetric domain needs a symmetric one for that domain, and a domain other than
`plaintext` that resolves to no mode is refused, as the publisher refuses it.

---

## Quick start

```ts
import {
  createConnection,
  subscribe,
  QueuePattern,
  InMemoryMessageIdempotencyStore,
  RabbitMqConnectionOptions,
} from "@dcsv-io/d2-messaging-rabbitmq";

const connection = createConnection(new RabbitMqConnectionOptions({
  connectionUri: process.env.D2_RABBITMQ_URI!, // secret - never logged whole
  clientProvidedName: "orders-svc",
}), logger);

const sub = subscribe({
  connection,
  logger,
  store: new InMemoryMessageIdempotencyStore(),
  descriptor: {
    queueName: "orders.order-placed",
    exchange: "example.orders.placed",
    exchangeType: "fanout",
    pattern: QueuePattern.DurableShared,
    routingKeyBinding: "",
    prefetch: 8,
    idempotency: true,
    nackedBy: "orders-svc",
  },
  handler: (message, ctx) => {
    // `message` = the decoded body; `ctx.propagated` = the per-message
    // operational context (requestId / fingerprints / callPath - never identity).
    return doWork(message);
  },
});

await sub.ready;
```

`createConnection(options, logger)` takes a `RabbitMqConnectionOptions` instance and an `ILogger`, and returns a
managed `D2Connection`, not the vendor connection. It validates the logger before opening
transport and observes sanitized connection errors through shutdown and late events.
Logger or sanitizer failures are caught without retry or fallback logging; those
diagnostics are lost, while vendor recovery and close results remain unchanged.
Pass that handle to `subscribe` and `createPublisher`; raw vendor handles are
rejected. Await `connection.close()` to stop registered resources before closing
the shared transport. Repeated close calls return the same completion. Create
separate vendor connections for operations outside the D2 API.

### Connection options

`new RabbitMqConnectionOptions(init)` copies a `RabbitMqConnectionOptionsInit` -
`connectionUri`, `allowUnencryptedTransport`, `clientProvidedName`, `retryLowMs`,
`retryHighMs` and `heartbeatSeconds` - and freezes the instance. The address
contains the broker credential, so the instance keeps it in a private field:
`connectionUri` is not a property of the instance, and `revealConnectionUri()` is
the only read. `JSON.stringify`, `structuredClone`, `util.inspect`, object spread
and a structured log read the public fields, so none of them renders the address.
The address is read once per instance: `createConnection` reads it, checks its
transport and connects to it, and the dead-letter transport of each subscription
on that connection connects to the same checked address without reading it again.
An object not created by `new RabbitMqConnectionOptions(...)`, such as a plain
object literal, is refused with a `TypeError` whose message never contains the
address, even when the object has its own `revealConnectionUri` method. The
.NET twin stores `RabbitMqConnectionOptions.ConnectionUri` the same way, behind
`RevealConnectionUri()`.

### Encrypted transport

The broker address contains the broker credential, and every message body crosses
the connection. `createConnection` refuses an address whose scheme is not `amqps`
unless its host is loopback or `allowUnencryptedTransport` is `true`, and throws
`UnencryptedTransportError` before any connection attempt. Loopback means
`localhost` in any case, an IPv4 address in 127.0.0.0/8 written as four decimal
octets with no leading zeros, or `[::1]`; any other spelling counts as remote. An
address that does not parse as a URL is refused even with the flag. The error
message never contains the address. Each subscriber's dedicated dead-letter
transport connects to the address its connection was checked with.

Set `allowUnencryptedTransport: true` only where nothing can observe the network
path to the broker, such as a local container network: the credential and every
message body cross that path unencrypted. The flag is never passed to the vendor
client. The .NET twin is `RabbitMqConnectionOptions.AllowUnencryptedTransport`.

```ts
// Local Docker only: the Compose broker listens without TLS.
const connection = createConnection(new RabbitMqConnectionOptions({
  connectionUri: process.env.D2_RABBITMQ_URI!,
  allowUnencryptedTransport: true,
}), logger);
```

### Dead-letter deadlines

`RabbitMqSubscriberOptions` defines the subscriber deadlines.
`subscriberOptions.dlqRepublishTimeout` defaults to five seconds and
`subscriberOptions.dlqCleanupTimeout` defaults to one second. Both accept
`Temporal.Duration` values containing whole elapsed milliseconds from 1 through
2,147,483,647; calendar units and fractional milliseconds are rejected.

The publication deadline includes admission, channel acquisition, confirm setup,
publication and the return/confirm barrier. Each subscriber owns a dedicated DLQ
connection. Timeout or shutdown terminates only that connection, then waits for
cleanup within the additional cleanup budget. Pending cleanup remains owned and
prevents replacement allocation until it completes. Shutdown stops DLQ work before
waiting for consumer callbacks.

The subscriber ACKs the original delivery only after a confirmed, routable publication.
Failure uses NACK without requeue on the original consumer channel. A lost confirm
does not prove the broker rejected the publication; fallback can produce a duplicate.
The subscriber copies original bytes and AMQP metadata before calling an opener or
handler. The original AMQP message-id property remains separate from the validated
processing identity and header fallback. A missing patched `rawBody` is a transport
error. The subscriber never reconstructs the body or acknowledges the delivery in that case.

The handler returns a `D2Result`. A failed result dead-letters the message
(`HANDLER_RESULT_FAILURE`); a thrown error dead-letters it
(`HANDLER_EXCEPTION`). On success the subscriber acks, after writing the idempotency mark.

---

## What the runtime does (per delivery)

1. Trace linkage - parses the producer's `traceparent` and starts a
   `Consumer`-kind span `receive {queue}` whose parent is the publish span, so
   the trace assembles across runtimes. A missing / malformed header starts a
   root span (never a reject). Span tags come from the spec-emitted
   `MessagingActivityTags` closed set (same values the .NET consumer emits).
2. Per-message context - decodes the `x-d2-context` header
   (base64url-of-JSON, exactly what the .NET `PropagatedContextSerializer.Encode`
   and the gRPC interceptor produce) via the shared
   `@dcsv-io/d2-request-context-abstractions` serializer and applies the operational
   subset (request id / path / fingerprints / WhoIs hash / locale-tier fields /
   `callPath`) onto a fresh per-message context. Identity is never taken from
   the wire, and `RequestOrigin` is never wire-reconstructed - those
   slots do not exist on the applied shape (authority-grade origin is established
   only from local transport evidence, never from a forwarded header). A
   malformed header is fail-safe (empty context, message still processed).
3. Idempotency (opt-in) - a precise contract mirroring .NET: a
   seen `message-id` is ack-and-skipped, never dead-lettered; a read-path
   store outage fails open (process anyway); the mark is written only on
   the success path before the ack; a mark-write failure NACKs to the DLQ
   (never leave the dedup window unguarded); failure paths never mark.
4. Body decompose - an injectable opener seam. The default handles
   plaintext (raw UTF-8 JSON) and throws on any body whose first byte is a
   known encryption-frame version (1 or 2), which sends the message to the DLQ with `DECRYPT_FAILURE`, never a
   silent mis-parse.
5. Dead-lettering - on failure the original body is republished to
   `{queue}.dlx` with an `x-d2-failure-reason` header (`DlqFailureMetadata`:
   `cause` / `errorCode` / `detail` / `attemptCount` / `traceId` / `nackedBy`,
   PII-safe) then the original is acked after routing and confirmation; a republish failure falls back to
   NACK-no-requeue. Producer headers (`traceparent`, `x-d2-context`, ...) are
   forwarded on the DLQ copy.

---

## Topology

`subscribe` declares the exact .NET topology (see
`DlqNaming`):

- primary queue with `x-dead-letter-exchange = {queue}.dlx`
- `{queue}.dlx` fanout DLX -> `{queue}.dlq` durable DLQ
- optional retry tiers (`{queue}.retry.{i}` + `{queue}.retry.return`)

Queue patterns: `CompetingConsumer`, `DurableShared`,
`FanoutExclusiveAutoDelete` (auto-suffixed per process to avoid the exclusive
queue lock).

---

## Testing

- Unit (`pnpm test`) - the full delivery matrix against injected seams;
  100% `src/**` coverage.
- Integration (`pnpm test:integration`) - a Testcontainer RabbitMQ replaying
  real .NET-emitted golden messages: wire-contract consume,
  encrypted-frame -> DLQ, handler-failure DLQ metadata, idempotency dedup, and
  competing consumers.
- Descriptor mirror - `MqMessages` / `MqMessagesRegistry` /
  `MqMessagesCatalog` (in `@dcsv-io/d2-messaging-abstractions`, generated from
  the messaging specs; sources committed) mirror the .NET `MqMessagesRegistry`.
  The framework catalog contains no message, so no parity assertion runs over it
  here. The application that owns a catalog runs the byte-equality gate over its own pair.
  That gate compares the catalog's generated TypeScript registry, canonically and
  field-by-field, against a fixture the .NET side emits from the registry the
  same specs produced. An application binds its catalog per Publish/encrypt
  fusion above.

## Packing from source

Build the package closure, then use the guarded pack entry point:

```bash
pnpm --filter "@dcsv-io/d2-messaging-rabbitmq..." -r --no-bail build
pnpm --filter @dcsv-io/d2-messaging-rabbitmq run pack:patched -- /absolute/output
```

The helper requires Node.js, pnpm and system `tar`. It installs the pinned client
in an isolated producer with lifecycle scripts disabled, verifies the complete
client tree, and packs only a verified artifact. It refuses an existing target.
Direct `pnpm pack` is rejected because it would omit the patched dependency.
The dependency-maintenance document in this package's `docs` folder in the source
repository covers regeneration and upgrade checks.

---

## Dependencies

- `rabbitmq-client` - bundled patched transport, with its MIT license.
- `temporal-polyfill` - elapsed subscriber duration values.
- `@dcsv-io/d2-headers-amqp` - AMQP header wire-value constants.
- `@dcsv-io/d2-messaging-abstractions` - `DlqFailureMetadataFields` / `DlqFailureCauses`
  + the `MqMessages` descriptor mirror.
- `@dcsv-io/d2-request-context-abstractions` - `PropagatedContextSerializer` +
  `IPropagatedContext`.
- `@dcsv-io/d2-encryption-abstractions` - frame-version constants (used to reject an encrypted body on the plaintext
  path).
- `@dcsv-io/d2-telemetry` - the `MessagingActivityTags` span-tag catalog.
- `@dcsv-io/d2-result`, `@dcsv-io/d2-logging`, `@dcsv-io/d2-utilities` - cross-cutting.
