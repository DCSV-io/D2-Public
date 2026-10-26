// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import type { ILogger } from "@dcsv-io/d2-logging";

// Twin of .NET TieredCacheLog EventId meanings: Warning on L1 invalidation
// failure and on L1 write failure after L2 success. Structured redis-style
// message strings (not LoggerMessage template parity). Log entries contain
// errorCode strings only, with no Exception. This package has no OTel meters.

const L1_INVALIDATION_FAILED_MESSAGE =
  "Tiered cache L1 invalidation handler failed.";

const L1_WRITE_FAILED_AFTER_L2_SUCCESS_MESSAGE =
  "Tiered cache L1 write failed after L2 success.";

const L1_DROP_FAILED_MESSAGE = "Tiered cache L1 drop failed.";

const INVALIDATION_PUBLISH_FAILED_MESSAGE =
  "Tiered cache invalidation publish failed.";

/**
 * Sentinel errorCode when a failed L1 result has no code, so the binding stays
 * inside its closed set.
 */
export const TIERED_ERROR_CODE_UNKNOWN = "unknown";

/**
 * Closed-set write-path operation names for the tiered L1-failure warning log
 * entries.
 * This package defines these names nowhere else.
 */
export const TieredCacheOp = {
  SET: "set",
  SET_MANY: "setMany",
  REMOVE: "remove",
  REMOVE_MANY: "removeMany",
  SET_IF_EQUAL: "setIfEqual",
  REMOVE_IF_EQUAL: "removeIfEqual",
} as const;

/** Closed-set type for {@link TieredCacheOp} values. */
export type TieredCacheOpName =
  (typeof TieredCacheOp)[keyof typeof TieredCacheOp];

/**
 * Logs Warning when the backplane invalidation handler's L1 remove fails.
 */
export function logL1InvalidationFailed(
  logger: ILogger,
  key: string,
  errorCode: string,
): void {
  logger.warn(L1_INVALIDATION_FAILED_MESSAGE, { key, errorCode });
}

/**
 * Logs Warning when an L1 write/remove fails after L2 already succeeded.
 *
 * @param operation - One of {@link TieredCacheOp} values.
 * @param keyOrCount - Single key, or `"N entries"` / `"N keys"` bulk form.
 */
export function logL1WriteFailedAfterL2Success(
  logger: ILogger,
  operation: TieredCacheOpName,
  keyOrCount: string,
  errorCode: string,
): void {
  logger.warn(L1_WRITE_FAILED_AFTER_L2_SUCCESS_MESSAGE, {
    operation,
    keyOrCount,
    errorCode,
  });
}

/**
 * Logs Warning when an L1 drop fails on a path that drops L1 whatever L2
 * returned, so the message says nothing about L2 having succeeded.
 *
 * @param operation - One of {@link TieredCacheOp} values.
 */
export function logL1DropFailed(
  logger: ILogger,
  operation: TieredCacheOpName,
  key: string,
  errorCode: string,
): void {
  logger.warn(L1_DROP_FAILED_MESSAGE, { operation, key, errorCode });
}

/**
 * Logs Warning when a conditional write took effect at L2 and the invalidation
 * publish that follows it failed. The other instances keep their local copy
 * of the superseded value until it expires.
 *
 * @param operation - One of {@link TieredCacheOp} values.
 */
export function logInvalidationPublishFailed(
  logger: ILogger,
  operation: TieredCacheOpName,
  key: string,
  errorCode: string,
): void {
  logger.warn(INVALIDATION_PUBLISH_FAILED_MESSAGE, {
    operation,
    key,
    errorCode,
  });
}
