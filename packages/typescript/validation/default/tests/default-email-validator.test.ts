// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { TK } from "@dcsv-io/d2-i18n-keys";
import { describe, expect, it } from "vitest";

import {
  DefaultEmailValidator,
  EMAIL_PATTERN,
} from "../src/default-email-validator.js";

const validator = new DefaultEmailValidator();

describe("DefaultEmailValidator - adversarial + property", () => {
  it("rejects undefined input (falsey path)", () => {
    const result = validator.validate(undefined);
    expect(result.success).toBe(false);
    expect(result.errorCode).toBe("GEN_VALIDATION_FAILED");
    expect(result.inputErrors[0]?.field).toBe("email");
    expect(result.inputErrors[0]?.errors[0]?.key).toBe(
      TK.common.validation.EMAIL_INVALID.key,
    );
  });

  it("rejects empty string", () => {
    expect(validator.validate("").success).toBe(false);
  });

  it("rejects whitespace-only input", () => {
    expect(validator.validate("   ").success).toBe(false);
  });

  it.each([
    "userexample.com",
    "a@@b.com",
    "a b@c.com",
    "a@b",
    "a@b.com.",
    "münchen@example.com",
    "user@exämple.com",
  ])("rejects malformed address %s", (bad) => {
    expect(validator.validate(bad).success).toBe(false);
  });

  it.each([".a@b.com", "a.@b.com", "a..b@c.com"])(
    // The shared structural pattern permits dots in any local-part position
    // (practical limits of RFC 5321/5322, not a full dot-atom validator). Both
    // runtimes accept these addresses; this test pins that, so a stricter regex
    // edit must be a deliberate cross-language decision.
    "accepts local-part dot-positioning %s",
    (addr) => {
      expect(validator.validate(addr).success).toBe(true);
    },
  );

  it("normalizes (trim + lowercase) on success", () => {
    const result = validator.validate("  User@Example.COM  ");
    expect(result.success).toBe(true);
    expect(result.data).toBe("user@example.com");
  });

  it("is idempotent - validating a normalized value returns it unchanged", () => {
    const first = validator.validate("  User@Example.COM  ");
    expect(first.success).toBe(true);
    const second = validator.validate(first.data!);
    expect(second.success).toBe(true);
    expect(second.data).toBe(first.data);
  });

  it("returns quickly on a pathological long input (ReDoS guard)", () => {
    // A long run of local-part chars with a single '@' and no valid domain -
    // the kind of input a backtracking-vulnerable regex would hang on. The
    // anchored, bounded pattern must short-circuit.
    const pathological = "a".repeat(50_000) + "@";
    const start = performance.now();
    const result = validator.validate(pathological);
    const elapsedMs = performance.now() - start;
    expect(result.success).toBe(false);
    // 500 ms ceiling, 10x the 50 ms budget this path is expected to meet. A
    // backtracking regex would exceed it on a 50k-char input; the bounded
    // pattern finishes well under it.
    expect(elapsedMs).toBeLessThan(500);
  });

  it("exposes the cross-language EMAIL_PATTERN literal", () => {
    // Pin the exact literal. The cross-language parity suite asserts this is
    // byte-identical with the .NET `EMAIL_PATTERN`; pinning it here too means
    // any local edit fails fast without waiting on the cross-runtime suite.
    // The regex literal is long and is not wrapped.
    expect(EMAIL_PATTERN).toBe(
      "^(?=.{1,254}$)[A-Z0-9._%+\\-]{1,64}@[A-Z0-9](?:[A-Z0-9\\-]{0,61}[A-Z0-9])?(?:\\.[A-Z0-9](?:[A-Z0-9\\-]{0,61}[A-Z0-9])?)+$",
    );
  });
});
