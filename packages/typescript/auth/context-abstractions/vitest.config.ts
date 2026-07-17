// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    include: ["tests/**/*.test.ts"],
    coverage: {
      provider: "v8",
      // Codegen-emitted; coverage from catalog-codegen emitter tests instead.
      include: ["src/**/*.ts"],
      exclude: ["src/**/*.g.ts", "src/index.ts"],
    },
  },
});
