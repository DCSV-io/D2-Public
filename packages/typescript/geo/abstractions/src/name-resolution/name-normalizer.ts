// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { falsey } from "@dcsv-io/d2-utilities";

/**
 * Name-normalization pipeline used by the geo name-resolver fuzzy matcher
 * to fold user-supplied free-form text down to a canonical comparison
 * form. Pure function: no I/O, no shared state, thread safe by design.
 *
 * Mirrors .NET `DcsvIo.D2.Geo.Abstractions.NameResolution.NameNormalizer`
 * byte-for-byte; the same input must produce the same output on both
 * runtimes. Cross-language parity is pinned via the byte-equivalent
 * fixture exercised by the parity test suite.
 *
 * Pipeline (applied in the same order as .NET. The step order determines the
 * output, so do not reorder the steps without auditing both runtimes,
 * especially when adding case-sensitive transforms):
 *
 * 1. **Empty / whitespace-only short-circuit**: returns `""`, so raw user
 *    input needs no prior check for blanks.
 *    Implemented via `falsey()` from `@dcsv-io/d2-utilities` (the parity helper
 *    that mirrors the .NET `Falsey()` extension semantics: null /
 *    undefined / empty / whitespace-only all collapse to `true`). The
 *    function signature rejects `null` / `undefined` at compile time, so
 *    the runtime null branches are unreachable from typed callers; the
 *    helper still handles the empty and whitespace-only cases.
 * 2. **NFD decomposition**: splits accented characters into a base letter
 *    and a combining mark so the marks can be stripped in the next pass.
 * 3. **Strip combining marks**: the `\p{M}` Unicode property removes every
 *    diacritic or accent (`"São Paulo"` -> `"Sao Paulo"`).
 * 4. **Spaced ampersand-token substitution**: replaces the spaced form
 *    `" & "` (with whitespace on both sides) with `" and "` so
 *    `"Trinidad & Tobago"` and `"Trinidad and Tobago"` compare equal.
 *    The spaced-form requirement deliberately preserves unspaced
 *    ampersands like `"AT&T"`, `"M&M's"`, `"R&D"`. This step runs before
 *    casefolding to match the .NET pipeline order exactly (the substring
 *    is case-invariant so the output is byte-equivalent either way, but
 *    the order is kept aligned so later case-sensitive steps cannot
 *    silently drift).
 * 5. **Locale-invariant lower-case**: `String.prototype.toLowerCase()`
 *    without a locale argument is ECMAScript locale-independent (matches
 *    .NET `CultureInfo.InvariantCulture.TextInfo.ToLower`). Note: a
 *    handful of edge-case Unicode code points (e.g. Turkish dotted-I
 *    `İ`) fold differently between ECMAScript's default case-fold and
 *    .NET's `ToLowerInvariant`. These are exceedingly rare in geo
 *    reference data, and the cross-language parity fixture detects any
 *    real drift.
 * 6. **Trim outer whitespace**: `.trim()` removes leading and trailing
 *    whitespace.
 * 7. **Collapse internal whitespace**: runs of one or more whitespace
 *    characters reduce to a single ASCII space (`"United  States"` ->
 *    `"United States"`).
 *
 * Pure (no closure state, no module-level mutation), so it is safe to call
 * concurrently.
 *
 * @param input - the free-form text to normalize. Empty / whitespace-only
 *   input returns `""`. Callers that need to distinguish "no input" from
 *   "valid-but-not-found" should check `=== ""` (or `falsey()` from
 *   `@dcsv-io/d2-utilities`) after the call.
 * @returns the normalized comparison key, or `""` for empty /
 *   whitespace-only input.
 */
export function normalize(input: string): string {
  // 1. Empty / whitespace-only short-circuit via the shared `falsey()`
  //    helper from `@dcsv-io/d2-utilities`. It mirrors the .NET `input.Falsey()`
  //    extension semantics so both runtimes collapse the same set of
  //    "no signal" inputs (null / undefined / empty / whitespace-only).
  if (falsey(input)) return "";

  // 2. NFD decomposition.
  // 3. Strip combining marks (Unicode property \p{M}).
  const stripped = input.normalize("NFD").replace(/\p{M}/gu, "");

  // 4. Spaced ampersand-token substitution: spaced form only, so it preserves
  //    AT&T / M&M's / R&D / etc. Applied before casefold to match the
  //    .NET pipeline order exactly.
  const ampersandSwapped = stripped.replaceAll(" & ", " and ");

  // 5. Locale-invariant lowercase (matches .NET ToLowerInvariant; see
  //    TSDoc above for the Turkish dotted-I caveat).
  const lowered = ampersandSwapped.toLowerCase();

  // 6. Trim outer whitespace.
  // 7. Collapse internal whitespace runs to a single ASCII space.
  return lowered.trim().replace(/\s+/g, " ");
}
