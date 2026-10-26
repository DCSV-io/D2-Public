// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { ErrorCodes } from "../src/error-codes.g.js";
import {
  canceled,
  conflict,
  forbidden,
  forgeryRefused,
  notFound,
  payloadTooLarge,
  serviceUnavailable,
  tooManyRequests,
  unauthorized,
  unhandledException,
  validationFailed,
} from "../src/factories.g.js";
import { created, fail, noContent, ok, someFound } from "../src/factories.js";
import { HttpStatusCode } from "../src/http-status-codes.js";

describe("ok()", () => {
  it("no args -> success, no payload", () => {
    const r = ok();
    expect(r.success).toBe(true);
    expect(r.data).toBeUndefined();
    expect(r.statusCode).toBe(HttpStatusCode.OK);
  });

  it("data overload", () => {
    const r = ok<{ id: string }>({ id: "x" });
    expect(r.data).toEqual({ id: "x" });
  });

  it("data + traceId", () => {
    const r = ok<number>(42, "trace-2");
    expect(r.data).toBe(42);
    expect(r.traceId).toBe("trace-2");
  });

  it("string-typed payload preserved", () => {
    const r = ok<string>("two");
    expect(r.data).toBe("two");
    expect(r.traceId).toBeUndefined();
  });
});

describe("created()", () => {
  it("HTTP 201 + success=true", () => {
    const r = created();
    expect(r.success).toBe(true);
    expect(r.statusCode).toBe(HttpStatusCode.Created);
  });
  it("traceId pass-through", () => {
    expect(created({ traceId: "t" }).traceId).toBe("t");
  });
});

describe("noContent()", () => {
  it("HTTP 204 + success=true, no payload", () => {
    const r = noContent();
    expect(r.success).toBe(true);
    expect(r.statusCode).toBe(HttpStatusCode.NoContent);
    expect(r.data).toBeUndefined();
    expect(r.errorCode).toBeUndefined();
  });

  it("traceId pass-through", () => {
    expect(noContent("t").traceId).toBe("t");
  });
});

describe("fail()", () => {
  it("default HTTP 400 + no errorCode", () => {
    const r = fail();
    expect(r.failed).toBe(true);
    expect(r.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(r.errorCode).toBeUndefined();
  });
  it("status + errorCode override", () => {
    const r = fail({
      statusCode: HttpStatusCode.Conflict,
      errorCode: "MY_CODE",
    });
    expect(r.statusCode).toBe(HttpStatusCode.Conflict);
    expect(r.errorCode).toBe("MY_CODE");
  });
});

describe("notFound()", () => {
  it("HTTP 404 + NOT_FOUND code + default TK", () => {
    const r = notFound();
    expect(r.statusCode).toBe(HttpStatusCode.NotFound);
    expect(r.errorCode).toBe(ErrorCodes.GEN_NOT_FOUND);
    // The default TK message must go on the wire as the snake-case en-US.json key,
    // which resolves in the Translator/Paraglide catalog. The raw PascalCase
    // symbol path would not resolve and could not be rendered. The factory
    // references the `TK.common.errors.NOT_FOUND` constant, whose value is this
    // snake-case key, matching the .NET wire key for the same factory.
    expect(r.messages[0]?.key).toBe("common_errors_NOT_FOUND");
  });
  it("override messages preserved", () => {
    const r = notFound({ messages: [{ key: "TK.X" }] });
    expect(r.messages).toEqual([{ key: "TK.X" }]);
  });

  it("accepts the universal ErrorOpts: inputErrors + errorCode + category override", () => {
    // Every error factory takes the one universal ErrorOpts, so a 404 can carry
    // inputErrors and stamp a domain errorCode and a category override.
    const r = notFound({
      inputErrors: [{ field: "id", errors: [{ key: "TK.X" }] }],
      errorCode: "GEO_SUBDIVISION_NOT_FOUND",
      category: "conflict",
      traceId: "t-9",
    });
    expect(r.statusCode).toBe(HttpStatusCode.NotFound);
    expect(r.errorCode).toBe("GEO_SUBDIVISION_NOT_FOUND");
    expect(r.category).toBe("conflict");
    expect(r.traceId).toBe("t-9");
    expect(r.inputErrors).toHaveLength(1);
    expect(r.messages[0]?.key).toBe("common_errors_NOT_FOUND");
  });
});

describe("unauthorized()", () => {
  it("HTTP 401 + UNAUTHORIZED code", () => {
    const r = unauthorized();
    expect(r.statusCode).toBe(HttpStatusCode.Unauthorized);
    expect(r.errorCode).toBe(ErrorCodes.GEN_UNAUTHORIZED);
    expect(r.messages[0]?.key).toBe("common_errors_UNAUTHORIZED");
  });
  it("errorCode override (e.g. LIB_AUTH_JWT_EXPIRED)", () => {
    expect(unauthorized({ errorCode: "LIB_AUTH_JWT_EXPIRED" }).errorCode).toBe(
      "LIB_AUTH_JWT_EXPIRED",
    );
  });
});

describe("forbidden()", () => {
  it("HTTP 403 + FORBIDDEN code", () => {
    const r = forbidden();
    expect(r.statusCode).toBe(HttpStatusCode.Forbidden);
    expect(r.errorCode).toBe(ErrorCodes.GEN_FORBIDDEN);
    expect(r.messages[0]?.key).toBe("common_errors_FORBIDDEN");
  });
  it("errorCode override", () => {
    expect(forbidden({ errorCode: "X" }).errorCode).toBe("X");
  });
});

describe("validationFailed()", () => {
  it("HTTP 400 + VALIDATION_FAILED + inputErrors pass-through", () => {
    const r = validationFailed({
      inputErrors: [{ field: "email", errors: [{ key: "TK.X" }] }],
    });
    expect(r.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(r.errorCode).toBe(ErrorCodes.GEN_VALIDATION_FAILED);
    expect(r.inputErrors).toHaveLength(1);
    expect(r.messages[0]?.key).toBe("common_errors_VALIDATION_FAILED");
  });
  it("errorCode override (e.g. FILES_INVALID_CONTENT_TYPE)", () => {
    expect(
      validationFailed({ errorCode: "FILES_INVALID_CONTENT_TYPE" }).errorCode,
    ).toBe("FILES_INVALID_CONTENT_TYPE");
  });
});

describe("conflict()", () => {
  it("HTTP 409 + CONFLICT", () => {
    const r = conflict();
    expect(r.statusCode).toBe(HttpStatusCode.Conflict);
    expect(r.errorCode).toBe(ErrorCodes.GEN_CONFLICT);
    expect(r.messages[0]?.key).toBe("common_errors_CONFLICT");
  });
});

describe("serviceUnavailable()", () => {
  it("HTTP 503 + SERVICE_UNAVAILABLE", () => {
    const r = serviceUnavailable();
    expect(r.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    expect(r.errorCode).toBe(ErrorCodes.GEN_SERVICE_UNAVAILABLE);
    expect(r.messages[0]?.key).toBe("common_errors_SERVICE_UNAVAILABLE");
  });
  it("errorCode override (e.g. LIB_AUTH_JWKS_UNAVAILABLE)", () => {
    expect(
      serviceUnavailable({ errorCode: "LIB_AUTH_JWKS_UNAVAILABLE" }).errorCode,
    ).toBe("LIB_AUTH_JWKS_UNAVAILABLE");
  });
});

describe("unhandledException()", () => {
  it("HTTP 500 + UNHANDLED_EXCEPTION", () => {
    const r = unhandledException();
    expect(r.statusCode).toBe(HttpStatusCode.InternalServerError);
    expect(r.errorCode).toBe(ErrorCodes.GEN_UNHANDLED_EXCEPTION);
    // The default TK key is UNKNOWN and not UNHANDLED_EXCEPTION, because no such
    // TK key exists. The error code and the user-message key differ by design.
    expect(r.messages[0]?.key).toBe("common_errors_UNKNOWN");
    expect(r.category).toBe("internal_error");
  });
  it("errorCode override (e.g. DOM_SAMPLE_PRECONDITION_VIOLATED)", () => {
    // The base factory for status 500 accepts an errorCode override that stamps
    // a specific code on the base status.
    expect(
      unhandledException({
        errorCode: "DOM_SAMPLE_PRECONDITION_VIOLATED",
      }).errorCode,
    ).toBe("DOM_SAMPLE_PRECONDITION_VIOLATED");
  });
});

describe("payloadTooLarge()", () => {
  it("HTTP 413 + PAYLOAD_TOO_LARGE", () => {
    const r = payloadTooLarge();
    expect(r.statusCode).toBe(HttpStatusCode.RequestEntityTooLarge);
    expect(r.errorCode).toBe(ErrorCodes.GEN_PAYLOAD_TOO_LARGE);
    expect(r.messages[0]?.key).toBe("common_errors_PAYLOAD_TOO_LARGE");
  });
});

describe("tooManyRequests()", () => {
  it("HTTP 429 + RATE_LIMITED + override-able", () => {
    const r = tooManyRequests();
    expect(r.statusCode).toBe(HttpStatusCode.TooManyRequests);
    expect(r.errorCode).toBe(ErrorCodes.GEN_RATE_LIMITED);
    // The factory name is tooManyRequests, the error code is RATE_LIMITED, and
    // the default TK key is TOO_MANY_REQUESTS (the names differ by design).
    expect(r.messages[0]?.key).toBe("common_errors_TOO_MANY_REQUESTS");
    expect(tooManyRequests({ errorCode: "OTP_RATE_LIMITED" }).errorCode).toBe(
      "OTP_RATE_LIMITED",
    );
  });
});

describe("canceled()", () => {
  it("HTTP 400 + CANCELED", () => {
    const r = canceled();
    expect(r.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(r.errorCode).toBe(ErrorCodes.GEN_CANCELED);
    expect(r.messages[0]?.key).toBe("common_errors_CANCELED");
  });
});

describe("forgeryRefused()", () => {
  it("HTTP 403 + FORGERY_REFUSED, reusing the shared forbidden message", () => {
    const r = forgeryRefused();
    expect(r.statusCode).toBe(HttpStatusCode.Forbidden);
    expect(r.errorCode).toBe(ErrorCodes.GEN_FORGERY_REFUSED);
    expect(r.messages[0]?.key).toBe("common_errors_FORBIDDEN");
  });
});

describe("someFound()", () => {
  it("HTTP 206 + SOME_FOUND, success=false (partial-success ladder)", () => {
    const r = someFound<{ ids: string[] }>({ data: { ids: ["a"] } });
    expect(r.statusCode).toBe(HttpStatusCode.PartialContent);
    expect(r.errorCode).toBe(ErrorCodes.GEN_SOME_FOUND);
    expect(r.success).toBe(false);
    expect(r.isPartialSuccess).toBe(true);
    expect(r.data).toEqual({ ids: ["a"] });
  });
});

describe("generated factories stamp the spec category", () => {
  // Per-factory pin: each generated base factory sets the snake-wire category
  // declared in contracts/error-codes/error-codes.spec.json. Producer-set at
  // generation time (no runtime registry lookup).
  it.each([
    [notFound(), "not_found"],
    [forbidden(), "policy_denied"],
    [unauthorized(), "policy_denied"],
    [validationFailed(), "validation_failure"],
    [conflict(), "conflict"],
    [unhandledException(), "internal_error"],
    [serviceUnavailable(), "infrastructure_unavailable"],
    [tooManyRequests(), "rate_limited"],
    [payloadTooLarge(), "payload_too_large"],
    [canceled(), "validation_failure"],
    [forgeryRefused(), "policy_denied"],
  ] as const)("factory carries category %#", (result, expectedCategory) => {
    expect(result.category).toBe(expectedCategory);
  });

  it("every error factory honors a caller category override (universal standard shape)", () => {
    // The override lets a domain factory that delegates to a base factory stamp
    // its own code's category. Every base factory honors it, notFound included.
    expect(forbidden({ category: "validation_failure" }).category).toBe(
      "validation_failure",
    );
    expect(unauthorized({ category: "not_found" }).category).toBe("not_found");
    expect(notFound({ category: "conflict" }).category).toBe("conflict");
  });

  it("hand-rolled ok/created/fail carry no category", () => {
    expect(ok().category).toBeUndefined();
    expect(created().category).toBeUndefined();
    expect(fail().category).toBeUndefined();
  });

  it("someFound carries partial_success category", () => {
    expect(someFound().category).toBe("partial_success");
    expect(someFound<{ id: string }>({ data: { id: "x" } }).category).toBe(
      "partial_success",
    );
  });
});
