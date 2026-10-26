// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

/**
 * Retry policy options. Mirrors .NET `DcsvIo.D2.Resilience.RetryOptions<T>`.
 * Defaults live in {@link RETRY_DEFAULTS}.
 */
export interface RetryOptions<T = unknown> {
  /** Maximum total attempts (including the first). Must be >= 1. */
  readonly maxAttempts: number;
  /** Base delay between attempts (ms). */
  readonly baseDelayMs: number;
  /** Multiplier applied each attempt. `2` -> 100ms, 200ms, 400ms, ... */
  readonly backoffMultiplier: number;
  /** Cap applied to per-attempt delay. */
  readonly maxDelayMs: number;
  /**
   * Random jitter expressed as a fractional multiplier of the computed
   * delay (e.g. `0.2` -> +/-20% randomization).
   */
  readonly jitter: number;
  /**
   * Predicate that decides whether a thrown error or returned `T` value is
   * retried. Defaults to `isTransient` if omitted; `cancellation` is NEVER
   * retried regardless of predicate (by convention, as in .NET).
   */
  readonly shouldRetry?: (errOrValue: T | unknown) => boolean;
  /** Convenience: classify whether an error is transient. */
  readonly isTransient?: (err: unknown) => boolean;
  /**
   * Optional override for the per-attempt sleep helper; a fake clock can
   * replace it.
   */
  readonly delayFunc?: (ms: number, signal?: AbortSignal) => Promise<void>;
}
