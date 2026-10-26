// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { falsey } from "@dcsv-io/d2-utilities";
import type { InputError } from "./input-error.js";
import type { TKMessage } from "@dcsv-io/d2-i18n-abstractions";

/**
 * Locale-aware translator function. Implementations bind `key + params`
 * to a localized string (typically backed by Paraglide on the
 * BFF/browser, or an `ITranslator` implementation on a .NET server).
 * The function receives the raw `key` plus an OPTIONAL parameter
 * dictionary; implementations MUST tolerate an undefined `params`.
 *
 * @example
 * ```ts
 * import { m } from "@dcsv-io/d2-i18n";
 *
 * const translate: TranslateFn = (key, params) => {
 *   const fn = (m as Record<string, (p?: Record<string, unknown>) => string>)[key];
 *   return fn === undefined ? key : fn(params);
 * };
 * ```
 */
export type TranslateFn = (
  key: string,
  params?: Readonly<Record<string, unknown>>,
) => string;

/**
 * Renders a single `TKMessage` to a localized string using the supplied
 * translator. The result is final text rather than the i18n-preserving
 * `TKMessage` shape.
 *
 * Rendering belongs where the client locale is known and final text is
 * displayed. The wire path keeps
 * `TKMessage` shapes end-to-end, because pre-rendering at the server removes
 * client-side locale switching.
 *
 * @param message The translation-key message to render.
 * @param translate Locale-aware translator function.
 * @returns The localized string, exactly as `translate` returns it. The
 *  function adds no fallback; for a key the translator does not recognize,
 *  the result is the value `translate` returns for it (the translator in the
 *  `TranslateFn` example returns the key verbatim).
 */
export function renderMessage(
  message: TKMessage,
  translate: TranslateFn,
): string {
  return translate(message.key, message.params);
}

/**
 * Renders an array of `TKMessage`s to localized strings. Empty input
 * returns an empty array (does not throw). Null / undefined input is
 * normalized to an empty array, as protection against untrusted
 * gateway responses where the field MAY be omitted.
 *
 * Wire-boundary carve-out: the parameter accepts
 * `null` because gateway response envelopes (deserialized from JSON over
 * the wire) may hold `null` for missing message arrays; this boundary
 * absorbs both `null` and `undefined` and returns an empty array.
 *
 * @param messages Array of `TKMessage` to render. `undefined` / `null`
 *  treated as empty.
 * @param translate Locale-aware translator function.
 * @returns Array of localized strings, one per input message.
 */
export function renderMessages(
  messages: readonly TKMessage[] | undefined | null,
  translate: TranslateFn,
): string[] {
  if (falsey(messages)) return [];
  return messages!.map((m) => renderMessage(m, translate));
}

/**
 * Renders an array of `InputError`s to a field-name -> string[] map.
 * Boundary helper for Superforms-style form error display where each
 * field has a list of human-readable error strings.
 *
 * Empty / undefined / null input returns an empty object. Duplicate
 * fields are merged (subsequent errors append to the existing array).
 * Entries with empty field name or zero errors are skipped.
 *
 * Wire-boundary carve-out: see {@link renderMessages}.
 *
 * @param inputErrors Array of `InputError` to render. `undefined` /
 *  `null` treated as empty.
 * @param translate Locale-aware translator function.
 * @returns A map keyed by field name whose values are localized error
 *  strings for that field.
 */
export function renderInputErrors(
  inputErrors: readonly InputError[] | undefined | null,
  translate: TranslateFn,
): Record<string, string[]> {
  const result: Record<string, string[]> = {};
  if (falsey(inputErrors)) return result;

  for (const ie of inputErrors!) {
    if (!ie.field || ie.errors.length === 0) continue;
    const rendered = ie.errors.map((m) => renderMessage(m, translate));
    const existing = result[ie.field];
    if (existing) existing.push(...rendered);
    else result[ie.field] = rendered;
  }
  return result;
}
