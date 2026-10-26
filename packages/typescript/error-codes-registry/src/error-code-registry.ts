// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { type ErrorCategory } from "@dcsv-io/d2-error-category";
import type { TKMessage } from "@dcsv-io/d2-i18n-abstractions";

// Re-export the relocated ErrorCategory, so
// `@dcsv-io/d2-error-codes-registry` still exports the `ErrorCategory` type.
export { type ErrorCategory } from "@dcsv-io/d2-error-category";

// ---------------------------------------------------------------------------
// ErrorCodeInfo - the per-code metadata record. Generated entries from
// `error-code-registry.g.ts` conform to this interface.
//
// Field alignment with the .NET runtime (must match exactly so the
// cross-runtime parity test passes), listed in the .NET record's field
// order:
//   code            -> string (SCREAMING_SNAKE wire-format code)
//   httpStatus      -> number (HTTP status integer)
//   category        -> ErrorCategory (closed string-union from @dcsv-io/d2-error-category)
//   userMessageKey  -> TKMessage (typed TK constant from @dcsv-io/d2-i18n-keys)
//   factoryName     -> string (PascalCase factory symbol)
//   factoryShape    -> ErrorCodeFactoryShape (standard / none)
//   doc             -> string (developer / JSDoc documentation text)
//   domain          -> string (business domain token; "common", "auth" and so on)
//   owner           -> string (gen or lib in this registry)
//   isDeprecated    -> boolean
// ---------------------------------------------------------------------------

// ErrorCategory is the relocated foundational @dcsv-io/d2-error-category leaf:
// the closed union generated from contracts/error-category/
// error-category.spec.json (the cross-runtime source). Re-exported from this
// package's barrel for backward compatibility.

/**
 * Closed factory-shape classification. Mirrors the canonical schema enum
 * byte-for-byte: `standard` is the one universal error-factory shape
 * (messages?, inputErrors?, errorCode?, category?, traceId? - all optional);
 * `none` emits the constant + boolean only (no factory).
 */
export type ErrorCodeFactoryShape = "standard" | "none";

/**
 * Full metadata record for one error code in the merged registry. Generated
 * from all `*-error-codes.spec.json` catalogs under `contracts/`. Mirrors the
 * .NET `ErrorCodeInfo` readonly record struct: same field names in the same
 * order. A cross-runtime parity test enforces the parity.
 */
export interface ErrorCodeInfo {
  /** Wire-format error code (SCREAMING_SNAKE). */
  readonly code: string;
  /** HTTP status mapping for transport. */
  readonly httpStatus: number;
  /** Closed semantic and telemetry classification. */
  readonly category: ErrorCategory;
  /**
   * Translation-key reference as a typed `TKMessage` constant from
   * `@dcsv-io/d2-i18n-keys`. Read `.key` for the raw snake_case wire key.
   */
  readonly userMessageKey: TKMessage;
  /** PascalCase factory symbol (e.g. `BearerMissing`, `NotFound`). */
  readonly factoryName: string;
  /** Factory signature variant. */
  readonly factoryShape: ErrorCodeFactoryShape;
  /** Developer documentation text. */
  readonly doc: string;
  /**
   * Business domain token, decided by the catalog the code was generated
   * from. The README lists the catalog-to-domain map this registry contains.
   */
  readonly domain: string;
  /**
   * Owner token, decided by the same catalog identity: `gen` for the generic
   * catalog, `lib` for a framework-library catalog. The schema reserves
   * further owner tokens for catalogs that ship outside this package.
   */
  readonly owner: string;
  /** `true` when the spec entry carries `"deprecated": true`. */
  readonly isDeprecated: boolean;
}

// ---------------------------------------------------------------------------
// ErrorCodeRegistry - the resolution API. Backed by a frozen Map for O(1)
// ordinal case-sensitive lookup. Static (no DI): the data is frozen at
// compile time with no runtime config.
//
// Resolution semantics (hard not-found):
//   resolve("UNKNOWN_CODE")  -> undefined
//   has("UNKNOWN_CODE")      -> false
//   Unknown codes are not synthesized; the caller supplies any fallback.
// ---------------------------------------------------------------------------

/**
 * Merged error-code registry. Aggregates all `*-error-codes.spec.json` catalogs
 * from `contracts/` into a single frozen `code -> ErrorCodeInfo` lookup table.
 *
 * Use `resolve(code)` for single-code lookups. Use `has(code)` when only
 * membership is needed. Use `all` to iterate every registered code.
 */
export interface ErrorCodeRegistry {
  /**
   * Resolve a wire error code to its full metadata.
   * Returns `undefined` for any code not in the registry (hard not-found;
   * the caller supplies any fallback logic).
   */
  resolve(code: string): ErrorCodeInfo | undefined;

  /**
   * Returns `true` if `code` is present in the registry, `false` otherwise.
   * Ordinal case-sensitive - `"not_found"` != `"GEN_NOT_FOUND"`.
   */
  has(code: string): boolean;

  /** Every registered code in the order they appear in `error-code-registry.g.ts`. */
  readonly all: readonly ErrorCodeInfo[];
}

/**
 * Build a frozen {@link ErrorCodeRegistry} from the generated entries array.
 * Called once at module load time in `error-code-registry.g.ts`.
 *
 * @internal - exported for the generated file only; use
 *   `errorCodeRegistry` from `@dcsv-io/d2-error-codes-registry` instead.
 */
export function buildRegistry(
  entries: readonly ErrorCodeInfo[],
): ErrorCodeRegistry {
  const map = new Map<string, ErrorCodeInfo>();
  for (const entry of entries) map.set(entry.code, entry);
  Object.freeze(map);

  return Object.freeze({
    resolve(code: string): ErrorCodeInfo | undefined {
      return map.get(code);
    },
    has(code: string): boolean {
      return map.has(code);
    },
    all: Object.freeze([...entries]) as readonly ErrorCodeInfo[],
  });
}
