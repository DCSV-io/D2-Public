// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import type { ILogger, LogBindings } from "@dcsv-io/d2-logging";
import {
  type IPropagatedContext,
  PropagatedContextSerializer,
} from "@dcsv-io/d2-request-context-abstractions";

import { type ConsumedMessage } from "../src/subscribing/consumed-message.js";

/**
 * Encodes a propagated context into the `x-d2-context` wire value:
 * base64url of the JSON, the same value that the .NET
 * `PropagatedContextSerializer.Encode` and the gRPC context interceptor produce.
 */
export function encodePropagatedHeader(ctx: IPropagatedContext): string {
  return Buffer.from(
    PropagatedContextSerializer.serialize(ctx),
    "utf8",
  ).toString("base64url");
}

/** One captured log call. */
export interface LogEntry {
  readonly level: string;
  readonly message: string;
  readonly bindings?: LogBindings;
}

/** A recording {@link ILogger} for asserting the consumer's observability. */
export class RecordingLogger implements ILogger {
  readonly entries: LogEntry[] = [];

  private record(level: string, message: string, bindings?: LogBindings): void {
    this.entries.push({ level, message, bindings });
  }

  trace(message: string, bindings?: LogBindings): void {
    this.record("trace", message, bindings);
  }

  debug(message: string, bindings?: LogBindings): void {
    this.record("debug", message, bindings);
  }

  info(message: string, bindings?: LogBindings): void {
    this.record("info", message, bindings);
  }

  warn(message: string, bindings?: LogBindings): void {
    this.record("warn", message, bindings);
  }

  error(message: string, bindings?: LogBindings): void {
    this.record("error", message, bindings);
  }

  fatal(message: string, bindings?: LogBindings): void {
    this.record("fatal", message, bindings);
  }

  child(): ILogger {
    return this;
  }

  /** True if any recorded message contains `fragment`. */
  logged(fragment: string): boolean {
    return this.entries.some((e) => e.message.includes(fragment));
  }
}

/** Builds a {@link ConsumedMessage} with default values for tests. */
export function makeMessage(
  overrides: Partial<ConsumedMessage> = {},
): ConsumedMessage {
  return {
    body: Buffer.from(JSON.stringify({ hello: "world" }), "utf8"),
    messageId: "0192f8c1-1111-7000-8000-000000000001",
    headers: {},
    deliveryTag: 1,
    redelivered: false,
    exchange: "example.orders.placed",
    routingKey: "",
    ...overrides,
  };
}

const SAMPLE_TRACE_ID = "4bf92f3577b34da6a3ce929d0e0e4736";
const SAMPLE_SPAN_ID = "00f067aa0ba902b7";

/** A valid W3C traceparent that contains a known trace id (for linkage assertions). */
export const SAMPLE_TRACEPARENT = `00-${SAMPLE_TRACE_ID}-${SAMPLE_SPAN_ID}-01`;
export const SAMPLE_PRODUCER_TRACE_ID = SAMPLE_TRACE_ID;

// Attempt-budget guard against a stuck test, the TypeScript counterpart of the
// .NET `_POLL_ATTEMPT_BUDGET` (DlqTests.cs and PublishConsumeRoundTripTests.cs).
// Each iteration waits a fixed poll interval. The budget caps attempts and
// never elapsed wall-clock time, so under load the effective wait grows with
// the slowdown, and no `Date.now()` deadline expires while the awaited work is
// still making progress. The budget is not a success-path deadline: a healthy
// delivery resolves in a few attempts, and the budget ends only a test that is
// permanently stuck. The 120s vitest `testTimeout` is the outer limit of the
// framework and is not part of any assertion's success path.
const _POLL_INTERVAL_MS = 100;
const _POLL_ATTEMPT_BUDGET = 600;

/**
 * Polls `probe` until it returns a value other than `undefined`, and returns
 * that value. Throws when the attempt budget is spent.
 */
export async function waitForValue<T>(
  probe: () => Promise<T | undefined> | T | undefined,
): Promise<T> {
  for (let attempt = 0; attempt < _POLL_ATTEMPT_BUDGET; attempt++) {
    const value = await probe();
    if (value !== undefined) return value;
    await new Promise((r) => setTimeout(r, _POLL_INTERVAL_MS));
  }

  throw new Error(
    `predicate not satisfied within ${_POLL_ATTEMPT_BUDGET} poll attempts`,
  );
}

/** Polls `predicate` until it returns true. Throws when the budget is spent. */
export async function waitUntil(predicate: () => boolean): Promise<void> {
  await waitForValue(() => (predicate() ? true : undefined));
}

/**
 * Waits on the attempt budget for `promise` to settle, then returns its value
 * or rethrows its rejection.
 */
export async function waitForSettled<T>(promise: Promise<T>): Promise<T> {
  let settled = false;
  promise.then(
    () => {
      settled = true;
    },
    () => {
      settled = true;
    },
  );
  await waitUntil(() => settled);
  return promise;
}
