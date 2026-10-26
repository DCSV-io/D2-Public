// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

/**
 * The decision one {@link ICacheAtomic.consumeToken} returned.
 *
 * Twin of .NET `DcsvIo.D2.Caching.TokenBucketDecision`, whose retry-after is a
 * `TimeSpan` where this one is milliseconds, as every other span on this
 * surface is.
 */
export interface TokenBucketDecision {
  /**
   * Whether the bucket held the cost. The cost was consumed when it did, and
   * nothing was consumed when it did not.
   */
  readonly admitted: boolean;

  /**
   * How long the bucket needs to refill to the cost the caller asked for, in
   * milliseconds. Zero on an admitted request.
   */
  readonly retryAfterMs: number;
}
