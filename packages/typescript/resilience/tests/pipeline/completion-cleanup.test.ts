// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ResilientPipeline } from "../../src/pipeline/resilient-pipeline.js";
import { RateLimiterLayer } from "../../src/pipeline/rate-limiter-layer.js";
import {
  TimeoutError,
  TimeoutLayer,
} from "../../src/pipeline/timeout-layer.js";

function deferredFixture<T>() {
  let resolve: ((value: T) => void) | undefined;
  let reject: ((error: unknown) => void) | undefined;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  if (resolve === undefined || reject === undefined)
    throw new Error("Promise executor did not initialize fixture");
  return { promise, resolve, reject };
}

describe("Pipeline completion cleanup", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("PreservesNextWaiter_WhenGrantCleanupRunsAfterDequeue", async () => {
    const layer = new RateLimiterLayer(ResilientPipeline.PassThrough, {
      maxConcurrency: 1,
      acquisitionTimeoutMs: 10,
    });
    const holder = deferredFixture<number>();
    const firstWork = deferredFixture<number>();
    const firstStarted = deferredFixture<number>();
    const order: string[] = [];
    const holderCall = layer.execute("holder", () => holder.promise);
    const first = layer.execute("first", () => {
      order.push("first");
      firstStarted.resolve(1);
      return firstWork.promise;
    });
    const second = layer
      .execute("second", async () => {
        order.push("second");
        return 2;
      })
      .catch((error: unknown) => error);
    holder.resolve(0);
    await holderCall;
    await firstStarted.promise;
    expect(order).toEqual(["first"]);
    firstWork.resolve(1);
    await first;
    await vi.advanceTimersByTimeAsync(10);
    expect(await second).toBe(2);
    expect(order).toEqual(["first", "second"]);
    expect(layer.activeCount).toBe(0);
    expect(vi.getTimerCount()).toBe(0);
  });

  it.each(["fulfills", "rejects"] as const)(
    "DisposesOnce_WhenTimedOutInnerWork_%sLater",
    async (completion) => {
      const controller = new AbortController();
      const remove = vi.spyOn(controller.signal, "removeEventListener");
      const work = deferredFixture<number>();
      const aborted = vi.fn();
      const layer = new TimeoutLayer(ResilientPipeline.PassThrough, {
        durationMs: 10,
      });
      const result = layer
        .execute(
          "late-work",
          (signal) => {
            signal?.addEventListener("abort", aborted);
            return work.promise;
          },
          controller.signal,
        )
        .catch((error: unknown) => error);

      await vi.advanceTimersByTimeAsync(10);
      expect(await result).toBeInstanceOf(TimeoutError);
      expect(aborted).toHaveBeenCalledTimes(1);
      expect(remove).toHaveBeenCalledTimes(1);
      if (completion === "fulfills") work.resolve(7);
      else work.reject(new Error("late failure"));
      await work.promise.catch(() => undefined);
      expect(remove).toHaveBeenCalledTimes(1);
      expect(aborted).toHaveBeenCalledTimes(1);
      expect(vi.getTimerCount()).toBe(0);
    },
  );

  it.each(["abort", "timeout"] as const)(
    "PreservesQueuedSuccessor_WhenEarlierWaiterEndsBy_%s",
    async (completion) => {
      const layer = new RateLimiterLayer(ResilientPipeline.PassThrough, {
        maxConcurrency: 1,
        acquisitionTimeoutMs: 10,
      });
      const holder = deferredFixture<number>();
      const holderCall = layer.execute("holder", () => holder.promise);
      const controller = new AbortController();
      const removed = vi.spyOn(controller.signal, "removeEventListener");
      const canceledOp = vi.fn(async () => "canceled");
      const canceled = layer
        .execute("canceled", canceledOp, controller.signal)
        .catch((error: unknown) => error);
      if (completion === "timeout") await vi.advanceTimersByTimeAsync(5);
      const successorController = new AbortController();
      const successorRemoved = vi.spyOn(
        successorController.signal,
        "removeEventListener",
      );
      const successor = layer.execute(
        "successor",
        async () => "successor",
        successorController.signal,
      );
      if (completion === "abort") controller.abort();
      else await vi.advanceTimersByTimeAsync(5);
      expect(await canceled).toMatchObject({
        name: completion === "abort" ? "AbortError" : "RateLimitRejectedError",
      });
      expect(canceledOp).not.toHaveBeenCalled();
      expect(removed).toHaveBeenCalledTimes(1);
      holder.resolve(1);
      await holderCall;
      expect(await successor).toBe("successor");
      expect(successorRemoved).toHaveBeenCalledTimes(1);
      expect(layer.activeCount).toBe(0);
      expect(vi.getTimerCount()).toBe(0);
    },
  );
});
