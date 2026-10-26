// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  CONSUME_TOKEN,
  INCREMENT_WITH_OPTIONAL_TTL,
  RELEASE_LOCK_IF_OWNER,
  REMOVE_IF_EQUAL,
  SET_ADD_WITH_OPTIONAL_TTL,
  SET_IF_EQUAL,
} from "../src/redis-lua-scripts.js";

/**
 * The exported script bodies, listed here so each one is read by a test.
 *
 * This suite asserts what the package itself defines: every script body is
 * exported, is a non-empty trimmed script, and refers to the key it acts on.
 * Whether each body matches its .NET twin is a cross-runtime question, asserted
 * outside this package.
 */
const EXPORTED = {
  CONSUME_TOKEN,
  INCREMENT_WITH_OPTIONAL_TTL,
  RELEASE_LOCK_IF_OWNER,
  REMOVE_IF_EQUAL,
  SET_ADD_WITH_OPTIONAL_TTL,
  SET_IF_EQUAL,
} as const;

describe("Lua scripts", () => {
  it.each(Object.entries(EXPORTED))(
    "luaScripts_eachExportedBody_isANonEmptyTrimmedScript (%s)",
    (_name, body) => {
      expect(typeof body).toBe("string");
      expect(body.length).toBeGreaterThan(0);
      expect(body).toBe(body.trim());
    },
  );

  it.each(Object.entries(EXPORTED))(
    "luaScripts_eachExportedBody_actsOnTheKeyItIsCalledWith (%s)",
    (_name, body) => {
      // Every script here is key-scoped, so each body must reference KEYS[1];
      // a script that references no key would act on whatever key the caller's
      // connection last used, not on the key it is called with.
      expect(body).toContain("KEYS[1]");
    },
  );
});
