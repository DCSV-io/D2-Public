// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import {
  DlqFailureCauses,
  DlqFailureMetadataFields,
} from "@dcsv-io/d2-messaging-abstractions";
import { fail, tk } from "@dcsv-io/d2-result";
import { describe, expect, it } from "vitest";

import { DlqFailureHeaderBuilder } from "../src/subscribing/dlq-failure-metadata.js";
import { MessageBodyDecodeError } from "../src/subscribing/message-body-decode-error.js";

function decode(header: string): Record<string, unknown> {
  return JSON.parse(header) as Record<string, unknown>;
}

describe("DlqFailureHeaderBuilder - .NET DlqFailureHeaderBuilder parity", () => {
  it("fromException: cause + error type name, no detail, PII-safe", () => {
    const meta = decode(
      DlqFailureHeaderBuilder.fromException(new TypeError("secret user input")),
    );
    expect(meta[DlqFailureMetadataFields.CAUSE]).toBe(
      DlqFailureCauses.HANDLER_EXCEPTION,
    );
    expect(meta[DlqFailureMetadataFields.ERROR_CODE]).toBe("TypeError");
    // The detail is never built from the exception message, which can contain
    // user input.
    expect(meta[DlqFailureMetadataFields.DETAIL]).toBeUndefined();
    expect(meta[DlqFailureMetadataFields.ATTEMPT_COUNT]).toBe(0);
    expect(JSON.stringify(meta)).not.toContain("secret user input");
  });

  it("fromResult: joins message KEYS (translation tokens) into detail", () => {
    const result = fail({
      errorCode: "DOM_SAMPLE_ORDER_UNAVAILABLE",
      messages: [
        tk("sample.errors.SEAL_KEY_UNAVAILABLE"),
        tk("common.errors.RETRY"),
      ],
    });
    const meta = decode(DlqFailureHeaderBuilder.fromResult(result));
    expect(meta[DlqFailureMetadataFields.CAUSE]).toBe(
      DlqFailureCauses.HANDLER_RESULT_FAILURE,
    );
    expect(meta[DlqFailureMetadataFields.ERROR_CODE]).toBe(
      "DOM_SAMPLE_ORDER_UNAVAILABLE",
    );
    expect(meta[DlqFailureMetadataFields.DETAIL]).toBe(
      "sample.errors.SEAL_KEY_UNAVAILABLE; common.errors.RETRY",
    );
  });

  it("fromResult: unknown error code + empty messages omit detail", () => {
    const meta = decode(DlqFailureHeaderBuilder.fromResult(fail()));
    // Pins the fallback for an absent error code to the wire literal that the
    // .NET sibling (`DlqFailureHeaderBuilder.cs:76`) also emits. The test fails
    // when the local `_UNKNOWN_ERROR_CODE` sentinel changes, so both runtimes
    // keep byte parity.
    expect(meta[DlqFailureMetadataFields.ERROR_CODE]).toBe("UNKNOWN");
    expect(meta[DlqFailureMetadataFields.DETAIL]).toBeUndefined();
  });

  it("fromResult: truncates an overlong detail to 256 chars", () => {
    const result = fail({ errorCode: "X", messages: [tk("a".repeat(400))] });
    const meta = decode(DlqFailureHeaderBuilder.fromResult(result));
    expect((meta[DlqFailureMetadataFields.DETAIL] as string).length).toBe(256);
  });

  it("fromRetriesExhausted: cause + errorCode both RETRIES_EXHAUSTED with count", () => {
    const meta = decode(DlqFailureHeaderBuilder.fromRetriesExhausted(5));
    expect(meta[DlqFailureMetadataFields.CAUSE]).toBe(
      DlqFailureCauses.RETRIES_EXHAUSTED,
    );
    expect(meta[DlqFailureMetadataFields.ERROR_CODE]).toBe(
      DlqFailureCauses.RETRIES_EXHAUSTED,
    );
    expect(meta[DlqFailureMetadataFields.ATTEMPT_COUNT]).toBe(5);
  });

  it("fromBoundary: carries the given cause + inner error name", () => {
    const meta = decode(
      DlqFailureHeaderBuilder.fromBoundary(
        DlqFailureCauses.DECRYPT_FAILURE,
        new MessageBodyDecodeError(
          DlqFailureCauses.DECRYPT_FAILURE,
          "no opener",
        ),
      ),
    );
    expect(meta[DlqFailureMetadataFields.CAUSE]).toBe(
      DlqFailureCauses.DECRYPT_FAILURE,
    );
    expect(meta[DlqFailureMetadataFields.ERROR_CODE]).toBe(
      "MessageBodyDecodeError",
    );
  });

  it("includes traceId + nackedBy when supplied; omits them when absent", () => {
    const withCtx = decode(
      DlqFailureHeaderBuilder.fromException(new Error("x"), {
        traceId: "abc123",
        nackedBy: "orders-svc",
      }),
    );
    expect(withCtx[DlqFailureMetadataFields.TRACE_ID]).toBe("abc123");
    expect(withCtx[DlqFailureMetadataFields.NACKED_BY]).toBe("orders-svc");

    const withoutCtx = decode(
      DlqFailureHeaderBuilder.fromException(new Error("x")),
    );
    expect(DlqFailureMetadataFields.TRACE_ID in withoutCtx).toBe(false);
    expect(DlqFailureMetadataFields.NACKED_BY in withoutCtx).toBe(false);
  });
});
