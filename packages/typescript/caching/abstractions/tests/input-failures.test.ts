// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { TK } from "@dcsv-io/d2-i18n-keys";
import { ErrorCodes, HttpStatusCode } from "@dcsv-io/d2-result";
import { describe, expect, it } from "vitest";

import { InputFailures } from "../src/index.js";

/**
 * Pins the wire-shape of `InputFailures.required` / `required<T>`.
 * Twin of .NET `Unit/Caching/Abstractions/InputFailuresTests`.
 * The exact shape is `success === false`, `ErrorCodes.GEN_VALIDATION_FAILED`,
 * status 400, and a single `inputError` that holds the param name and
 * `TK.common.errors.NOT_NULL_VIOLATION`.
 */

describe("InputFailures.required", () => {
  it("required_emptyParamName_stillBuildsValidationFailed", () => {
    const result = InputFailures.required("");

    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(ErrorCodes.GEN_VALIDATION_FAILED);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toHaveLength(1);
    expect(result.inputErrors[0]?.field).toBe("");
  });

  it("required_whitespaceParamName_buildsValidationFailedWithThatField", () => {
    const result = InputFailures.required("   ");

    expect(result.success).toBe(false);
    expect(result.inputErrors).toHaveLength(1);
    expect(result.inputErrors[0]?.field).toBe("   ");
  });

  it.each([
    "key",
    "keys",
    "entries",
    "paramName",
    "anyArbitraryParameterName",
  ] as const)(
    "required_paramName_propagatesVerbatim_theory (%s)",
    (paramName) => {
      const generic = InputFailures.required<number>(paramName);
      const nonGeneric = InputFailures.required(paramName);

      expect(generic.inputErrors[0]?.field).toBe(paramName);
      expect(nonGeneric.inputErrors[0]?.field).toBe(paramName);
    },
  );

  it("required_wireShape_successFalse_validationFailedCode_status400_singleInputError_fieldEqualsParam_tkNotNullViolation", () => {
    const paramName = "key";
    const result = InputFailures.required<string>(paramName);

    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(ErrorCodes.GEN_VALIDATION_FAILED);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.statusCode).toBe(400);
    expect(result.inputErrors).toHaveLength(1);
    expect(result.inputErrors[0]?.field).toBe(paramName);
    expect(result.inputErrors[0]?.errors).toEqual([
      TK.common.errors.NOT_NULL_VIOLATION,
    ]);
  });

  it("required_nonGeneric_matchesGeneric_errorShape", () => {
    const paramName = "lockId";
    const generic = InputFailures.required<boolean>(paramName);
    const nonGeneric = InputFailures.required(paramName);

    expect(generic.success).toBe(nonGeneric.success);
    expect(generic.errorCode).toBe(nonGeneric.errorCode);
    expect(generic.statusCode).toBe(nonGeneric.statusCode);
    expect(generic.inputErrors).toEqual(nonGeneric.inputErrors);
  });

  it("required_generic_dataIsUndefined", () => {
    const result = InputFailures.required<string>("key");

    expect(result.data).toBeUndefined();
  });

  it("required_paramNameWithControlChars_isPreservedAsFieldNameWithoutThrow", () => {
    const paramName = "key\nwith\tcontrol\0chars";
    const result = InputFailures.required(paramName);

    expect(result.success).toBe(false);
    expect(result.inputErrors[0]?.field).toBe(paramName);
  });
});

describe("InputFailures.invalid", () => {
  it("invalid_wireShape_usesValidationFailedTkNotNotNull", () => {
    const paramName = "amount";
    const result = InputFailures.invalid<number>(paramName);

    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(ErrorCodes.GEN_VALIDATION_FAILED);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toHaveLength(1);
    expect(result.inputErrors[0]?.field).toBe(paramName);
    expect(result.inputErrors[0]?.errors).toEqual([
      TK.common.errors.VALIDATION_FAILED,
    ]);
  });

  it("invalid_nonGeneric_matchesGeneric_errorShape", () => {
    const paramName = "expirationMs";
    const generic = InputFailures.invalid<boolean>(paramName);
    const nonGeneric = InputFailures.invalid(paramName);

    expect(generic.success).toBe(nonGeneric.success);
    expect(generic.errorCode).toBe(nonGeneric.errorCode);
    expect(generic.statusCode).toBe(nonGeneric.statusCode);
    expect(generic.inputErrors).toEqual(nonGeneric.inputErrors);
  });
});

describe("InputFailures.tokenBucket", () => {
  it.each([
    ["", 10, 60_000, 5, 1, "key"],
    ["   ", 10, 60_000, 5, 1, "key"],
    ["k", 0, 60_000, 5, 1, "refillTokens"],
    ["k", -1, 60_000, 5, 1, "refillTokens"],
    ["k", 1.5, 60_000, 5, 1, "refillTokens"],
    ["k", 10, 0, 5, 1, "refillIntervalMs"],
    ["k", 10, 0.5, 5, 1, "refillIntervalMs"],
    ["k", 10, 1.5, 5, 1, "refillIntervalMs"],
    ["k", 10, 60_000, 0, 1, "burst"],
    ["k", 10, 60_000, 5, 0, "cost"],
    ["k", 10, 60_000, 5, 6, "cost"],
  ] as const)(
    "tokenBucket_anArgumentTheBucketCannotUse_isRefusedByName (%s %d %d %d %d)",
    (key, refillTokens, refillIntervalMs, burst, cost, field) => {
      const result = InputFailures.tokenBucket(
        key,
        refillTokens,
        refillIntervalMs,
        burst,
        cost,
      );

      expect(result).not.toBeUndefined();
      expect(result?.success).toBe(false);
      expect(result?.inputErrors[0]?.field).toBe(field);
    },
  );

  it("tokenBucket_everyArgumentAtItsBound_isAccepted", () => {
    expect(InputFailures.tokenBucket("k", 1, 1, 1, 1)).toBeUndefined();
  });

  it("tokenBucket_goodArguments_areAccepted", () => {
    expect(InputFailures.tokenBucket("k", 10, 60_000, 5, 5)).toBeUndefined();
  });
});
