// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

/**
 * Matches one or more whitespace characters. No backtracking: a single greedy
 * quantifier with no following pattern.
 */
export const WHITESPACE_RE = /\s+/g;

/**
 * Matches characters NOT allowed in display names. Allowed: letters from
 * any Unicode script, digits, spaces, hyphens, apostrophes, periods,
 * commas. A single char-class with no quantifier, so no backtracking.
 */
export const DISPLAY_NAME_INVALID_RE = /[^\p{L}\p{N}\s\-'.,]/gu;

/**
 * Basic `local@domain.tld` email shape: a local part, then two or more domain
 * labels joined by single dots. Linear in the input length: each quantified
 * class excludes the character that ends it (`@` ends the local part, `.` ends
 * each domain label), so no two quantifiers match the same character. The
 * pattern has no length limit; callers that store the address bound it with
 * `EMAIL_MAX`. The .NET `StringExtensions.EmailRegex` has the same text.
 */
export const EMAIL_RE = /^[^@\s]+@[^@\s.]+(?:\.[^@\s.]+)+$/;

/**
 * Matches any string of 8-4-4-4-12 hex digits (any version or variant, and
 * nil), as .NET `Guid.TryParse` accepts. Used by
 * {@link tryParseTruthyUndefUuid}. No backtracking: fixed-length anchors.
 */
export const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * The canonical "empty" UUID: all zeros. The TS analog of
 * .NET `Guid.Empty`.
 */
export const EMPTY_UUID = "00000000-0000-0000-0000-000000000000";
