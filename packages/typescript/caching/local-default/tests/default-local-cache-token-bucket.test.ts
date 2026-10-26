// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { DefaultLocalCache } from "../src/index.js";

/**
 * The token-bucket primitive on the in-process implementation, driven by an
 * injected clock so the refill is observed rather than waited for. The Redis
 * implementation runs the same algorithm as a Lua script and is exercised
 * against a real server in `tests/integration`.
 */
describe("DefaultLocalCache token bucket", () => {
  const MINUTE_MS = 60_000;

  it("consumeToken_clockRecoversFromRollback_preservesAccrual", async () => {
    let now = 1000;
    const cache = newCache(() => now);
    expect(
      (await cache.consumeToken("rollback", 1, 1000, 1)).data?.admitted,
    ).toBe(true);
    now = 0;
    expect(
      (await cache.consumeToken("rollback", 1, 1000, 1)).data?.admitted,
    ).toBe(false);
    now = 1000;
    expect(
      (await cache.consumeToken("rollback", 1, 1000, 1)).data?.admitted,
    ).toBe(false);
    now = 1999;
    expect(
      (await cache.consumeToken("rollback", 1, 1000, 1)).data?.retryAfterMs,
    ).toBe(1);
    now = 2000;
    expect(
      (await cache.consumeToken("rollback", 1, 1000, 1)).data?.admitted,
    ).toBe(true);
    cache.dispose();
  });

  it("consumeToken_clockRollsBackWithRemainder_preservesFraction", async () => {
    let now = 0;
    const cache = newCache(() => now);
    await cache.consumeToken("remainder", 1, 3000, 1);
    now = 1;
    await cache.consumeToken("remainder", 1, 3000, 1);
    now = 0;
    await cache.consumeToken("remainder", 1, 3000, 1);
    now = 2999;
    expect(
      (await cache.consumeToken("remainder", 1, 3000, 1)).data?.retryAfterMs,
    ).toBe(1);
    now = 3000;
    expect(
      (await cache.consumeToken("remainder", 1, 3000, 1)).data?.admitted,
    ).toBe(true);
    cache.dispose();
  });

  it("consumeToken_aBucketNobodyUsed_admitsItsWholeBurst", async () => {
    const cache = newCache();
    let admitted = 0;

    for (let i = 0; i < 6; i++) {
      const decision = await cache.consumeToken("bucket", 10, MINUTE_MS, 5);

      if (decision.data?.admitted === true) {
        admitted++;
      }
    }

    expect(admitted).toBe(5);
  });

  it("consumeToken_pastTheBurst_reportsTheWaitForOneToken", async () => {
    // A fixed clock, as the .NET twin uses: the wait for one token is exact
    // only when no time passes between the calls that spend the burst.
    const cache = newCache(() => 1_000);

    for (let i = 0; i < 5; i++) {
      await cache.consumeToken("bucket", 10, MINUTE_MS, 5);
    }

    const refused = await cache.consumeToken("bucket", 10, MINUTE_MS, 5);

    expect(refused.data?.admitted).toBe(false);

    // Ten tokens a minute is one token every six seconds.
    expect(refused.data?.retryAfterMs).toBe(6_000);
  });

  it("consumeToken_afterTheWaitItReported_admitsAgain", async () => {
    let now = 1_000;
    const cache = newCache(() => now);

    for (let i = 0; i < 5; i++) {
      await cache.consumeToken("bucket", 10, MINUTE_MS, 5);
    }

    const refused = await cache.consumeToken("bucket", 10, MINUTE_MS, 5);
    now += refused.data?.retryAfterMs ?? 0;
    const next = await cache.consumeToken("bucket", 10, MINUTE_MS, 5);

    expect(next.data?.admitted).toBe(true);
  });

  /**
   * The property a fixed window does not have. A window of one minute admitting
   * ten requests admits ten at the end of one window and ten more a second
   * later at the start of the next; a bucket that refills by elapsed time has
   * only what the elapsed seconds added.
   */
  it("consumeToken_twoSecondsAfterTheBurstIsSpent_admitsNothing", async () => {
    let now = 1_000;
    const cache = newCache(() => now);

    for (let i = 0; i < 10; i++) {
      await cache.consumeToken("bucket", 10, MINUTE_MS, 10);
    }

    now += 2_000;
    let admitted = 0;

    for (let i = 0; i < 10; i++) {
      const decision = await cache.consumeToken("bucket", 10, MINUTE_MS, 10);

      if (decision.data?.admitted === true) {
        admitted++;
      }
    }

    expect(admitted).toBe(0);
  });

  it("consumeToken_aFullIntervalAfterTheBurstIsSpent_hasRefilledTheBurst", async () => {
    let now = 1_000;
    const cache = newCache(() => now);

    for (let i = 0; i < 10; i++) {
      await cache.consumeToken("bucket", 10, MINUTE_MS, 10);
    }

    now += MINUTE_MS;
    let admitted = 0;

    for (let i = 0; i < 11; i++) {
      const decision = await cache.consumeToken("bucket", 10, MINUTE_MS, 10);

      if (decision.data?.admitted === true) {
        admitted++;
      }
    }

    expect(admitted).toBe(10);
  });

  it("consumeToken_aRefillPastTheCapacity_isClampedToIt", async () => {
    let now = 1_000;
    const cache = newCache(() => now);

    await cache.consumeToken("bucket", 10, MINUTE_MS, 3);
    now += 60 * MINUTE_MS;
    let admitted = 0;

    for (let i = 0; i < 4; i++) {
      const decision = await cache.consumeToken("bucket", 10, MINUTE_MS, 3);

      if (decision.data?.admitted === true) {
        admitted++;
      }
    }

    expect(admitted).toBe(3);
  });

  it("consumeToken_aClockSteppedBackwards_refillsNothing", async () => {
    let now = 10 * MINUTE_MS;
    const cache = newCache(() => now);

    for (let i = 0; i < 3; i++) {
      await cache.consumeToken("bucket", 10, MINUTE_MS, 3);
    }

    now -= 5 * MINUTE_MS;
    const decision = await cache.consumeToken("bucket", 10, MINUTE_MS, 3);

    expect(decision.data?.admitted).toBe(false);
  });

  it("consumeToken_aCostOverOne_consumesThatMuch", async () => {
    const cache = newCache();

    const first = await cache.consumeToken("bucket", 10, MINUTE_MS, 5, 3);
    const second = await cache.consumeToken("bucket", 10, MINUTE_MS, 5, 3);

    expect(first.data?.admitted).toBe(true);
    expect(second.data?.admitted).toBe(false);
  });

  it("consumeToken_aBucketLeftAlone_expiresAfterARefillFromEmpty", async () => {
    let now = 1_000;
    const cache = newCache(() => now);

    await cache.consumeToken("bucket", 10, MINUTE_MS, 10);

    // A refill from empty plus one interval of slack.
    expect((await cache.getTtl("bucket")).data).toBe(2 * MINUTE_MS);
  });

  it("consumeToken_aKeyHoldingSomethingElse_answersConflict", async () => {
    const cache = newCache();
    await cache.set("bucket", "not a bucket");

    const result = await cache.consumeToken("bucket", 10, MINUTE_MS, 5);

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(409);
  });

  it("consumeToken_anArgumentTheBucketCannotUse_isRefusedByName", async () => {
    const cache = newCache();

    const result = await cache.consumeToken("", 10, MINUTE_MS, 5);

    expect(result.success).toBe(false);
    expect(result.inputErrors[0]?.field).toBe("key");
  });

  it("consumeToken_twoKeys_areTwoBuckets", async () => {
    const cache = newCache();

    await cache.consumeToken("one", 10, MINUTE_MS, 1);
    const other = await cache.consumeToken("two", 10, MINUTE_MS, 1);

    expect(other.data?.admitted).toBe(true);
  });

  /**
   * Ten tokens a minute is one thousandth of a token every six milliseconds, so
   * a caller arriving every millisecond arrives twice inside every step the
   * refill can take. The rate over the whole run must still be the configured
   * one: twelve seconds of a ten-a-minute rate is two tokens.
   */
  it("consumeToken_arrivalsFasterThanOneThousandthAccrues_keepsTheRate", async () => {
    let now = 1_000;
    const cache = newCache(() => now);

    for (let i = 0; i < 5; i++) {
      await cache.consumeToken("bucket", 10, MINUTE_MS, 5);
    }

    let admitted = 0;

    for (let ms = 0; ms < 12_000; ms++) {
      now += 1;
      const decision = await cache.consumeToken("bucket", 10, MINUTE_MS, 5);

      if (decision.data?.admitted === true) {
        admitted++;
      }
    }

    expect(admitted).toBe(2);
  });

  it("consumeToken_arrivalsFasterThanOneThousandthAccrues_reportsAShrinkingWait", async () => {
    let now = 1_000;
    const cache = newCache(() => now);

    for (let i = 0; i < 5; i++) {
      await cache.consumeToken("bucket", 10, MINUTE_MS, 5);
    }

    const first = await cache.consumeToken("bucket", 10, MINUTE_MS, 5);

    for (let ms = 0; ms < 3_000; ms++) {
      now += 1;
      await cache.consumeToken("bucket", 10, MINUTE_MS, 5);
    }

    const later = await cache.consumeToken("bucket", 10, MINUTE_MS, 5);

    // The wait that a refused call reports is only usable if the arrivals that
    // took place during it did not push the bucket's own instant forward.
    expect(later.data?.retryAfterMs).toBe(
      (first.data?.retryAfterMs ?? 0) - 3_000,
    );
  });

  it("consumeToken_afterDispose_throws", async () => {
    const cache = newCache();
    cache.dispose();

    await expect(cache.consumeToken("k", 1, 1_000, 1)).rejects.toThrow(
      "DefaultLocalCache is disposed.",
    );
  });

  function newCache(clock?: () => number): DefaultLocalCache {
    return new DefaultLocalCache({ defaultExpirationMs: 0 }, clock);
  }
});
