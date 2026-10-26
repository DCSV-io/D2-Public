// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { falsey } from "./falsey.js";

/**
 * Boundary helper that returns the trimmed string when `input` has any
 * non-whitespace content, otherwise returns `undefined`. Mirrors the .NET
 * `name.Falsey()` short-circuit pattern: raw user input passes through this
 * helper at the boundary, so downstream code only sees `string` (the
 * narrowed truthy form) and does not re-check null / undefined / empty /
 * whitespace.
 *
 * Use this helper at every public-API boundary where the input may be
 * null / undefined / empty / whitespace-only strings. It returns the trimmed
 * form, so a second `.trim()` call is unnecessary.
 *
 * Wire-boundary carve-out: the `input` parameter is
 * `string | null | undefined` because this helper EXISTS to absorb wire
 * `null` (cookies / headers / DB columns / JSON values) and normalize to
 * `undefined`. Domain code consumes the `string | undefined` return.
 *
 * @param input - the raw string to normalize.
 * @returns the trimmed string when truthy; `undefined` otherwise.
 */
export function truthyOrUndefined(
  input: string | null | undefined,
): string | undefined {
  if (falsey(input)) return undefined;
  return (input as string).trim();
}
