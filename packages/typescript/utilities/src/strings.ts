// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { falsey } from "./falsey.js";
import { DISPLAY_NAME_INVALID_RE, WHITESPACE_RE } from "./regex.js";

/**
 * Returns `undefined` when the input is null/undefined/empty/whitespace;
 * otherwise returns the trimmed string.
 *
 * TS naming follows the `*Undef*` convention (use
 * `undefined` for absent semantics, not `null`). The .NET mirror uses
 * `ToNullIfEmpty()` per C# convention: the TypeScript helpers use
 * `undefined` for absent (`*Undef*`), and the .NET mirrors use `null`
 * (`*Null*`).
 */
export function toUndefIfEmpty(
  input: string | null | undefined,
): string | undefined {
  if (falsey(input)) return undefined;
  return (input as string).trim();
}

/**
 * Trims leading/trailing whitespace and collapses any internal whitespace
 * runs (spaces, tabs, newlines) into a single space. Returns `undefined` if
 * the string is empty after cleaning.
 *
 * TS convention: returns `string | undefined`. The .NET
 * mirror `CleanStr()` returns `string?`: same absent semantics, with the
 * idiomatic token of each language.
 */
export function cleanStr(input: string | null | undefined): string | undefined {
  const trimmed = input?.trim();
  if (falsey(trimmed)) return undefined;
  return (trimmed as string).replace(WHITESPACE_RE, " ");
}

/**
 * Strips characters not allowed in display names then applies {@link cleanStr}.
 * Returns `undefined` if empty after cleaning.
 *
 * TS convention: returns `string | undefined`.
 */
export function cleanDisplayStr(
  input: string | null | undefined,
): string | undefined {
  if (falsey(input)) return undefined;
  const stripped = (input as string).replace(DISPLAY_NAME_INVALID_RE, "");
  return cleanStr(stripped);
}
