// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { afterEach, expect, it, vi } from "vitest";
import { uuidv7 } from "../src/uuid.js";

afterEach(() => vi.unstubAllGlobals());

it.each([undefined, {}, { getRandomValues: "not-callable" }])(
  "RefusesUuidWithoutRandomness_WhenCryptoIs %j",
  (cryptoStub) => {
    vi.stubGlobal("crypto", cryptoStub);
    expect(() => uuidv7(() => 0)).toThrow(
      "uuidv7: Web Crypto API unavailable (globalThis.crypto.getRandomValues)",
    );
  },
);

it("UsesInjectedRandomness_WhenCryptoIsAvailable", () => {
  const getRandomValues = vi.fn((bytes: Uint8Array) => bytes.fill(0xff));
  vi.stubGlobal("crypto", { getRandomValues });
  expect(uuidv7(() => 0)).toBe("00000000-0000-7fff-bfff-ffffffffffff");
  expect(getRandomValues).toHaveBeenCalledTimes(1);
});
