// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  tryParseTruthyUndefEnum,
  tryParseTruthyUndefInt,
  tryParseTruthyUndefUuid,
} from "../src/parse.js";
import { EMPTY_UUID } from "../src/regex.js";

describe("tryParseTruthyUndefUuid", () => {
  it("returns canonical lowercase UUID on success", () => {
    expect(
      tryParseTruthyUndefUuid("550E8400-E29B-41D4-A716-446655440000"),
    ).toBe("550e8400-e29b-41d4-a716-446655440000");
  });

  it.each([
    ["null", null],
    ["undefined", undefined],
    ["empty", ""],
    ["whitespace", "   "],
    ["empty UUID", EMPTY_UUID],
    ["malformed", "not-a-uuid"],
    ["wrong shape", "550e8400-e29b-41d4-a716"],
    ["wrong type chars", "550e8400-e29b-41d4-a716-44665544000Z"],
  ])("%s -> undefined", (_label, input) => {
    expect(tryParseTruthyUndefUuid(input)).toBeUndefined();
  });
});

describe("tryParseTruthyUndefInt", () => {
  it.each([
    ["happy positive", "42", 42],
    ["happy negative", "-7", -7],
    ["zero", "0", 0],
    ["padded", "  42  ", 42],
  ])("%s -> %s", (_label, input, expected) => {
    expect(tryParseTruthyUndefInt(input)).toBe(expected);
  });

  it.each([
    ["null", null],
    ["undefined", undefined],
    ["empty", ""],
    ["whitespace", "   "],
    ["float", "3.14"],
    ["scientific", "1e3"],
    ["alpha", "abc"],
    ["mixed", "42a"],
    ["sign-only", "-"],
    ["one past MAX_SAFE_INTEGER", "9007199254740992"],
    ["one past MIN_SAFE_INTEGER", "-9007199254740992"],
    ["16 nines", "9999999999999999"],
    ["309 digits", "1".repeat(309)],
  ])("%s -> undefined", (_label, input) => {
    expect(tryParseTruthyUndefInt(input)).toBeUndefined();
  });

  it.each([
    ["MAX_SAFE_INTEGER", "9007199254740991", Number.MAX_SAFE_INTEGER],
    ["MIN_SAFE_INTEGER", "-9007199254740991", Number.MIN_SAFE_INTEGER],
  ])("%s at the bound -> exact", (_label, input, expected) => {
    expect(tryParseTruthyUndefInt(input)).toBe(expected);
  });
});

describe("tryParseTruthyUndefEnum", () => {
  const Color = { Red: "red", Green: "green", Blue: "blue" } as const;

  it.each([
    ["exact match", "Red", "Red"],
    ["case-insensitive", "red", "Red"],
    ["uppercase", "BLUE", "Blue"],
    ["padded", "  Green  ", "Green"],
  ])("%s -> %s", (_label, input, expected) => {
    expect(tryParseTruthyUndefEnum(Color, input)).toBe(expected);
  });

  it.each([
    ["null", null],
    ["undefined", undefined],
    ["empty", ""],
    ["whitespace", "   "],
    ["unknown", "Yellow"],
  ])("%s -> undefined", (_label, input) => {
    expect(tryParseTruthyUndefEnum(Color, input)).toBeUndefined();
  });
});
