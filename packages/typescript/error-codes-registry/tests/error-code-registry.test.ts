// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  buildRegistry,
  type ErrorCategory,
  type ErrorCodeFactoryShape,
  type ErrorCodeInfo,
} from "../src/error-code-registry.js";
import { errorCodeRegistry } from "../src/generated/error-code-registry.g.js";
import { TK } from "@dcsv-io/d2-i18n-keys";

// ---------------------------------------------------------------------------
// buildRegistry - unit tests
// ---------------------------------------------------------------------------

describe("buildRegistry - resolution API", () => {
  const entries: readonly ErrorCodeInfo[] = [
    {
      code: "GEN_NOT_FOUND",
      httpStatus: 404,
      category: "not_found",
      userMessageKey: TK.common.errors.NOT_FOUND,
      factoryName: "NotFound",
      factoryShape: "standard",
      doc: "Not found.",
      domain: "common",
      owner: "gen",
      isDeprecated: false,
    },
    {
      code: "LIB_AUTH_BEARER_MISSING",
      httpStatus: 401,
      category: "validation_failure",
      userMessageKey: TK.auth.errors.UNAUTHORIZED,
      factoryName: "BearerMissing",
      factoryShape: "standard",
      doc: "Bearer missing.",
      domain: "auth",
      owner: "lib",
      isDeprecated: false,
    },
  ];
  const registry = buildRegistry(entries);

  it("resolve: known code returns full 10-field ErrorCodeInfo", () => {
    const info = registry.resolve("GEN_NOT_FOUND");
    expect(info).toBeDefined();
    expect(info?.code).toBe("GEN_NOT_FOUND");
    expect(info?.httpStatus).toBe(404);
    expect(info?.category).toBe("not_found");
    expect(info?.userMessageKey).toBe(TK.common.errors.NOT_FOUND);
    expect(info?.factoryName).toBe("NotFound");
    expect(info?.factoryShape).toBe("standard");
    expect(info?.doc).toBe("Not found.");
    expect(info?.domain).toBe("common");
    expect(info?.owner).toBe("gen");
    expect(info?.isDeprecated).toBe(false);
  });

  it("resolve: unknown code returns undefined (hard not-found)", () => {
    expect(registry.resolve("NOPE")).toBeUndefined();
    expect(registry.resolve("NOT_EXISTING")).toBeUndefined();
  });

  it("resolve: empty string returns undefined", () => {
    expect(registry.resolve("")).toBeUndefined();
  });

  it("resolve: whitespace-only string returns undefined", () => {
    expect(registry.resolve("   ")).toBeUndefined();
  });

  it("resolve: lowercase code returns undefined (ordinal case-sensitive)", () => {
    // Codes are SCREAMING_SNAKE - lowercase must not resolve.
    expect(registry.resolve("not_found")).toBeUndefined();
    expect(registry.resolve("auth_bearer_missing")).toBeUndefined();
  });

  it("resolve: mixed-case code returns undefined", () => {
    expect(registry.resolve("Not_Found")).toBeUndefined();
  });

  it("has: known code returns true", () => {
    expect(registry.has("GEN_NOT_FOUND")).toBe(true);
    expect(registry.has("LIB_AUTH_BEARER_MISSING")).toBe(true);
  });

  it("has: unknown code returns false", () => {
    expect(registry.has("NOPE")).toBe(false);
  });

  it("has: empty string returns false", () => {
    expect(registry.has("")).toBe(false);
  });

  it("has: lowercase returns false (ordinal case-sensitive)", () => {
    expect(registry.has("not_found")).toBe(false);
  });

  it("all: contains every registered entry", () => {
    expect(registry.all).toHaveLength(2);
    expect(registry.all.map((e) => e.code)).toContain("GEN_NOT_FOUND");
    expect(registry.all.map((e) => e.code)).toContain(
      "LIB_AUTH_BEARER_MISSING",
    );
  });

  it("all: is frozen (immutable)", () => {
    expect(Object.isFrozen(registry.all)).toBe(true);
  });

  it("resolve: auth entry has correct 10 fields", () => {
    const info = registry.resolve("LIB_AUTH_BEARER_MISSING");
    expect(info?.code).toBe("LIB_AUTH_BEARER_MISSING");
    expect(info?.httpStatus).toBe(401);
    expect(info?.category).toBe("validation_failure");
    expect(info?.userMessageKey).toBe(TK.auth.errors.UNAUTHORIZED);
    expect(info?.factoryName).toBe("BearerMissing");
    expect(info?.factoryShape).toBe("standard");
    expect(info?.doc).toBe("Bearer missing.");
    expect(info?.domain).toBe("auth");
    expect(info?.owner).toBe("lib");
    expect(info?.isDeprecated).toBe(false);
  });

  it("buildRegistry with empty entries produces an empty registry", () => {
    const empty = buildRegistry([]);
    expect(empty.all).toHaveLength(0);
    expect(empty.resolve("GEN_NOT_FOUND")).toBeUndefined();
    expect(empty.has("GEN_NOT_FOUND")).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// ErrorCategory type - every schema value is valid
// ---------------------------------------------------------------------------

describe("ErrorCategory type - 9 schema values", () => {
  it("covers all 9 canonical category values", () => {
    const categories: ErrorCategory[] = [
      "validation_failure",
      "not_found",
      "conflict",
      "policy_denied",
      "rate_limited",
      "payload_too_large",
      "infrastructure_unavailable",
      "internal_error",
      "partial_success",
    ];
    // Every value - if the type is narrowed incorrectly, a TS error will fire.
    expect(categories).toHaveLength(9);
  });
});

// ---------------------------------------------------------------------------
// ErrorCodeFactoryShape type - both schema values are valid
// ---------------------------------------------------------------------------

describe("ErrorCodeFactoryShape type - 2 schema values", () => {
  it("covers both canonical factoryShape values", () => {
    const shapes: ErrorCodeFactoryShape[] = ["standard", "none"];
    expect(shapes).toHaveLength(2);
  });
});

// ---------------------------------------------------------------------------
// Generated errorCodeRegistry - the real merged registry
// ---------------------------------------------------------------------------

describe("errorCodeRegistry - generated merged registry", () => {
  it("all: contains the expected public-only total count (16 generic + 15 auth + 21 pagination + 1 messaging = 53)", () => {
    // The public merge excludes every closed-source catalog, and every such
    // catalog emits codes under the `DOM_` owner prefix.
    expect(errorCodeRegistry.all).toHaveLength(53);
    expect(errorCodeRegistry.all.some((e) => e.code.startsWith("DOM_"))).toBe(
      false,
    );
  });

  it("resolve: every generic code resolves with domain 'common' and owner 'gen'", () => {
    const genericCodes = [
      "GEN_NOT_FOUND",
      "GEN_FORBIDDEN",
      "GEN_UNAUTHORIZED",
      "GEN_VALIDATION_FAILED",
      "GEN_CONFLICT",
      "GEN_UNHANDLED_EXCEPTION",
      "GEN_COULD_NOT_BE_SERIALIZED",
      "GEN_COULD_NOT_BE_DESERIALIZED",
      "GEN_SERVICE_UNAVAILABLE",
      "GEN_SOME_FOUND",
      "GEN_PARTIAL_SUCCESS",
      "GEN_RATE_LIMITED",
      "GEN_IDEMPOTENCY_IN_FLIGHT",
      "GEN_PAYLOAD_TOO_LARGE",
      "GEN_CANCELED",
      "GEN_FORGERY_REFUSED",
    ];
    for (const code of genericCodes) {
      const info = errorCodeRegistry.resolve(code);
      expect(info, `${code} should resolve`).toBeDefined();
      expect(info?.domain, `${code} domain should be 'common'`).toBe("common");
      expect(info?.owner, `${code} owner should be 'gen'`).toBe("gen");
      expect(info?.isDeprecated, `${code} isDeprecated`).toBe(false);
    }
  });

  it("resolve: every auth code resolves with domain 'auth' and owner 'lib'", () => {
    const authCodes = [
      "LIB_AUTH_BEARER_MISSING",
      "LIB_AUTH_BEARER_MALFORMED",
      "LIB_AUTH_JWT_SIGNATURE_INVALID",
      "LIB_AUTH_JWT_EXPIRED",
      "LIB_AUTH_JWT_NOT_YET_VALID",
      "LIB_AUTH_JWT_ISSUER_MISMATCH",
      "LIB_AUTH_JWT_AUDIENCE_MISMATCH",
      "LIB_AUTH_JWT_CLAIM_MISSING",
      "LIB_AUTH_JWT_ACT_CHAIN_MALFORMED",
      "LIB_AUTH_JWT_KID_NOT_FOUND",
      "LIB_AUTH_JWKS_UNAVAILABLE",
      "LIB_AUTH_SESSION_REVOKED",
      "LIB_AUTH_SESSION_LIVENESS_UNAVAILABLE",
      "LIB_AUTH_SCOPE_INSUFFICIENT",
      "LIB_AUTH_REQUEST_ORIGIN_UNESTABLISHED",
    ];
    for (const code of authCodes) {
      const info = errorCodeRegistry.resolve(code);
      expect(info, `${code} should resolve`).toBeDefined();
      expect(info?.domain, `${code} domain should be 'auth'`).toBe("auth");
      expect(info?.owner, `${code} owner should be 'lib'`).toBe("lib");
      expect(info?.isDeprecated, `${code} isDeprecated`).toBe(false);
    }
  });

  it("resolve: DOM_-prefixed codes are absent from the public registry", () => {
    // Closed-source catalogs own the `DOM_` owner prefix. None of their codes
    // is merged here, so resolve returns undefined and has returns false for one.
    expect(
      errorCodeRegistry.all.filter((e) => e.code.startsWith("DOM_")),
    ).toEqual([]);
    expect(
      errorCodeRegistry.resolve("DOM_SAMPLE_KEY_NOT_FOUND"),
    ).toBeUndefined();
    expect(errorCodeRegistry.has("DOM_SAMPLE_KEY_STATE_CONFLICT")).toBe(false);
  });

  it("resolve: unknown code returns undefined", () => {
    expect(errorCodeRegistry.resolve("NOPE")).toBeUndefined();
    expect(errorCodeRegistry.resolve("UNKNOWN_CODE")).toBeUndefined();
  });

  it("resolve: empty string returns undefined", () => {
    expect(errorCodeRegistry.resolve("")).toBeUndefined();
  });

  it("resolve: whitespace-only returns undefined", () => {
    expect(errorCodeRegistry.resolve("  ")).toBeUndefined();
  });

  it("resolve: lowercase returns undefined (case-sensitive)", () => {
    expect(errorCodeRegistry.resolve("not_found")).toBeUndefined();
    expect(errorCodeRegistry.resolve("auth_bearer_missing")).toBeUndefined();
  });

  it("has: all registered codes return true", () => {
    for (const info of errorCodeRegistry.all) {
      expect(
        errorCodeRegistry.has(info.code),
        `has(${info.code}) should be true`,
      ).toBe(true);
    }
  });

  it("has: unknown code returns false", () => {
    expect(errorCodeRegistry.has("TOTALLY_UNKNOWN")).toBe(false);
  });

  it("resolve GEN_NOT_FOUND: full 10-field correctness", () => {
    const info = errorCodeRegistry.resolve("GEN_NOT_FOUND");
    expect(info).toBeDefined();
    expect(info?.code).toBe("GEN_NOT_FOUND");
    expect(info?.httpStatus).toBe(404);
    expect(info?.category).toBe("not_found");
    expect(info?.userMessageKey).toBe(TK.common.errors.NOT_FOUND);
    expect(info?.factoryName).toBe("NotFound");
    expect(info?.factoryShape).toBe("standard");
    expect(info?.doc).toMatch(/resource was not found/i);
    expect(info?.domain).toBe("common");
    expect(info?.owner).toBe("gen");
    expect(info?.isDeprecated).toBe(false);
  });

  it("resolve LIB_AUTH_JWKS_UNAVAILABLE: full 10-field correctness", () => {
    const info = errorCodeRegistry.resolve("LIB_AUTH_JWKS_UNAVAILABLE");
    expect(info).toBeDefined();
    expect(info?.code).toBe("LIB_AUTH_JWKS_UNAVAILABLE");
    expect(info?.httpStatus).toBe(503);
    expect(info?.category).toBe("infrastructure_unavailable");
    expect(info?.userMessageKey).toBe(TK.auth.errors.TEMPORARILY_UNAVAILABLE);
    expect(info?.factoryName).toBe("JwksUnavailable");
    expect(info?.factoryShape).toBe("standard");
    expect(info?.domain).toBe("auth");
    expect(info?.owner).toBe("lib");
    expect(info?.isDeprecated).toBe(false);
  });

  it("every entry has closed-set owner and boolean isDeprecated", () => {
    const owners = new Set(["gen", "lib", "dom"]);
    for (const info of errorCodeRegistry.all) {
      expect(
        owners.has(info.owner),
        `${info.code}: owner '${info.owner}'`,
      ).toBe(true);
      expect(typeof info.isDeprecated).toBe("boolean");
    }
  });

  it("every userMessageKey is a TKMessage with a non-empty key string", () => {
    for (const info of errorCodeRegistry.all) {
      expect(
        typeof info.userMessageKey.key === "string" &&
          info.userMessageKey.key.length > 0,
        `${info.code}: userMessageKey.key must be a non-empty string`,
      ).toBe(true);
    }
  });

  it("every category value is one of the 9 canonical values", () => {
    const valid = new Set<string>([
      "validation_failure",
      "not_found",
      "conflict",
      "policy_denied",
      "rate_limited",
      "payload_too_large",
      "infrastructure_unavailable",
      "internal_error",
      "partial_success",
    ]);
    for (const info of errorCodeRegistry.all) {
      expect(
        valid.has(info.category),
        `${info.code}: category '${info.category}' is not a known value`,
      ).toBe(true);
    }
  });

  it("every factoryShape value is one of the 2 canonical values", () => {
    const valid = new Set<string>(["standard", "none"]);
    for (const info of errorCodeRegistry.all) {
      expect(
        valid.has(info.factoryShape),
        `${info.code}: factoryShape '${info.factoryShape}' is not a known value`,
      ).toBe(true);
    }
  });

  it("every code in 'all' matches SCREAMING_SNAKE pattern", () => {
    const re = /^[A-Z][A-Z0-9_]*$/;
    for (const info of errorCodeRegistry.all) {
      expect(
        re.test(info.code),
        `${info.code}: does not match SCREAMING_SNAKE`,
      ).toBe(true);
    }
  });

  it("no duplicate codes in 'all'", () => {
    const codes = errorCodeRegistry.all.map((e) => e.code);
    const unique = new Set(codes);
    expect(unique.size).toBe(codes.length);
  });
});
