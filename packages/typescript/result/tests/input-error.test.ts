// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { InputErrorWireShape } from "../src/input-error.g.js";
import { inputError } from "../src/input-error.js";

describe("inputError()", () => {
  it("constructs the wire-shape", () => {
    const e = inputError("email", [{ key: "TK.X" }]);
    expect(e).toEqual({ field: "email", errors: [{ key: "TK.X" }] });
  });

  it("emits wire-shape property names from the spec-derived catalog", () => {
    // The inputError() builder emits the property names `field` and `errors`
    // on the wire, and they must equal the names in the spec catalog (a single
    // object shape).
    const e = inputError("email", [{ key: "TK.X" }]);
    expect(Object.keys(e).sort()).toEqual(
      [InputErrorWireShape.FIELD, InputErrorWireShape.ERRORS].sort(),
    );
  });

  it("wire-shape catalog pins FIELD = 'field'", () => {
    expect(InputErrorWireShape.FIELD).toBe("field");
  });

  it("wire-shape catalog pins ERRORS = 'errors'", () => {
    expect(InputErrorWireShape.ERRORS).toBe("errors");
  });
});
