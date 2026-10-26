// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

export type { ITranslator } from "./i-translator.js";
export {
  SupportedLocales,
  type SupportedLocalesConfig,
  loadSupportedLocalesConfig,
} from "./supported-locales.js";
export { Translator, type LocaleCatalogs } from "./translator.js";
// Re-export the TKMessage primitives from @dcsv-io/d2-i18n-abstractions so
// @dcsv-io/d2-i18n exposes the message shape and factory without a second import.
export { type TKMessage, tk } from "@dcsv-io/d2-i18n-abstractions";
