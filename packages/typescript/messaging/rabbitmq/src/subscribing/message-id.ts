// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { AmqpHeaders } from "@dcsv-io/d2-headers-amqp";

// Hard cap on accepted message-id length. The publisher emits a 36-char
// UUIDv7. An attacker who controls a queue the subscriber reads could send a
// 64KB id and bloat the idempotency store key. An id longer than the cap is
// handled as no id (idempotency is skipped and the delivery is still acked).
// Mirrors the .NET `SubscriberChannel._MAX_MESSAGE_ID_LENGTH`.
const _MAX_MESSAGE_ID_LENGTH = 128;
const _CONTROL_CHAR_MAX = 0x1f;
const _DEL_CHAR = 0x7f;
const _COLON = 0x3a;
const _SPACE = 0x20;

/**
 * Reads and validates the delivery's message id. It mirrors the .NET
 * `SubscriberChannel.ReadMessageId`. It prefers the AMQP `message-id` property
 * and falls back to the `message-id` header. It rejects lengths outside the
 * accepted range (key injection, store-memory DoS) and any control character,
 * `:` or space that could split the store's namespace. A rejected id returns
 * `undefined`, and the delivery still acks, without idempotency.
 *
 * @param amqpMessageId The AMQP `message-id` property (or undefined).
 * @param headers The delivery's AMQP headers (or undefined).
 */
export function readValidatedMessageId(
  amqpMessageId: string | undefined,
  headers: Readonly<Record<string, unknown>> | undefined,
): string | undefined {
  const raw = readRaw(amqpMessageId, headers);
  if (raw === undefined) return undefined;

  if (raw.length === 0 || raw.length > _MAX_MESSAGE_ID_LENGTH) return undefined;

  for (let i = 0; i < raw.length; i++) {
    const code = raw.charCodeAt(i);
    if (
      code <= _CONTROL_CHAR_MAX ||
      code === _DEL_CHAR ||
      code === _COLON ||
      code === _SPACE
    ) {
      return undefined;
    }
  }

  return raw;
}

function readRaw(
  amqpMessageId: string | undefined,
  headers: Readonly<Record<string, unknown>> | undefined,
): string | undefined {
  if (typeof amqpMessageId === "string" && amqpMessageId.length > 0)
    return amqpMessageId;

  const fromHeader = headers?.[AmqpHeaders.MESSAGE_ID];
  if (typeof fromHeader === "string") return fromHeader;
  if (fromHeader instanceof Uint8Array)
    return Buffer.from(fromHeader).toString("utf8");

  return undefined;
}
