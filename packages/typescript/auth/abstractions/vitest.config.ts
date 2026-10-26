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
      // Codegen-emitted files (`*.g.ts` - auth-error-codes / auth-failures /
      // scopes / jwt-claim-types) are covered by the catalog-codegen emitter
      // snapshots, which live with the code generator and not in this package,
      // and by per-value pin tests in tests/. The same glob also excludes
      // jwt-payload.g.ts and protocol-audiences.g.ts, which no test in tests/
      // pins. The hand-written `index.ts` is a pure re-export and is excluded
      // from the 100/100/100/100 threshold.
      exclude: ["src/**/*.g.ts", "src/index.ts"],
      thresholds: {
        lines: 100,
        branches: 100,
        functions: 100,
        statements: 100,
      },
    },
  },
});
