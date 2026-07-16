// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    include: ["tests/**/*.test.ts"],
    coverage: {
      provider: "v8",
      include: ["src/**/*.ts"],
      // The barrel `index.ts` is a pure re-export and `ports.ts` is types-only
      // (no runtime); every logic-bearing module is held to the 100% bar below.
      exclude: ["src/**/*.g.ts", "src/index.ts", "src/ports.ts"],
      thresholds: {
        lines: 100,
        branches: 100,
        functions: 100,
        statements: 100,
      },
    },
  },
});
