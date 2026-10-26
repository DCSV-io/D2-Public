// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import {
  type AsyncMessage,
  type Connection,
  type Consumer,
  ConsumerStatus,
} from "rabbitmq-client";
import type { ILogger } from "@dcsv-io/d2-logging";
import { sanitizedErrorRender } from "@dcsv-io/d2-logging";

import { type BodyOpener, PlaintextBodyOpener } from "./body-opener.js";
import { type ConsumedMessage } from "./consumed-message.js";
import {
  DeliveryAction,
  DeliveryProcessor,
  type MessageHandler,
  type RepublishFn,
} from "./delivery-processor.js";
import { type IMessageIdempotencyStore } from "../idempotency/message-idempotency-store.js";
import { readValidatedMessageId } from "./message-id.js";
import {
  createCompletion,
  registerConnectionResource,
  type D2Connection,
} from "../connection/managed-connection.js";
import { DlqRepublisher } from "./dlq-republisher.js";
import {
  normalizeSubscriberOptions,
  type RabbitMqSubscriberOptions,
} from "./subscriber-options.js";
import {
  type ExchangeDeclareParams,
  planTopology,
  type QueueBindParams,
  type QueueDeclareParams,
} from "../topology/topology-plan.js";
import {
  resolveQueueName,
  type SubscriptionDescriptor,
} from "../topology/subscription-descriptor.js";

/** Everything needed to register one subscriber against a live connection. */
export interface SubscribeOptions {
  /** A live (auto-reconnecting) connection. */
  readonly connection: D2Connection;
  /** The subscription contract (queue / exchange / pattern / prefetch / ...). */
  readonly descriptor: SubscriptionDescriptor;
  /** The message handler. */
  readonly handler: MessageHandler;
  /** Structured logger. It never receives an `Error` directly, for PII safety. */
  readonly logger: ILogger;
  /** Body-decompose seam (defaults to {@link PlaintextBodyOpener}). */
  readonly opener?: BodyOpener;
  /** Idempotency store - required only when `descriptor.idempotency` is true. */
  readonly store?: IMessageIdempotencyStore;
  /** Explicit fanout-queue suffix, for a deterministic queue name. */
  readonly queueSuffix?: string;
  /** Independent dead-letter publication and cleanup deadlines. */
  readonly subscriberOptions?: RabbitMqSubscriberOptions;
}

/** A running subscription: its resolved queue, a readiness promise, and close. */
export interface Subscription {
  /** The on-broker queue name (suffix-resolved for fanout-exclusive patterns). */
  readonly queueName: string;
  /** Resolves once topology is declared and the consumer has started. */
  readonly ready: Promise<void>;
  /** Stops consuming and closes the consumer + republish channels. */
  close(): Promise<void>;
}

/**
 * Registers a manual-ack subscriber on a live connection. It is the TS twin of
 * the .NET `SubscriberChannel` + `ConsumerHostedService`. It declares the exact
 * .NET topology (primary + `{q}.dlx` + `{q}.dlq` + optional retry tiers), starts
 * a competing/fanout consumer, and routes every delivery through the
 * {@link DeliveryProcessor}. The connection reconnects automatically. On
 * reconnect the consumer re-declares its primary queue + exchanges + bindings,
 * and this function re-declares the DLQ / retry-tier queues on each
 * `connection` event.
 *
 * @param options The subscription registration.
 */
export function subscribe(options: SubscribeOptions): Subscription {
  const { descriptor, handler, logger } = options;
  if (!Number.isInteger(descriptor.prefetch) || descriptor.prefetch <= 0)
    throw new RangeError("Prefetch must be positive.");
  const normalized = normalizeSubscriberOptions(options.subscriberOptions);
  const resolvedQueueName = resolveQueueName(descriptor, options.queueSuffix);
  const plan = planTopology(descriptor, resolvedQueueName);
  const opener = options.opener ?? new PlaintextBodyOpener();
  const registration = registerConnectionResource(options.connection);
  const connection = registration.vendor;
  let publisher: DlqRepublisher;
  let processor: DeliveryProcessor;
  // A connection close waits for the registration's setup until a resource is
  // attached, so a throw before the attach fails that setup.
  try {
    publisher = new DlqRepublisher(
      registration.options,
      normalized,
      descriptor.prefetch,
      logger,
    );
    const republish: RepublishFn = async (dlxName, body, headers, metadata) => {
      const outcome = await publisher.republish(
        { dlxName, body, headers, ...metadata },
        registration.signal,
      );
      if (outcome !== "confirmed")
        throw new Error(`DLQ republish outcome: ${outcome}`);
    };

    processor = new DeliveryProcessor({
      descriptor,
      resolvedQueueName,
      handler,
      opener,
      republish,
      logger,
      store: options.store,
    });
  } catch (error) {
    registration.failSetup(error);
    throw error;
  }

  // ConsumerProps cannot express the DLQ + retry-tier queues (or their
  // bindings), so they are declared explicitly, up front and on every reconnect.
  const auxQueues = plan.queues.filter((q) => q.queue !== resolvedQueueName);
  const auxBindings = plan.bindings.filter(
    (b) => b.queue !== resolvedQueueName,
  );
  const declareAux = (): Promise<void> =>
    applyAuxTopology(connection, plan.exchanges, auxQueues, auxBindings);

  // Re-declare the aux topology on every reconnect. The handler is kept in a
  // variable so `close()` can deregister it. The `connection` is long-lived and
  // shared across subscribers, so a listener that is not removed would keep
  // firing, and redeclare a closed subscriber's topology, for the life of the
  // process.
  const onReconnect = (): void => {
    if (registration.signal.aborted) return;
    declareAux().catch((err: unknown) =>
      logger.error("aux topology declaration failed", {
        queue: resolvedQueueName,
        error: sanitizedErrorRender(err),
      }),
    );
  };

  let consumer: Consumer | undefined;
  let startup: Promise<void> = Promise.resolve();
  const retained: {
    startupObservation?: Promise<void>;
    resourceWork?: Promise<void>;
    failureCleanup?: Promise<void>;
  } = {};
  let dlqClose: Promise<void> | undefined;
  const stoppingError = new Error("Subscription is stopping.");
  const readiness = createCompletion<void>();
  const resources = createCompletion<void>();
  const readinessObservation = readiness.promise.then(
    () => {},
    () => {},
  );
  const onReady = (): void => readiness.resolve();
  const stop = (): void => {
    dlqClose ??= publisher.close();
    readiness.reject(stoppingError);
  };
  const closeOwned = (): Promise<void> => {
    stop();
    connection.off("connection", onReconnect);
    const closeWork = (async () => {
      const results = await Promise.allSettled([
        startup,
        consumer?.close(),
        dlqClose,
      ]);
      consumer?.off("ready", onReady);
      registration.signal.removeEventListener("abort", stop);
      await readinessObservation;
      const errors = results.flatMap((result, index) =>
        result.status === "rejected" &&
        !(index === 0 && result.reason === stoppingError)
          ? [result.reason]
          : [],
      );
      if (errors.length !== 0)
        throw new AggregateError(errors, "Subscription cleanup failed.");
    })();
    retained.resourceWork = (async () => {
      const results = await Promise.allSettled([
        closeWork,
        publisher.resourceCompletion,
      ]);
      const errors = results.flatMap((result) =>
        result.status === "rejected" ? [result.reason] : [],
      );
      if (errors.length !== 0)
        resources.reject(
          new AggregateError(errors, "Subscription resources failed."),
        );
      else resources.resolve();
    })();
    return closeWork;
  };
  registration.attach({
    close: closeOwned,
    resourceCompletion: resources.promise,
  });
  registration.signal.addEventListener("abort", stop, { once: true });

  try {
    if (registration.signal.aborted) throw stoppingError;
    connection.on("connection", onReconnect);
    consumer = connection.createConsumer(
      {
        queue: resolvedQueueName,
        lazy: true,
        requeue: false,
        concurrency: descriptor.concurrency ?? descriptor.prefetch,
        qos: { prefetchCount: descriptor.prefetch },
        queueOptions: toQueueDeclare(plan.primaryQueue),
        exchanges: plan.exchanges.map(toExchangeDeclare),
        queueBindings: plan.bindings
          .filter((b) => b.queue === resolvedQueueName)
          .map(toQueueBind),
      },
      async (msg: AsyncMessage) => {
        let consumed: ConsumedMessage;
        try {
          consumed = toConsumedMessage(msg);
        } catch (error: unknown) {
          logger.error("consumer raw body unavailable", {
            queue: resolvedQueueName,
            error: sanitizedErrorRender(error),
          });
          return ConsumerStatus.DROP;
        }
        const action = await processor.process(consumed);
        return action === DeliveryAction.Drop
          ? ConsumerStatus.DROP
          : ConsumerStatus.ACK;
      },
    );

    consumer.on("error", (err: unknown) =>
      logger.error("consumer error", {
        queue: resolvedQueueName,
        error: sanitizedErrorRender(err),
      }),
    );

    logger.info("subscriber starting", {
      queue: resolvedQueueName,
      pattern: descriptor.pattern,
      prefetch: descriptor.prefetch,
    });

    consumer.on("ready", onReady);
    startup = (async (): Promise<void> => {
      await declareAux();
      if (registration.signal.aborted) throw stoppingError;
      consumer!.start();
      await readiness.promise;
    })();
    retained.startupObservation = startup.catch((error: unknown) => {
      readiness.reject(error);
      retained.failureCleanup = registration
        .close()
        .catch((cleanupError: unknown) =>
          logger.error("subscription cleanup failed", {
            error: sanitizedErrorRender(cleanupError),
          }),
        );
    });
  } catch (error) {
    readiness.reject(error);
    retained.failureCleanup = registration
      .close()
      .catch((cleanupError: unknown) =>
        logger.error("subscription cleanup failed", {
          error: sanitizedErrorRender(cleanupError),
        }),
      );
    throw error;
  }

  return {
    queueName: resolvedQueueName,
    ready: readiness.promise,
    close: () => registration.close(),
  };
}

/** Maps a rabbitmq-client `AsyncMessage` to the pipeline's normalized shape. */
export function toConsumedMessage(msg: AsyncMessage): ConsumedMessage {
  if (!Buffer.isBuffer(msg.rawBody))
    throw new TypeError("RabbitMQ message rawBody must be a Buffer.");
  const headers = (msg.headers ?? {}) as Readonly<Record<string, unknown>>;
  return {
    body: Buffer.from(msg.rawBody),
    messageId: readValidatedMessageId(msg.messageId, headers),
    originalMessageId: msg.messageId,
    headers,
    deliveryTag: msg.deliveryTag,
    redelivered: msg.redelivered,
    exchange: msg.exchange,
    routingKey: msg.routingKey,
    contentType: msg.contentType,
    contentEncoding: msg.contentEncoding,
    correlationId: msg.correlationId,
  };
}

async function applyAuxTopology(
  connection: Connection,
  exchanges: readonly ExchangeDeclareParams[],
  queues: readonly QueueDeclareParams[],
  bindings: readonly QueueBindParams[],
): Promise<void> {
  for (const e of exchanges)
    await connection.exchangeDeclare(toExchangeDeclare(e));
  for (const q of queues) await connection.queueDeclare(toQueueDeclare(q));
  for (const b of bindings) await connection.queueBind(toQueueBind(b));
}

function toExchangeDeclare(e: ExchangeDeclareParams): {
  exchange: string;
  type: string;
  durable: boolean;
  autoDelete: boolean;
} {
  return {
    exchange: e.exchange,
    type: e.type,
    durable: e.durable,
    autoDelete: e.autoDelete,
  };
}

function toQueueDeclare(q: QueueDeclareParams): {
  queue: string;
  durable: boolean;
  exclusive: boolean;
  autoDelete: boolean;
  arguments?: Record<string, unknown>;
} {
  return {
    queue: q.queue,
    durable: q.durable,
    exclusive: q.exclusive,
    autoDelete: q.autoDelete,
    arguments: q.arguments === undefined ? undefined : { ...q.arguments },
  };
}

function toQueueBind(b: QueueBindParams): {
  queue: string;
  exchange: string;
  routingKey: string;
} {
  return { queue: b.queue, exchange: b.exchange, routingKey: b.routingKey };
}
