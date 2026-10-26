<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-error-category

Foundational zero-dependency leaf that exports the closed `ErrorCategory` classification - the semantic/telemetry class every `D2Result` and every error code carries. Mirrors `DcsvIo.D2.ErrorCodes.Category` on the .NET side. Lives here so `@dcsv-io/d2-result` and `@dcsv-io/d2-error-codes-registry` can import `ErrorCategory` without risking a circular dependency.

The category is the producer's coarse signal - `infrastructure_unavailable`, `not_found`, `validation_failure` - so a consumer can do generic class-based handling (retry any `infrastructure_unavailable`) without importing the producer's error-code catalog.

## Install

```bash
pnpm add @dcsv-io/d2-error-category
```

## Public API

| Export                 | Purpose                                                                                                                                                          |
| ---------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ErrorCategory`        | The string-union of snake_case wire strings (e.g. `"not_found"`). The wire string is the union value.                                                            |
| `ErrorCategoryWire`    | `const` object mapping each PascalCase member name -> wire string (e.g. `ErrorCategoryWire.NotFound === "not_found"`). Mirrors the .NET `ErrorCategory` members. |
| `ALL_ERROR_CATEGORIES` | Readonly array of every wire string in canonical (ordinal) order.                                                                                                |

All of them are generated from `contracts/error-category/error-category.spec.json` (sources committed). Do not edit `src/generated/error-category.g.ts` by hand - changes are overwritten on the next codegen run.

## Dependencies

None. This is a zero-dependency leaf so any package may import it regardless of where it sits in the graph.

## Usage example

```ts
import { type ErrorCategory, ErrorCategoryWire } from "@dcsv-io/d2-error-category";

function isRetryable(category: ErrorCategory): boolean {
  return (
    category === ErrorCategoryWire.InfrastructureUnavailable ||
    category === ErrorCategoryWire.RateLimited
  );
}
```

## Why a separate package

`ErrorCategory` is needed by both `@dcsv-io/d2-result` (the typed `D2Result.category` field) and
`@dcsv-io/d2-error-codes-registry` (the `ErrorCodeInfo.category` field). Keeping it in a zero-dependency leaf package
means neither of those packages has to import the other to use it, so no import cycle can form.
`@dcsv-io/d2-i18n-keys` does the same for the TK constants.

## Parity with .NET

Mirrors `DcsvIo.D2.ErrorCodes.Category` (`ErrorCategory.g.cs`). Both are generated from the same
`contracts/error-category/error-category.spec.json` source. A cross-runtime parity fixture
asserts that the .NET enum wire set, this TS union and the
spec are the same set, so the three cannot differ without the fixture failing.
