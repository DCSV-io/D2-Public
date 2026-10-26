// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { D2Result } from "./d2-result.js";

/**
 * Bubbles a downstream result's failure shape up into a fresh result of the
 * caller's payload type. When `source.success` is true a `RangeError` is
 * thrown, because a fail-shape transfer from a successful result is a
 * programming error.
 */
export function bubbleFail<TOut>(source: D2Result<unknown>): D2Result<TOut> {
  if (source.success)
    throw new RangeError(
      "bubbleFail: source result is success; cannot bubble a failure",
    );
  return new D2Result<TOut>({
    success: false,
    messages: source.messages,
    inputErrors: source.inputErrors,
    statusCode: source.statusCode,
    errorCode: source.errorCode,
    traceId: source.traceId,
    category: source.category,
  });
}

/**
 * Bubbles a downstream result's shape (success OR failure) into a fresh
 * result of the caller's payload type. The `data` parameter overrides the
 * payload. Use it when wrapping a typed downstream call where the payload type
 * changes but every other field of the result passes through unchanged.
 */
export function bubble<TIn, TOut>(
  source: D2Result<TIn>,
  data?: TOut,
): D2Result<TOut> {
  return new D2Result<TOut>({
    success: source.success,
    data,
    messages: source.messages,
    inputErrors: source.inputErrors,
    statusCode: source.statusCode,
    errorCode: source.errorCode,
    traceId: source.traceId,
    category: source.category,
  });
}
