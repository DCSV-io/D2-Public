// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { TkMessageWireShape } from "../src/generated/tk-message.g.js";
import { tk } from "../src/tk-message.js";

describe("tk()", () => {
  it("returns key-only message when no params", () => {
    expect(tk("TK.X")).toEqual({ key: "TK.X" });
  });

  it("returns key + params when params supplied", () => {
    expect(tk("TK.X", { name: "Alice" })).toEqual({
      key: "TK.X",
      params: { name: "Alice" },
    });
  });

  it("explicit empty params object preserved", () => {
    expect(tk("TK.X", {})).toEqual({ key: "TK.X", params: {} });
  });

  it("emits wire-shape property names from the spec-derived catalog", () => {
    // The property names of the wire message come from the spec-derived
    // constants. This assertion compares the full key set, so it fails when
    // tk() emits a key outside those constants or omits one of them.
    const m = tk("TK.X", { p: 1 });
    expect(Object.keys(m).sort()).toEqual(
      [TkMessageWireShape.KEY, TkMessageWireShape.PARAMS].sort(),
    );
  });

  it("wire-shape catalog pins KEY = 'key'", () => {
    expect(TkMessageWireShape.KEY).toBe("key");
  });

  it("wire-shape catalog pins PARAMS = 'params'", () => {
    expect(TkMessageWireShape.PARAMS).toBe("params");
  });
});
