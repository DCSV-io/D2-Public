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
    // Unit suite only. The Testcontainers integration suite lives under
    // tests/integration/ and runs through vitest.integration.config.ts. It has
    // no coverage gate: its tests check real-broker paths and do not count lines.
    include: ["tests/**/*.test.ts"],
    exclude: ["tests/integration/**", "node_modules", "dist"],
    coverage: {
      provider: "v8",
      include: ["src/**/*.ts"],
      // `domain-crypto-map.ts` holds types only (the compile-time publish witness).
      // tests/publisher-type-witness.compile.ts checks it under the type-check
      // gate, and runtime coverage does not measure it.
      exclude: [
        "src/**/*.g.ts",
        "src/index.ts",
        "src/publishing/domain-crypto-map.ts",
      ],
      thresholds: {
        lines: 100,
        branches: 100,
        functions: 100,
        statements: 100,
      },
    },
  },
});
