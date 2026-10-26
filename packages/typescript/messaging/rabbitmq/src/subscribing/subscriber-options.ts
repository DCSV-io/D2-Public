// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { Temporal } from "temporal-polyfill";

/** Independent deadlines for dead-letter publication and retained cleanup. */
export interface RabbitMqSubscriberOptions {
  /** Admission-through-confirm deadline; defaults to five seconds. */
  readonly dlqRepublishTimeout?: Temporal.Duration;
  /** Additional wait after forced termination; defaults to one second. */
  readonly dlqCleanupTimeout?: Temporal.Duration;
}

/** Immutable timer-boundary values captured before transport allocation. */
export interface NormalizedSubscriberOptions {
  readonly republishMs: number;
  readonly cleanupMs: number;
}

/** Validates elapsed durations without rounding or accepting calendar units. */
export function normalizeSubscriberOptions(
  options: RabbitMqSubscriberOptions = {},
): Readonly<NormalizedSubscriberOptions> {
  return Object.freeze({
    republishMs: milliseconds(
      options.dlqRepublishTimeout ?? Temporal.Duration.from({ seconds: 5 }),
    ),
    cleanupMs: milliseconds(
      options.dlqCleanupTimeout ?? Temporal.Duration.from({ seconds: 1 }),
    ),
  });
}

function milliseconds(input: Temporal.Duration): number {
  const duration = Temporal.Duration.from(input);
  if (
    duration.years !== 0 ||
    duration.months !== 0 ||
    duration.weeks !== 0 ||
    duration.days !== 0
  )
    throw new RangeError(
      "Subscriber duration must not contain calendar units.",
    );
  const value = duration.total({ unit: "milliseconds" });
  if (!Number.isInteger(value) || value < 1 || value > 2147483647)
    throw new RangeError(
      "Subscriber duration must be whole milliseconds from 1 through 2147483647.",
    );
  return value;
}
