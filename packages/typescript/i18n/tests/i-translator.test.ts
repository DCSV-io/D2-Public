// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import type { TKMessage } from "@dcsv-io/d2-i18n-abstractions";
import type { ITranslator } from "../src/i-translator.js";

describe("ITranslator", () => {
  it("can be implemented by hand-rolled stubs (interface contract)", () => {
    const stub: ITranslator = {
      t: (_locale: string, message: TKMessage) => `[${message.key}]`,
    };
    expect(stub.t("en-US", { key: "TK.x" })).toBe("[TK.x]");
  });
});
