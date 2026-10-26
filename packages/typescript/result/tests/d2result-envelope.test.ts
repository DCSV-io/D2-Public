// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  ALL_D2RESULT_ENVELOPE_FIELD_NAMES,
  D2ResultEnvelopeFieldNames,
} from "../src/d2result-envelope.g.js";

// The catalog is the wire contract every serializer reads its property names
// from, so each name is pinned by value rather than by shape: a regenerated
// catalog that renamed one would otherwise change the wire silently.
describe("D2ResultEnvelopeFieldNames - per-VALUE pinning", () => {
  it.each([
    ["SUCCESS", "success"],
    ["DATA", "data"],
    ["MESSAGES", "messages"],
    ["INPUT_ERRORS", "inputErrors"],
    ["ERROR_CODE", "errorCode"],
    ["TRACE_ID", "traceId"],
    ["STATUS_CODE", "statusCode"],
    ["CATEGORY", "category"],
  ])("D2ResultEnvelopeFieldNames.%s = %s", (key, value) => {
    expect(
      D2ResultEnvelopeFieldNames[
        key as keyof typeof D2ResultEnvelopeFieldNames
      ],
    ).toBe(value);
  });
});

describe("ALL_D2RESULT_ENVELOPE_FIELD_NAMES", () => {
  it("holds every catalog value, in declaration order", () => {
    expect(ALL_D2RESULT_ENVELOPE_FIELD_NAMES).toEqual(
      Object.values(D2ResultEnvelopeFieldNames),
    );
  });
});
