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
      // The codegen-emitted `*.g.ts` files are excluded from coverage.
      include: ["src/**/*.ts"],
      exclude: ["src/**/*.g.ts", "src/index.ts"],
    },
  },
});
