// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { existsSync, readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import type { CountryCode } from "@dcsv-io/d2-geo-abstractions";
import { describe, expect, it } from "vitest";

import { DefaultPostalCodeValidator } from "../src/default-postal-code-validator.js";
import { POSTAL_CODE_REGEXES } from "../src/postal-code-regexes.g.js";

const GB = "GB" as CountryCode;
const UK = "UK" as CountryCode;
const validator = new DefaultPostalCodeValidator();

/** Walks up from this file to the folder whose `contracts/` holds the dataset. */
function readSharedDataset(): Record<string, unknown> {
  const relative = join("contracts", "validation", "postal-code-regexes.json");
  let dir = dirname(fileURLToPath(import.meta.url));
  for (;;) {
    const candidate = join(dir, relative);
    if (existsSync(candidate)) {
      return JSON.parse(readFileSync(candidate, "utf8")) as Record<
        string,
        unknown
      >;
    }
    const parent = resolve(dir, "..");
    if (parent === dir) {
      throw new Error(`could not find ${relative} above ${import.meta.url}`);
    }
    dir = parent;
  }
}

describe("POSTAL_CODE_REGEXES (generated from the shared dataset)", () => {
  it("is frozen", () => {
    expect(Object.isFrozen(POSTAL_CODE_REGEXES)).toBe(true);
  });

  it("holds exactly the country entries of the shared JSON dataset, with equal patterns", () => {
    const dataset = Object.fromEntries(
      Object.entries(readSharedDataset()).filter(
        ([key]) => !key.startsWith("$"),
      ),
    );

    expect(Object.keys(POSTAL_CODE_REGEXES).sort()).toEqual(
      Object.keys(dataset).sort(),
    );
    expect({ ...POSTAL_CODE_REGEXES }).toEqual(dataset);
  });

  it("holds no metadata key", () => {
    expect(
      Object.keys(POSTAL_CODE_REGEXES).filter((k) => k.startsWith("$")),
    ).toEqual([]);
  });

  it("holds only upper-case two-letter country keys with non-empty compilable patterns", () => {
    for (const [key, pattern] of Object.entries(POSTAL_CODE_REGEXES)) {
      expect(key).toMatch(/^[A-Z]{2}$/);
      expect(pattern.length).toBeGreaterThan(0);
      expect(() => new RegExp(pattern, "i")).not.toThrow();
    }
  });
});

describe("DefaultPostalCodeValidator - GB and UK letter positions", () => {
  it.each([
    ["GB", GB],
    ["UK", UK],
  ])("accepts a valid %s code", (_name, country) => {
    const result = validator.validate("EC1A 1AA", country);
    expect(result.success).toBe(true);
    expect(result.data).toBe("EC1A 1AA");
  });

  it.each([
    ["GB", GB],
    ["UK", UK],
  ])("accepts a lower-case %s code and normalizes it", (_name, country) => {
    const result = validator.validate("  ec1a 1zz ", country);
    expect(result.success).toBe(true);
    expect(result.data).toBe("EC1A 1ZZ");
  });

  it.each(["^", "[", "\\", "]", "_", "`"])(
    "rejects '%s' in a letter position for GB and UK",
    (ch) => {
      expect(validator.validate(`EC1A 1${ch}B`, GB).success).toBe(false);
      expect(validator.validate(`EC1A 1B${ch}`, GB).success).toBe(false);
      expect(validator.validate(`EC1A 1${ch}B`, UK).success).toBe(false);
      expect(validator.validate(`${ch}C1A 1AA`, GB).success).toBe(false);
    },
  );
});
