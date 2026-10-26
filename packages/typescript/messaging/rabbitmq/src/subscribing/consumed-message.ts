// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

/**
 * A normalized incoming delivery: the transport-neutral shape the delivery
 * pipeline processes. The consume boundary builds it from the rabbitmq-client
 * `AsyncMessage`, so the core pipeline can be unit-tested without a live
 * broker.
 */
export interface ConsumedMessage {
  /** Raw body bytes (opaque `application/octet-stream`). */
  readonly body: Buffer;
  /** Validated message id, or undefined when absent / rejected. */
  readonly messageId?: string;
  /** Original AMQP message-id property, independent of the processing identity. */
  readonly originalMessageId?: string;
  /** All AMQP headers on the delivery. */
  readonly headers: Readonly<Record<string, unknown>>;
  /** Broker-assigned delivery tag. */
  readonly deliveryTag: number;
  /** True if the message was previously delivered. */
  readonly redelivered: boolean;
  /** Exchange the message was published to. */
  readonly exchange: string;
  /** Routing key the message was published with. */
  readonly routingKey: string;
  /** AMQP content-type property (carried forward on DLQ republish). */
  readonly contentType?: string;
  /** Original AMQP content-encoding property. */
  readonly contentEncoding?: string;
  /** AMQP correlation-id property (carried forward on DLQ republish). */
  readonly correlationId?: string;
}

/**
 * Reads an AMQP header as a UTF-8 string. Header values arrive decoded from
 * rabbitmq-client (longstr -> string), but a byte-typed header arrives as a
 * `Uint8Array` / `Buffer`; both normalize to a string. Any other type, or an
 * absent header, yields `undefined`.
 *
 * @param headers The delivery's AMQP headers.
 * @param name The header name.
 */
export function readHeaderString(
  headers: Readonly<Record<string, unknown>>,
  name: string,
): string | undefined {
  const raw = headers[name];
  if (typeof raw === "string") return raw;
  if (raw instanceof Uint8Array) return Buffer.from(raw).toString("utf8");

  return undefined;
}
