// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

// Only these x-death reasons reflect a consumer-side retry cycle. Broker-side
// flow control (maxlen, delivery_limit) is excluded, because counting it would
// trip RETRIES_EXHAUSTED too early.
const RETRY_CYCLE_REASONS = new Set(["expired", "rejected"]);

/**
 * Counts redelivery attempts from the broker's `x-death` header. It mirrors
 * the .NET `SubscriberChannel.ReadAttemptCount`. RabbitMQ adds one entry per
 * `(queue, reason)` the message has cycled through, and the `count` field
 * increments on each further cycle. The function sums `count` across entries
 * whose `reason` is `expired` (retry-tier TTL expiry) or `rejected` (consumer
 * NACK). It returns 0 for a first delivery and for a missing or malformed
 * header. It fails open, because a broker quirk must not strand a message in
 * retry forever.
 *
 * @param headers The delivery's AMQP headers (or undefined).
 */
export function readAttemptCount(
  headers: Readonly<Record<string, unknown>> | undefined,
): number {
  if (headers === undefined) return 0;

  const raw = headers["x-death"];
  if (!Array.isArray(raw)) return 0;

  let total = 0;
  for (const entry of raw) {
    if (entry === null || typeof entry !== "object") continue;

    const record = entry as Record<string, unknown>;
    const reason = record["reason"];
    if (typeof reason !== "string" || !RETRY_CYCLE_REASONS.has(reason))
      continue;

    total += toCount(record["count"]);
  }

  return total;
}

function toCount(value: unknown): number {
  if (typeof value === "number" && Number.isFinite(value))
    return Math.trunc(value);
  if (typeof value === "bigint") return Number(value);

  return 0;
}
