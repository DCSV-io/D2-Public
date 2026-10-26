// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

/**
 * Returns true when the value is null, undefined, an empty string, a
 * whitespace-only string, or an empty array, Set or Map.
 *
 * Mirrors the .NET `Falsey()` extension semantics from
 * `DcsvIo.D2.Utilities.Extensions.StringExtensions` /
 * `EnumerableExtensions` / `GuidExtensions`, except that the zero UUID string
 * is not treated as falsey here. The runtime kind check is permissive: strings,
 * arrays, sets and maps share a single boundary helper, so no type branch is
 * needed at the call site.
 */
export function falsey(value: unknown): boolean {
  if (value === null || value === undefined) return true;
  if (typeof value === "string") {
    if (value.length === 0) return true;
    return value.trim().length === 0;
  }
  if (Array.isArray(value)) return value.length === 0;
  if (value instanceof Set || value instanceof Map) return value.size === 0;
  return false;
}
