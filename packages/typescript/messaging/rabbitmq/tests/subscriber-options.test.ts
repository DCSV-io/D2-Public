// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { Temporal } from "temporal-polyfill";
import { describe, expect, it } from "vitest";
import { normalizeSubscriberOptions } from "../src/subscribing/subscriber-options.js";
import { dlqLifetimeContractFixture } from "./fixtures/dlq-lifetime-contract-fixture.js";

// This import does not compile without the subscriber option boundary.
describe("Subscriber duration validation", () => {
  it("Normalize_SharedFixture_MatchesDefaults", () => {
    expect(normalizeSubscriberOptions()).toEqual(
      dlqLifetimeContractFixture.defaults,
    );
  });

  it("Normalize_WithoutOverrides_UsesMatchingDefaults", () => {
    expect(normalizeSubscriberOptions()).toEqual({
      republishMs: 5000,
      cleanupMs: 1000,
    });
  });

  it.each([1, 2147483647])(
    "Normalize_WholeMillisecondsAtBounds_Accepts(%s)",
    (milliseconds) => {
      const duration = Temporal.Duration.from({ milliseconds });
      expect(
        normalizeSubscriberOptions({
          dlqRepublishTimeout: duration,
          dlqCleanupTimeout: duration,
        }),
      ).toEqual({ republishMs: milliseconds, cleanupMs: milliseconds });
    },
  );

  it.each([
    Temporal.Duration.from({ milliseconds: 0 }),
    Temporal.Duration.from({ milliseconds: -1 }),
    Temporal.Duration.from({ milliseconds: 2147483648 }),
    Temporal.Duration.from({ nanoseconds: 1000001 }),
    Temporal.Duration.from({ years: 1 }),
    Temporal.Duration.from({ months: 1 }),
    Temporal.Duration.from({ weeks: 1 }),
    Temporal.Duration.from({ days: 1 }),
  ])("Normalize_InvalidDuration_RejectsBothOptions(%s)", (duration) => {
    expect(() =>
      normalizeSubscriberOptions({ dlqRepublishTimeout: duration }),
    ).toThrow(RangeError);
    expect(() =>
      normalizeSubscriberOptions({ dlqCleanupTimeout: duration }),
    ).toThrow(RangeError);
  });

  it("Normalize_MixedSignDynamicInput_Rejects", () => {
    const duration = { seconds: 1, milliseconds: -1 } as Temporal.Duration;
    expect(() =>
      normalizeSubscriberOptions({ dlqRepublishTimeout: duration }),
    ).toThrow(RangeError);
  });

  it("Normalize_LaterOptionsMutation_DoesNotChangeSnapshot", () => {
    const options = {
      dlqRepublishTimeout: Temporal.Duration.from({ seconds: 2 }),
    };
    const normalized = normalizeSubscriberOptions(options);
    options.dlqRepublishTimeout = Temporal.Duration.from({ seconds: 3 });
    expect(normalized.republishMs).toBe(2000);
    expect(Object.isFrozen(normalized)).toBe(true);
  });
});
