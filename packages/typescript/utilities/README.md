<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-utilities

Helpers for values at a boundary: `falsey` / `truthy` checks, string cleaning, parse functions that return
`undefined` on failure, indexed environment-variable array parsing, and the regex constants shared across the
TypeScript packages. Mirrors `DcsvIo.D2.Utilities` (.NET).

## Install

```bash
pnpm add @dcsv-io/d2-utilities
```

## Public API

| Export                                                                              | Purpose                                                                                                                                                         |
| ----------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `falsey(value)`                                                                     | True when the value is null, undefined, an empty or whitespace string, or an empty collection.                                                                  |
| `truthy(value)`                                                                     | Inverse of `falsey`.                                                                                                                                            |
| `toUndefIfEmpty(s)`                                                                 | Returns the trimmed string, or `undefined` when the input is empty or whitespace.                                                                               |
| `cleanStr(s)`                                                                       | Trims, collapses each whitespace run to one space, and returns `undefined` when the result is empty.                                                            |
| `cleanDisplayStr(s)`                                                                | Strips characters outside the display-name allowlist, then applies `cleanStr`.                                                                                  |
| `tryParseTruthyUndefUuid(s)`                                                        | Returns the canonical lowercase UUID on success and `undefined` otherwise (the empty UUID gives `undefined`).                                                   |
| `tryParseTruthyUndefInt(s)`                                                         | Returns the parsed integer on success and `undefined` otherwise.                                                                                                |
| `tryParseTruthyUndefEnum(enumObj, s)`                                               | Returns the canonical key on a case-insensitive match and `undefined` otherwise.                                                                                |
| `chunk(arr, size)`                                                                  | Splits `arr` into consecutive chunks of `size`; throws when `size < 1`.                                                                                         |
| `clean(items, cleaner, opts?)`                                                      | Applies a per-element cleaner to any `Iterable<T>`. `opts` selects the null and empty handling; the defaults match the .NET version.                            |
| `parseEnvArray(prefix, env)`                                                        | Reads indexed environment-variable arrays such as `PREFIX__0=a, PREFIX__1=b, ...`; stops at the first gap.                                                      |
| `uuidv7(now?)`                                                                      | Generates a time-ordered RFC 9562 UUIDv7: a 48-bit millisecond timestamp prefix followed by random bits. The optional `now` argument injects a clock for tests. |
| `WHITESPACE_RE` / `DISPLAY_NAME_INVALID_RE` / `EMAIL_RE` / `UUID_RE` / `EMPTY_UUID` | Pre-built regular expressions and the canonical empty-UUID constant.                                                                                            |

## Dependencies

None. The package has no runtime dependencies.

## Usage example

```ts
import { falsey, tryParseTruthyUndefUuid, parseEnvArray } from "@dcsv-io/d2-utilities";

if (falsey(input)) return undefined;

const id = tryParseTruthyUndefUuid(headers["x-org-id"]);
if (id === undefined) throw new Error("missing org id");

// Reads AUTH_AUDIENCES__0, AUTH_AUDIENCES__1, ... and stops at the first gap.
const audiences = parseEnvArray("AUTH_AUDIENCES", process.env);
```

## Parity with .NET

Mirrors `DcsvIo.D2.Utilities` extensions:

- `falsey` / `truthy` -> `Falsey()` / `Truthy()` extensions on string / collection / Guid
- `toUndefIfEmpty` -> `ToNullIfEmpty()` on string (same behavior; TS returns `undefined` and .NET returns `null`; see
  [Naming divergence from .NET](#naming-divergence-from-net))
- `cleanStr` / `cleanDisplayStr` -> `CleanStr()` / `CleanDisplayStr()` on string
- `tryParseTruthyUndefUuid` / `tryParseTruthyUndefInt` / `tryParseTruthyUndefEnum` -> `TryParseTruthyNull(out ...)`
  extensions (same behavior; TS returns `undefined` and .NET returns `null`)
- `clean(items, cleaner, opts)` -> `IEnumerable<T>.Clean(cleaner, enumEmptyBehavior, valueNullBehavior)` extension.
  It has the same default behaviors (`ReturnNull` / `RemoveNulls`) and the same throw conditions on each side.
- `parseEnvArray` -> matches `IConfiguration` array binding (`PREFIX__INDEX` keys)

### Naming divergence from .NET

TS helpers use `*Undef*` names where the .NET equivalent uses `*Null*`. Each name follows the language's convention for
an absent value: `undefined` in TS, and `null` in .NET per BCL convention. The behavior is functionally equivalent.

## Edge cases

- `tryParseTruthyUndefUuid` rejects the empty UUID (`"00000000-..."`), as .NET collapses `Guid.Empty`.
- `parseEnvArray` stops at the first gap, so sparse arrays collapse to the dense prefix, as in .NET `IConfiguration`.
- `cleanStr` keeps a single space between words and never returns a whitespace-only string.
- `tryParseTruthyUndefInt` accepts integers only and rejects floats, scientific notation and integers outside
  `Number.MIN_SAFE_INTEGER` to `Number.MAX_SAFE_INTEGER`.
- `clean` accepts any `Iterable<T>` (arrays, sets, map values, generators). It always materializes its result as an
  array, except the `null` that the `ReturnNull` behavior returns, and it exhausts a generator in one pass.
