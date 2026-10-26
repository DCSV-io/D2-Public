// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    // D2_VITEST_WORKERS workers per package, 4 when it is unset, each a child
    // process that exits with the run.
    pool: "forks",
    maxWorkers: Number(process.env.D2_VITEST_WORKERS) || 4,
    include: ["tests/**/*.test.ts"],
    coverage: {
      provider: "v8",
      include: ["src/**/*.ts"],
      // The barrel `index.ts` is a pure re-export and `ports.ts` is types-only
      // (no runtime); every logic-bearing module must reach the 100% thresholds
      // below.
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
