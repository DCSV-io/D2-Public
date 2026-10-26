// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  ALL_ERROR_CODES,
  ErrorCodes,
  getErrorHttpStatus,
} from "../src/error-codes.g.js";
import { HttpStatusCode } from "../src/http-status-codes.js";

describe("ErrorCodes - per-VALUE pinning (defends rename safety)", () => {
  it.each([
    ["GEN_NOT_FOUND", "GEN_NOT_FOUND"],
    ["GEN_FORBIDDEN", "GEN_FORBIDDEN"],
    ["GEN_UNAUTHORIZED", "GEN_UNAUTHORIZED"],
    ["GEN_VALIDATION_FAILED", "GEN_VALIDATION_FAILED"],
    ["GEN_CONFLICT", "GEN_CONFLICT"],
    ["GEN_UNHANDLED_EXCEPTION", "GEN_UNHANDLED_EXCEPTION"],
    ["GEN_COULD_NOT_BE_SERIALIZED", "GEN_COULD_NOT_BE_SERIALIZED"],
    ["GEN_COULD_NOT_BE_DESERIALIZED", "GEN_COULD_NOT_BE_DESERIALIZED"],
    ["GEN_SERVICE_UNAVAILABLE", "GEN_SERVICE_UNAVAILABLE"],
    ["GEN_SOME_FOUND", "GEN_SOME_FOUND"],
    ["GEN_PARTIAL_SUCCESS", "GEN_PARTIAL_SUCCESS"],
    ["GEN_RATE_LIMITED", "GEN_RATE_LIMITED"],
    ["GEN_IDEMPOTENCY_IN_FLIGHT", "GEN_IDEMPOTENCY_IN_FLIGHT"],
    ["GEN_PAYLOAD_TOO_LARGE", "GEN_PAYLOAD_TOO_LARGE"],
    ["GEN_CANCELED", "GEN_CANCELED"],
    ["GEN_FORGERY_REFUSED", "GEN_FORGERY_REFUSED"],
  ])("ErrorCodes.%s = %s", (key, value) => {
    expect(ErrorCodes[key as keyof typeof ErrorCodes]).toBe(value);
  });
});

describe("ALL_ERROR_CODES - exhaustiveness", () => {
  it("contains every ErrorCodes key", () => {
    expect([...ALL_ERROR_CODES].sort()).toEqual(Object.keys(ErrorCodes).sort());
  });

  it("preserves spec order (NOT_FOUND first, FORGERY_REFUSED last)", () => {
    expect(ALL_ERROR_CODES[0]).toBe("GEN_NOT_FOUND");
    expect(ALL_ERROR_CODES[ALL_ERROR_CODES.length - 1]).toBe(
      "GEN_FORGERY_REFUSED",
    );
  });
});

describe("getErrorHttpStatus - per-VALUE pinning (mirrors .NET ErrorCodes.GetHttpStatus)", () => {
  it.each([
    ["GEN_NOT_FOUND", 404],
    ["GEN_FORBIDDEN", 403],
    ["GEN_UNAUTHORIZED", 401],
    ["GEN_VALIDATION_FAILED", 400],
    ["GEN_CONFLICT", 409],
    ["GEN_UNHANDLED_EXCEPTION", 500],
    ["GEN_COULD_NOT_BE_SERIALIZED", 500],
    ["GEN_COULD_NOT_BE_DESERIALIZED", 500],
    ["GEN_SERVICE_UNAVAILABLE", 503],
    ["GEN_SOME_FOUND", 206],
    ["GEN_PARTIAL_SUCCESS", 503],
    ["GEN_RATE_LIMITED", 429],
    ["GEN_IDEMPOTENCY_IN_FLIGHT", 409],
    ["GEN_PAYLOAD_TOO_LARGE", 413],
    ["GEN_CANCELED", 400],
    ["GEN_FORGERY_REFUSED", 403],
  ])("getErrorHttpStatus(%s) = %s", (code, expectedStatus) => {
    expect(getErrorHttpStatus(code)).toBe(expectedStatus);
  });

  it("returns 500 for an unknown code (defensive default)", () => {
    expect(getErrorHttpStatus("UNKNOWN_TO_THE_CATALOG")).toBe(500);
  });
});

describe("HttpStatusCode - per-VALUE pinning", () => {
  it.each([
    ["OK", 200],
    ["Created", 201],
    ["NoContent", 204],
    ["PartialContent", 206],
    ["BadRequest", 400],
    ["Unauthorized", 401],
    ["Forbidden", 403],
    ["NotFound", 404],
    ["Conflict", 409],
    ["RequestEntityTooLarge", 413],
    ["TooManyRequests", 429],
    ["InternalServerError", 500],
    ["ServiceUnavailable", 503],
  ])("HttpStatusCode.%s = %s", (key, value) => {
    expect(HttpStatusCode[key as keyof typeof HttpStatusCode]).toBe(value);
  });
});
