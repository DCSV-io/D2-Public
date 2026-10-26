// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { type Counter, metrics, type Tracer, trace } from "@opentelemetry/api";

/**
 * OTel source / meter name for the messaging-rabbitmq consumer runtime.
 * Byte-identical to the .NET `MessagingTelemetry.SOURCE_NAME` so consume
 * spans + metrics from both runtimes aggregate under one instrumentation
 * scope.
 */
export const MESSAGING_SOURCE_NAME = "DcsvIo.D2.Messaging.RabbitMq";

const meter = metrics.getMeter(MESSAGING_SOURCE_NAME);

/**
 * Consumer-side failures to republish to the dead-letter exchange: the
 * failure header was lost and the consumer fell back to a plain
 * NACK-no-requeue. Mirrors the .NET
 * `d2.messaging.rabbitmq.dlq_republish_failures` counter.
 */
export const dlqRepublishFailuresCounter: Counter = meter.createCounter(
  "d2.messaging.rabbitmq.dlq_republish_failures",
  {
    unit: "{republish}",
    description:
      "Consumer-side failures to republish to the dead-letter exchange: " +
      "the failure header was lost and the consumer fell back to a nack " +
      "without requeue.",
  },
);

/**
 * Post-handler-success idempotency-mark write failures. The delivery
 * processor increments it when the mark write fails. The rabbitmq-client
 * library sends the ack, so this runtime does not observe a failed ack; the
 * .NET `d2.messaging.rabbitmq.ack_failures` counter has the same name and
 * also counts failed acks. The idempotency mark prevents duplicate work on
 * broker redelivery.
 */
export const ackFailuresCounter: Counter = meter.createCounter(
  "d2.messaging.rabbitmq.ack_failures",
  {
    unit: "{ack}",
    description:
      "Consumer-side failures after a successful handler run: the idempotency " +
      "mark could not be written. A failed ack is not observed here.",
  },
);

/**
 * Publish attempts: one increment per send, made before the send, as the
 * .NET `d2.messaging.rabbitmq.publishes` counter of the same name counts it.
 * This publisher has no retry loop, so it makes one attempt per publish.
 */
export const publishesCounter: Counter = meter.createCounter(
  "d2.messaging.rabbitmq.publishes",
  {
    unit: "{publish}",
    description: "Total publish attempts (including retries).",
  },
);

/**
 * Publisher-side failures: a broker send or confirm failure. An unknown
 * message constant and a compose (encrypt/seal) failure are not counted,
 * because no send was attempted. The .NET
 * `d2.messaging.rabbitmq.publish_failures` counter in `RabbitMqMessageBus.cs`
 * counts every publish that fails after the retry loop, retried or not.
 */
export const publishFailuresCounter: Counter = meter.createCounter(
  "d2.messaging.rabbitmq.publish_failures",
  {
    unit: "{publish}",
    description: "Publisher-side failures: a broker send or confirm failure.",
  },
);

/** Returns the shared Consumer-kind tracer for the messaging runtime. */
export function consumerTracer(): Tracer {
  return trace.getTracer(MESSAGING_SOURCE_NAME);
}

/** Returns the shared Producer-kind tracer for the messaging runtime. */
export function producerTracer(): Tracer {
  return trace.getTracer(MESSAGING_SOURCE_NAME);
}
