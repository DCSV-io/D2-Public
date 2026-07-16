// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { falsey } from "./falsey.js";

/**
 * Inverse of `falsey` — returns true when the value is non-null, non-empty,
 * and (for strings) contains at least one non-whitespace character.
 */
export function truthy(value: unknown): boolean {
  return !falsey(value);
}
