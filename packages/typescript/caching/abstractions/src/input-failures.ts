// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { TK } from "@dcsv-io/d2-i18n-keys";
import {
  inputError,
  validationFailed,
  type D2Result,
} from "@dcsv-io/d2-result";

import type { TokenBucketDecision } from "./token-bucket-decision.js";

/**
 * Pre-built `D2Result` input-failure responses for cache implementations.
 * Keeps the cache surface pure errors-as-values instead of mixing in
 * throws for caller mistakes.
 *
 * Constructors and DI registration still throw, because they have a
 * different lifecycle from per-call input validation.
 *
 * Twin of .NET `DcsvIo.D2.Caching.InputFailures`.
 */
function requiredImpl<T = void>(paramName: string): D2Result<T> {
  return validationFailed<T>({
    inputErrors: [inputError(paramName, [TK.common.errors.NOT_NULL_VIOLATION])],
  });
}

function invalidImpl<T = void>(paramName: string): D2Result<T> {
  return validationFailed<T>({
    inputErrors: [inputError(paramName, [TK.common.errors.VALIDATION_FAILED])],
  });
}

export const InputFailures = {
  /**
   * Builds a `D2Result` / `D2Result<T>` input failure for a missing
   * (null / empty / falsey) required parameter.
   *
   * @param paramName - Parameter name (use a string literal matching
   *   the call-site argument name).
   */
  required: requiredImpl as {
    (paramName: string): D2Result;
    <T>(paramName: string): D2Result<T>;
  },

  /**
   * Builds a `D2Result` / `D2Result<T>` input failure for a present but
   * invalid parameter value (range, non-finite, non-safe-integer, etc.).
   * Does **not** use `NOT_NULL_VIOLATION` - the value is present.
   *
   * @param paramName - Parameter name (use a string literal matching
   *   the call-site argument name).
   */
  invalid: invalidImpl as {
    (paramName: string): D2Result;
    <T>(paramName: string): D2Result<T>;
  },

  /**
   * Builds the input failure that `consumeToken` must return for these
   * arguments, or `undefined` when every one of them is valid.
   *
   * Every flavor returns the same result for the same argument, so the checks
   * live here rather than once per implementation. A cost larger than the burst
   * is refused because the bucket can never hold it, so the member would report
   * a wait that waiting does not satisfy.
   *
   * The interval is refused unless it is a whole number of milliseconds, at
   * least one. Every flavor measures it in whole milliseconds, so an interval
   * that rounded to zero would divide by zero and a fractional one would refill
   * the bucket at a rate the caller did not ask for. The .NET twin refuses on
   * the same bound: its own interval is a `TimeSpan` and it requires a whole
   * millisecond count.
   *
   * @param key - The bucket key.
   * @param refillTokens - Tokens added per interval.
   * @param refillIntervalMs - The interval those tokens are added over.
   * @param burst - The bucket's capacity.
   * @param cost - Tokens this request consumes.
   */
  tokenBucket(
    key: string,
    refillTokens: number,
    refillIntervalMs: number,
    burst: number,
    cost: number,
  ): D2Result<TokenBucketDecision> | undefined {
    if (typeof key !== "string" || key.trim().length === 0) {
      return requiredImpl<TokenBucketDecision>("key");
    }

    if (!Number.isSafeInteger(refillTokens) || refillTokens < 1) {
      return invalidImpl<TokenBucketDecision>("refillTokens");
    }

    if (!Number.isSafeInteger(refillIntervalMs) || refillIntervalMs < 1) {
      return invalidImpl<TokenBucketDecision>("refillIntervalMs");
    }

    if (!Number.isSafeInteger(burst) || burst < 1) {
      return invalidImpl<TokenBucketDecision>("burst");
    }

    if (!Number.isSafeInteger(cost) || cost < 1 || cost > burst) {
      return invalidImpl<TokenBucketDecision>("cost");
    }

    return undefined;
  },
};
