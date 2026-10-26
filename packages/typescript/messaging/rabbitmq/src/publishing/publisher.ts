// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { SpanStatusCode } from "@opentelemetry/api";
import type { EncryptionDomainMode } from "@dcsv-io/d2-encryption-abstractions";
import { AmqpHeaders } from "@dcsv-io/d2-headers-amqp";
import type { ILogger } from "@dcsv-io/d2-logging";
import { sanitizedErrorRender } from "@dcsv-io/d2-logging";
import {
  MqMessagesRegistry,
  type MqMessageDescriptor,
  type MqMessagesCatalog,
} from "@dcsv-io/d2-messaging-abstractions";
import {
  type D2Result,
  ok,
  serviceUnavailable,
  unhandledException,
} from "@dcsv-io/d2-result";
import { MessagingActivityTags } from "@dcsv-io/d2-telemetry";
import { uuidv7 } from "@dcsv-io/d2-utilities";
import {
  type D2Connection,
  registerConnectionResource,
} from "../connection/managed-connection.js";

import {
  publishFailuresCounter,
  publishesCounter,
  producerTracer,
} from "../telemetry.js";
import { composeBody, type Composer } from "./body-composer.js";
import {
  type ApplicationDomainModes,
  resolveDomainModes,
} from "../encryption-domain-modes.js";
import type { DomainCryptoMapOf, PublishableKey } from "./domain-crypto-map.js";

const _CONTENT_TYPE = "application/octet-stream";

/**
 * Default broker-confirm wait bound (ms), mirroring the .NET publisher's
 * `RabbitMqPublisherOptions.ConfirmTimeout` default (5s, `RabbitMqMessageBus.cs`).
 * In confirm mode a wedged broker must never hang the publish call forever.
 */
const _CONFIRM_TIMEOUT_MS = 5_000;

/** The AMQP publish envelope this publisher passes to the confirm-publisher. */
export interface PublishEnvelope {
  readonly exchange: string;
  readonly routingKey: string;
  readonly durable: boolean;
  readonly messageId: string;
  readonly contentType: string;
  readonly headers: Readonly<Record<string, unknown>>;
}

/** The confirm-publisher send seam (injectable for tests; the real one sends to the broker). */
export type PublishSender = (
  envelope: PublishEnvelope,
  body: Buffer,
) => Promise<void>;

/**
 * Raised when the broker-confirm wait exceeds the bound. Mapped to a transient
 * publish failure; the .NET twin surfaces the same case as a `TimeoutException`
 * (transient) from the `ConfirmTimeout` linked CTS.
 */
class PublishConfirmTimeoutError extends Error {
  constructor(timeoutMs: number) {
    super(`Publish confirm timed out after ${timeoutMs}ms.`);
    this.name = "PublishConfirmTimeoutError";
  }
}

/**
 * Sends via the confirm-publisher, bounding the broker-confirm wait. In confirm
 * mode `sender` resolves only when the broker acks; if the timeout fires first it
 * rejects with {@link PublishConfirmTimeoutError} so a wedged confirm surfaces as a
 * transient failure instead of hanging the publish call. The underlying send is
 * abandoned on timeout; the confirm-publisher's own reconnect re-drives it.
 */
async function sendWithConfirmTimeout(
  sender: PublishSender,
  envelope: PublishEnvelope,
  body: Buffer,
  confirmTimeoutMs: number,
): Promise<void> {
  // The Promise executor runs synchronously, so `timer` is always assigned before
  // the try below (the `!` asserts that to the compiler).
  let timer: ReturnType<typeof setTimeout>;
  const timeout = new Promise<never>((_, reject) => {
    timer = setTimeout(
      () => reject(new PublishConfirmTimeoutError(confirmTimeoutMs)),
      confirmTimeoutMs,
    );

    // Do not keep the event loop alive for the confirm-timeout guard.
    timer.unref();
  });

  try {
    await Promise.race([sender(envelope, body), timeout]);
  } finally {
    clearTimeout(timer!);
  }
}

/**
 * The auto-encrypting publisher: publishing and encryption are structurally
 * fused. `publish` is the only path to the socket, and its `key` is
 * compile-time constrained to {@link PublishableKey}: a message on an encrypted
 * domain that was not wired into `crypto` is a compile error, and there is no
 * raw-bytes publish overload. The runtime default-deny in
 * {@link composeBody} is the second check for dynamic and fixture paths.
 */
export interface D2Publisher<TWired, TCatalog = typeof MqMessagesCatalog> {
  /**
   * Publishes a message. `key` must be a message the bound catalog contains
   * whose encryption domain is `plaintext` or is wired into the publisher's
   * `crypto` map.
   *
   * @param key The publishable message constant.
   * @param message The message payload (JSON-serialized onto the wire).
   * @returns A successful result on broker confirm; a typed failure otherwise.
   */
  publish<K extends PublishableKey<TWired, TCatalog>>(
    key: K,
    message: object,
  ): Promise<D2Result>;
  /** Closes the underlying confirm-publisher. */
  close(): Promise<void>;
}

/** Options for {@link createPublisher}. */
export interface CreatePublisherOptions<
  TWired extends Partial<DomainCryptoMapOf<TModes>>,
  TCatalog extends Readonly<Record<string, MqMessageDescriptor>> =
    typeof MqMessagesCatalog,
  TModes extends ApplicationDomainModes = Record<never, never>,
> {
  /**
   * The wired composers, keyed by encrypted domain. Passed with `const` so the
   * wired domains' literal keys drive the {@link PublishableKey} constraint.
   */
  readonly crypto: TWired;
  /**
   * The modes of the application's own encryption domains: the domains its
   * catalog publishes on that the framework `EncryptionDomainModes` catalog does
   * not contain. Passed `as const`: each domain's literal mode fixes the composer
   * its `crypto` slot accepts, and the runtime default-deny resolves the
   * domain's mode from it. A domain in neither this map nor the framework
   * catalog is refused.
   * Validated by {@link resolveDomainModes} before the confirm-publisher opens.
   */
  readonly modes?: TModes;
  /** Structured logger (never receives an `Error` directly, for PII safety). */
  readonly logger: ILogger;
  /**
   * The catalog this publisher resolves against, at runtime and at compile
   * time. Omitted, the runtime reads the generated `MqMessagesRegistry` and the
   * constraint reads the generated `MqMessagesCatalog`: the framework's own
   * entries, and nothing else. An application passes the `as const` catalog its
   * own catalog package exports, and its literal `encryption` values then drive
   * the {@link PublishableKey} constraint on {@link D2Publisher.publish}. A
   * fixture catalog drives sealed-domain tests the same way.
   */
  readonly descriptors?: TCatalog;
  /**
   * Broker-confirm wait bound (ms). Defaults to the .NET `ConfirmTimeout` (5s). A
   * wedged confirm surfaces as a transient failure instead of hanging forever.
   */
  readonly confirmTimeoutMs?: number;
}

/**
 * Publish logic, factored out of the broker binding so the compose / default-deny
 * / envelope / telemetry path is unit-testable with a fake {@link PublishSender}.
 * The body is composed once (a resend reuses the exact bytes, so there is no
 * re-encryption under a fresh nonce).
 */
export async function publishVia(
  sender: PublishSender,
  registry: Readonly<Record<string, MqMessageDescriptor>>,
  composers: Readonly<Record<string, Composer | undefined>>,
  logger: ILogger,
  key: string,
  message: object,
  confirmTimeoutMs: number = _CONFIRM_TIMEOUT_MS,
  modes: Readonly<Record<string, EncryptionDomainMode>> = resolveDomainModes(),
): Promise<D2Result> {
  // Default-deny descriptor resolution: an unknown constant never publishes.
  const descriptor = registry[key];

  // Not counted: no send was attempted, and .NET counts nothing for it either
  // (public/packages/dotnet/messaging/rabbitmq/Publishing/RabbitMqMessageBus.cs).
  if (descriptor === undefined) {
    logger.error("publish rejected - unknown message constant", { key });

    return unhandledException();
  }

  const json = new TextEncoder().encode(JSON.stringify(message));
  const span = producerTracer().startSpan("publish");
  span.setAttribute(MessagingActivityTags.MESSAGING_SYSTEM, "rabbitmq");
  span.setAttribute(MessagingActivityTags.MESSAGING_OPERATION_TYPE, "publish");
  span.setAttribute(
    MessagingActivityTags.MESSAGING_DESTINATION_NAME,
    descriptor.exchange,
  );

  // Compose once (reused verbatim on any resend). A compose failure is not
  // counted as a publish failure, because no send was attempted; .NET counts
  // nothing for it either (RabbitMqMessageBus.cs).
  let composed: Awaited<ReturnType<typeof composeBody>>;
  try {
    composed = await composeBody(descriptor, json, composers, modes);
  } catch (err) {
    span.setStatus({ code: SpanStatusCode.ERROR });
    span.end();
    logger.error("publish failed", {
      key,
      exchange: descriptor.exchange,
      error: sanitizedErrorRender(err),
    });

    return serviceUnavailable();
  }

  try {
    const messageId = uuidv7();
    const routingKey = descriptor.defaultRoutingKey ?? "";

    const headers: Record<string, unknown> = {
      [AmqpHeaders.PROTO_TYPE]: descriptor.messageType,
    };

    if (composed.kid !== undefined) {
      headers[AmqpHeaders.ENCRYPTION_KID] = composed.kid;
    }

    span.setAttribute(MessagingActivityTags.MESSAGING_MESSAGE_ID, messageId);
    span.setAttribute(
      MessagingActivityTags.MESSAGING_RABBITMQ_ROUTING_KEY,
      routingKey,
    );

    // Counted per send attempt, before the send, as .NET counts it (RabbitMqMessageBus.cs).
    publishesCounter.add(1);

    await sendWithConfirmTimeout(
      sender,
      {
        exchange: descriptor.exchange,
        routingKey,
        durable: true,
        messageId,
        contentType: _CONTENT_TYPE,
        headers,
      },
      Buffer.from(composed.body),
      confirmTimeoutMs,
    );

    return ok();
  } catch (err) {
    // Only a failed send or confirm reaches this catch.
    publishFailuresCounter.add(1);
    span.setStatus({ code: SpanStatusCode.ERROR });

    if (err instanceof PublishConfirmTimeoutError) {
      logger.error("publish confirm timed out", {
        key,
        exchange: descriptor.exchange,
        timeoutMs: confirmTimeoutMs,
      });
    } else {
      logger.error("publish failed", {
        key,
        exchange: descriptor.exchange,
        error: sanitizedErrorRender(err),
      });
    }

    return serviceUnavailable();
  } finally {
    span.end();
  }
}

/**
 * Creates an auto-encrypting publisher over a live connection. The wired
 * `crypto` map's literal keys and the bound catalog's literal `encryption`
 * values together drive the compile-time {@link PublishableKey} constraint on
 * {@link D2Publisher.publish}, and the application `modes` fix which composer
 * each application domain's `crypto` slot accepts.
 *
 * @param connection A live (auto-reconnecting) connection.
 * @param options The wired composers and logger, plus the catalog to bind and
 *   the application domain modes.
 * @returns The publisher.
 * @throws When `modes` fails {@link resolveDomainModes} validation; nothing is
 *   opened on the connection in that case.
 */
export function createPublisher<
  const TWired extends Partial<DomainCryptoMapOf<TModes>>,
  TCatalog extends Readonly<Record<string, MqMessageDescriptor>> =
    typeof MqMessagesCatalog,
  const TModes extends ApplicationDomainModes = Record<never, never>,
>(
  connection: D2Connection,
  options: CreatePublisherOptions<TWired, TCatalog, TModes>,
): D2Publisher<TWired, TCatalog> {
  const modes = resolveDomainModes(options.modes);
  const registry = options.descriptors ?? MqMessagesRegistry;
  const composers = options.crypto as Readonly<
    Record<string, Composer | undefined>
  >;
  const confirmTimeoutMs = options.confirmTimeoutMs ?? _CONFIRM_TIMEOUT_MS;
  const registration = registerConnectionResource(connection);
  let publisher: ReturnType<typeof registration.vendor.createPublisher>;
  try {
    publisher = registration.vendor.createPublisher({ confirm: true });
    registration.attach({ close: () => publisher.close() });
  } catch (error) {
    registration.failSetup(error);
    throw error;
  }
  const sender: PublishSender = (envelope, body) =>
    publisher.send(envelope, body);

  return {
    publish: ((key: string, message: object) => {
      if (registration.signal.aborted)
        return Promise.resolve(serviceUnavailable());
      return publishVia(
        sender,
        registry,
        composers,
        options.logger,
        key,
        message,
        confirmTimeoutMs,
        modes,
      );
    }) as D2Publisher<TWired, TCatalog>["publish"],
    close: () => registration.close(),
  };
}
