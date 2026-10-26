// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------
//
// Prebuild helper for public TypeScript packages.
//
// When the `ts-codegen` package is in the workspace, the script runs its emit
// script. Otherwise it skips regeneration and uses the committed `*.g.ts`
// files, which are the published sources.
//
// Usage (from a package dir):
//   node ../scripts/run-ts-codegen.mjs src/error-codes-emit.ts
//   node ../../scripts/run-ts-codegen.mjs src/headers-emit.ts --target=http
//
// -----------------------------------------------------------------------

import { execFileSync } from "node:child_process";

const args = process.argv.slice(2);

if (args.length === 0) {
  console.error(
    "usage: run-ts-codegen.mjs <emit-script-under-ts-codegen> [args...]",
  );
  process.exit(2);
}

function hasTsCodegen() {
  try {
    execFileSync(
      "pnpm",
      ["--filter", "ts-codegen", "exec", "node", "-e", "process.exit(0)"],
      {
        stdio: "pipe",
        shell: process.platform === "win32",
      },
    );
    return true;
  } catch {
    return false;
  }
}

if (!hasTsCodegen()) {
  console.log(
    "run-ts-codegen: ts-codegen not in workspace - skipping regen (committed generated sources)",
  );
  process.exit(0);
}

execFileSync("pnpm", ["--filter", "ts-codegen", "exec", "tsx", ...args], {
  stdio: "inherit",
  shell: process.platform === "win32",
});
