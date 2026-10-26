// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, it, expect } from "vitest";
import { TK } from "@dcsv-io/d2-i18n-keys";

const here = dirname(fileURLToPath(import.meta.url));

function findMessagesEnUs(): string {
  // Walk up until the monorepo layout (public/contracts) or the public clone
  // layout (contracts/) is found.
  let dir = here;

  for (let i = 0; i < 12; i++) {
    const monorepo = join(dir, "public", "contracts", "messages", "en-US.json");
    const publicOss = join(dir, "contracts", "messages", "en-US.json");

    if (existsSync(monorepo)) {
      return monorepo;
    }

    if (existsSync(publicOss)) {
      return publicOss;
    }

    const parent = dirname(dir);

    if (parent === dir) {
      break;
    }

    dir = parent;
  }

  throw new Error(
    `Could not locate contracts/messages/en-US.json from ${here}`,
  );
}

const EN_US_PATH = findMessagesEnUs();

/** Decompose a flat i18n key - mirrors the emitter's decomposeKey logic. */
function decompose(
  key: string,
): { domain: string; category: string; constant: string } | undefined {
  const segments = key.split("_").filter((s) => s.length > 0);
  if (segments.length < 3) return undefined;
  return {
    domain: segments[0]!,
    category: segments[1]!,
    constant: segments.slice(2).join("_").toUpperCase(),
  };
}

describe("TK catalog", () => {
  // -------------------------------------------------------------------------
  // Explicit spot-checks (regression pins for the canonical examples).
  // -------------------------------------------------------------------------

  it("TK.common.errors.REQUEST_FAILED resolves to the correct key string", () => {
    expect(TK.common.errors.REQUEST_FAILED.key).toBe(
      "common_errors_REQUEST_FAILED",
    );
  });

  it("TK.common.errors.CANCELED resolves to the correct key string", () => {
    expect(TK.common.errors.CANCELED.key).toBe("common_errors_CANCELED");
  });

  it("TK.common.pagination.CURSOR_INVALID resolves to the correct key string", () => {
    expect(TK.common.pagination.CURSOR_INVALID.key).toBe(
      "common_pagination_CURSOR_INVALID",
    );
  });

  it("TK leaf values are TKMessage instances whose key matches the original key", () => {
    // Each leaf is a TKMessage instance (`{ key }`). The constant is the
    // message, so it can be added to D2Result.messages directly.
    expect(typeof TK.common.errors.NOT_FOUND).toBe("object");
    expect(TK.common.errors.NOT_FOUND.key).toBe("common_errors_NOT_FOUND");
    expect(TK.auth.errors.UNAUTHORIZED.key).toBe("auth_errors_UNAUTHORIZED");
    expect(TK.common.validation.EMAIL_INVALID.key).toBe(
      "common_validation_EMAIL_INVALID",
    );
  });

  // -------------------------------------------------------------------------
  // Full-catalog exhaustive check: every decomposable key in en-US.json is
  // reachable in TK at its computed path and equals the key string.
  // -------------------------------------------------------------------------

  it("every decomposable key in en-US.json is reachable in TK and equals the key string", () => {
    const catalog = JSON.parse(readFileSync(EN_US_PATH, "utf8")) as Record<
      string,
      string
    >;
    const tkAny = TK as Record<
      string,
      Record<string, Record<string, { key: string }>>
    >;

    for (const key of Object.keys(catalog)) {
      const entry = decompose(key);
      if (entry === undefined) {
        // Fewer than 3 segments (for example "$schema"): skipped on purpose.
        continue;
      }

      const { domain, category, constant } = entry;
      const domainObj = tkAny[domain];
      expect(domainObj, `TK.${domain} must exist (key: ${key})`).toBeDefined();

      const categoryObj = domainObj?.[category];
      expect(
        categoryObj,
        `TK.${domain}.${category} must exist (key: ${key})`,
      ).toBeDefined();

      const leaf = categoryObj?.[constant];
      expect(
        leaf,
        `TK.${domain}.${category}.${constant} must exist (key: ${key})`,
      ).toBeDefined();
      expect(
        leaf?.key,
        `TK.${domain}.${category}.${constant}.key must equal the original key string`,
      ).toBe(key);
    }
  });

  // -------------------------------------------------------------------------
  // Structural invariants.
  // -------------------------------------------------------------------------

  it("TK carries the open domains and no product domain", () => {
    // The open catalog is emitted from the open message root only, so it
    // contains no domain declared outside that root.
    const domains = Object.keys(TK).sort();
    expect(domains).toContain("auth");
    expect(domains).toContain("common");
    expect(domains).toContain("contacts");
    expect(domains).toContain("geo");
    expect(domains).not.toContain("webclient");
  });

  it("TK.common has errors, pagination, time, ui, and validation categories", () => {
    const categories = Object.keys(TK.common).sort();
    expect(categories).toContain("errors");
    expect(categories).toContain("pagination");
    expect(categories).toContain("time");
    expect(categories).toContain("ui");
    expect(categories).toContain("validation");
  });

  it("every leaf value is a TKMessage with a non-empty key", () => {
    const tkAny = TK as Record<
      string,
      Record<string, Record<string, { key: string }>>
    >;
    for (const [domain, domainObj] of Object.entries(tkAny))
      for (const [category, categoryObj] of Object.entries(domainObj))
        for (const [constant, leaf] of Object.entries(categoryObj)) {
          expect(
            typeof leaf === "object" &&
              typeof leaf.key === "string" &&
              leaf.key.length > 0,
            `TK.${domain}.${category}.${constant} must be a TKMessage with a non-empty key`,
          ).toBe(true);
        }
  });
});
