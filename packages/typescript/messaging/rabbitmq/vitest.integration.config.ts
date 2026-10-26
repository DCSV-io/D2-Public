// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    // Testcontainer-backed integration suite: replays real .NET-emitted golden
    // messages through a live RabbitMQ broker. The suite manages its own
    // container (Testcontainers). It has no coverage gate and uses generous
    // timeouts for the image pull and broker boot.
    include: ["tests/integration/**/*.test.ts"],
    testTimeout: 120_000,
    hookTimeout: 180_000,
  },
});
