// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { afterEach, expect, it, vi } from "vitest";
import { RetryHelper } from "../src/retry/retry-helper.js";

afterEach(() => vi.useRealTimers());

it("StopsBeforeDelay_WhenFailedAttemptAbortsCaller", async () => {
  const controller = new AbortController();
  const operation = vi.fn(async () => {
    controller.abort();
    throw new TypeError("fetch failed");
  });
  await expect(
    RetryHelper.retryAsync(operation, {}, controller.signal),
  ).rejects.toMatchObject({ name: "AbortError" });
  expect(operation).toHaveBeenCalledTimes(1);
});

it("CancelsPendingDelay_WhenCallerAborts", async () => {
  vi.useFakeTimers();
  const controller = new AbortController();
  const operation = vi.fn(async () => {
    throw new TypeError("fetch failed");
  });
  const result = RetryHelper.retryAsync(
    operation,
    { baseDelayMs: 1000, jitter: 0 },
    controller.signal,
  );
  const rejected = expect(result).rejects.toMatchObject({ name: "AbortError" });
  await Promise.resolve();
  expect(vi.getTimerCount()).toBe(1);
  controller.abort();
  await rejected;
  expect(vi.getTimerCount()).toBe(0);
  expect(operation).toHaveBeenCalledTimes(1);
});
