// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

// Public surface for consumer and publisher. The publisher mirrors .NET's
// structural publish/encrypt fusion at the type level: `createPublisher`
// binds a compile-time type witness (an unwired encrypted domain is a compile
// error, and there is no raw-bytes publish surface) plus a second, runtime
// default-deny check.

// Connection.
export {
  RabbitMqConnectionOptions,
  type RabbitMqConnectionOptionsInit,
  createConnection,
  redactAmqpUri,
} from "./connection/connection-options.js";
export type { D2Connection } from "./connection/managed-connection.js";
export { UnencryptedTransportError } from "./connection/transport-admission.js";

// Subscription.
export {
  type SubscribeOptions,
  type Subscription,
  subscribe,
} from "./subscribing/subscriber.js";
export {
  type MessageHandler,
  type RepublishFn,
} from "./subscribing/delivery-processor.js";
export { type ConsumedMessage } from "./subscribing/consumed-message.js";
export { type RabbitMqSubscriberOptions } from "./subscribing/subscriber-options.js";

// Topology contract.
export {
  type SubscriptionDescriptor,
  type TieredRetryDescriptor,
  resolveQueueName,
} from "./topology/subscription-descriptor.js";
export { QueuePattern } from "./topology/queue-pattern.js";
export { DlqNaming } from "./topology/dlq-naming.js";

// Per-message context establishment.
export {
  type ConsumeContext,
  type MutablePropagatedContext,
  applyPropagatedContext,
  establishConsumeContext,
} from "./context/consume-context.js";

// Body-decompose seam.
export {
  type BodyOpener,
  PlaintextBodyOpener,
} from "./subscribing/body-opener.js";
export {
  CryptoBodyOpener,
  assertOpenerMatchesDomain,
} from "./subscribing/crypto-body-opener.js";
export { MessageBodyDecodeError } from "./subscribing/message-body-decode-error.js";

// Publisher: the auto-encrypting publish/encrypt fusion.
export {
  createPublisher,
  publishVia,
  type D2Publisher,
  type CreatePublisherOptions,
  type PublishEnvelope,
  type PublishSender,
} from "./publishing/publisher.js";
export {
  composeBody,
  type Composer,
  type ComposedBody,
} from "./publishing/body-composer.js";
export {
  type DomainCryptoMap,
  type DomainCryptoMapOf,
  type ComposerFor,
  type ComposerForMode,
  type EncryptedDomain,
  type PublishableKey,
  type PublishableKeyOf,
  type CatalogEncryption,
} from "./publishing/domain-crypto-map.js";
export { readEncryptionKid } from "./publishing/encryption-kid.js";

// Application-defined encryption-domain modes (publisher and consumer).
export {
  type ApplicationDomainModes,
  resolveDomainModes,
} from "./encryption-domain-modes.js";

// Idempotency.
export {
  type IMessageIdempotencyStore,
  InMemoryMessageIdempotencyStore,
} from "./idempotency/message-idempotency-store.js";

// Telemetry.
export { MESSAGING_SOURCE_NAME } from "./telemetry.js";
