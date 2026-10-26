// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import type { CircuitState } from "./circuit-state.js";

/**
 * Circuit breaker configuration. It mirrors .NET `CircuitBreakerOptions`, plus
 * the `isFailure` / `onStateChange` seams that .NET passes as
 * `CircuitBreaker<T>` ctor parameters. They are defined here so the single
 * options object holds the full breaker configuration.
 */
export interface CircuitBreakerOptions<T = unknown> {
  /** Failures (back-to-back) needed to trip from Closed -> Open. */
  readonly failureThreshold: number;
  /** Cooldown (ms) that the circuit stays Open before it transitions to HalfOpen. */
  readonly cooldownMs: number;
  /**
   * Clock function returning a millisecond timestamp; defaults to `Date.now`,
   * and a fake can replace it.
   */
  readonly nowFunc?: () => number;
  /**
   * Value-based failure predicate. Thrown errors ALWAYS count as failures;
   * this predicate adds value-based failures for operations that surface a
   * failure WITHOUT throwing (e.g. `(r) => !r.success` for a `D2Result`).
   * A returned value satisfying the predicate increments the failure counter
   * and is then returned to the caller unchanged (it is NOT re-thrown).
   * Mirrors .NET `CircuitBreaker<T>`'s `isFailure` ctor parameter.
   */
  readonly isFailure?: (result: T) => boolean;
  /**
   * Callback fired synchronously on every REAL state transition (an
   * idempotent Closed->Closed on repeated success does NOT fire). Mirrors
   * .NET `CircuitBreaker<T>`'s `onStateChange` ctor parameter. This is the
   * observability seam, because the breaker emits no spans, metrics or logs of
   * its own.
   *
   * Warning: a THROWING callback propagates out of `execute()` and REPLACES
   * the upstream error (or the operation's return). A throwing logger or metric
   * emitter here therefore hides the upstream failure behind its own error,
   * which makes an outage harder to diagnose. Keep the body to non-throwing
   * log/metric calls, or wrap it in your own try/catch. Mirrors the .NET
   * remark.
   */
  readonly onStateChange?: (from: CircuitState, to: CircuitState) => void;
}
