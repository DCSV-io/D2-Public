// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

const here = dirname(fileURLToPath(import.meta.url));

function findRepoRoot(): string {
  // Walk up from this test file looking for the repo root marker
  // (a directory containing contracts/protos/).
  let dir = here;
  for (let i = 0; i < 12; i++) {
    if (existsSync(join(dir, "contracts", "protos"))) return dir;
    const parent = resolve(dir, "..");
    if (parent === dir) break;
    dir = parent;
  }
  throw new Error(
    "could not locate repo root (expected contracts/protos/ ancestor)",
  );
}

const repoRoot = findRepoRoot();

describe("@dcsv-io/d2-protos - generation surface", () => {
  it("contracts/protos/ exists at repo root", () => {
    const protosDir = join(repoRoot, "contracts", "protos");
    expect(existsSync(protosDir)).toBe(true);
  });

  it("buf.gen.yaml is present in this package", () => {
    expect(existsSync(join(here, "..", "buf.gen.yaml"))).toBe(true);
  });

  it("the proto module contains d2/common/v2/{d2_result,health,jobs,ping}.proto", () => {
    const commonV2 = join(
      repoRoot,
      "contracts",
      "protos",
      "d2",
      "common",
      "v2",
    );

    expect(readdirSync(commonV2).sort()).toEqual([
      "d2_result.proto",
      "health.proto",
      "jobs.proto",
      "ping.proto",
    ]);
  });

  it("src/generated contains only d2/common/v2", () => {
    const generated = join(here, "..", "src", "generated");

    expect(readdirSync(generated)).toEqual(["d2"]);
    expect(readdirSync(join(generated, "d2"))).toEqual(["common"]);
    expect(readdirSync(join(generated, "d2", "common"))).toEqual(["v2"]);
    expect(readdirSync(join(generated, "d2", "common", "v2")).sort()).toEqual([
      "d2_result.ts",
      "health.ts",
      "jobs.ts",
      "ping.ts",
    ]);
  });

  it("no generated file contains the d2.common.v1 package", () => {
    const v2 = join(here, "..", "src", "generated", "d2", "common", "v2");

    for (const name of readdirSync(v2)) {
      const content = readFileSync(join(v2, name), "utf8");

      expect(content, name).not.toContain("d2.common.v1");
      expect(content, name).toContain('protobufPackage = "d2.common.v2"');
    }
  });
});
