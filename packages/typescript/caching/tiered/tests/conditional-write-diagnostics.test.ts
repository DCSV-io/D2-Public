// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { expect, it, vi } from "vitest";
import { fail, HttpStatusCode } from "@dcsv-io/d2-result";
import { DefaultTieredCache } from "../src/index.js";
import {
  createBackplaneTestDouble,
  createCapturingTestLogger,
  createDistributedCacheTestDouble,
  createLocalCacheTestDouble,
} from "./tiered-double-test-harness.js";

it.each(["local", "backplane"])(
  "KeepsCommittedRemovalAndLogsUnknown_WhenFailureHasNoCodeIn %s",
  async (failureSite) => {
    const l1 = createLocalCacheTestDouble();
    const l2 = createDistributedCacheTestDouble();
    const logger = createCapturingTestLogger();
    const backplane = createBackplaneTestDouble();
    const noCode = fail({ statusCode: HttpStatusCode.ServiceUnavailable });
    const cache = new DefaultTieredCache({ l1, l2, logger, backplane });
    try {
      await l2.set("fixture-key", "fixture-value");
      if (failureSite === "local") l1.nextResult.remove = noCode;
      else vi.spyOn(backplane, "publishInvalidation").mockResolvedValue(noCode);
      const result = await cache.removeIfEqual("fixture-key", "fixture-value");
      expect(result.success).toBe(true);
      expect(result.data).toBe(true);
      expect(logger.warnings).toEqual([
        {
          message:
            failureSite === "local"
              ? "Tiered cache L1 drop failed."
              : "Tiered cache invalidation publish failed.",
          bindings: {
            operation: "removeIfEqual",
            key: "fixture-key",
            errorCode: "unknown",
          },
        },
      ]);
    } finally {
      await cache[Symbol.asyncDispose]();
    }
  },
);
