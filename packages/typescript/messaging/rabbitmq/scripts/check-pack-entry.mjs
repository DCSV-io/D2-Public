// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { isAbsolute, resolve, basename, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { lstatSync } from "node:fs";

export function checkPackEntry(environment, packageRoot) {
  const destination = environment.D2_PATCHED_PACK_INTERMEDIATE;
  if (
    typeof destination !== "string" ||
    !isAbsolute(destination) ||
    basename(destination) !== "source" ||
    !basename(dirname(destination)).startsWith("d2-rabbitmq-pack-") ||
    resolve(destination) === resolve(packageRoot)
  ) {
    throw new Error(
      "Use pnpm run pack:patched -- <destination>; direct source packing is not supported.",
    );
  }
  const stat = lstatSync(destination);
  if (!stat.isDirectory() || stat.isSymbolicLink())
    throw new Error(
      "Intermediate pack destination must be an owned directory.",
    );
}

if (
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
  checkPackEntry(
    process.env,
    resolve(dirname(fileURLToPath(import.meta.url)), ".."),
  );
}
