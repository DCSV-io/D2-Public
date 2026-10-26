<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-error-codes-registry

Merged **public** cross-catalog error-code registry for D2. Aggregates every
public `*-error-codes.spec.json` catalog (GEN + public LIB) into a single frozen
`code -> ErrorCodeInfo` lookup table - so any consuming service can resolve
a wire error code to its full metadata (httpStatus, semantic category,
user-message TK key, factory name/shape, doc, domain, owner, isDeprecated)
without importing the producer's catalog.

## Install

```bash
pnpm add @dcsv-io/d2-error-codes-registry
```

## Key facts

- **Generated**: `src/generated/error-code-registry.g.ts` is auto-generated
  from the public error-codes specs and ships committed - do not hand-edit.
  The producing repository's codegen regenerates it from those specs whenever
  a catalog changes, and the regenerated file arrives here as a release.
- **Leaf package**: depends only on `@dcsv-io/d2-error-category`, `@dcsv-io/d2-i18n-abstractions` and
  `@dcsv-io/d2-i18n-keys`; application-boundary hosts depend on this package to resolve foreign wire codes.
- **Collision-safe**: the emitter hard-fails at codegen time on any
  cross-catalog code collision (`D2ERC004`) or reserved-namespace violation
  (`D2ERC005`). No registry is emitted until violations are resolved.
- **Cross-runtime parity**: mirrors `DcsvIo.D2.ErrorCodes.Registry` (.NET)
  - same fields in the same order, same domain tokens, same owner tokens, same
  category wire-strings. This registry merges the public catalogs only.

## API

```ts
import {
  errorCodeRegistry,
  type ErrorCodeInfo,
} from "@dcsv-io/d2-error-codes-registry";

// Resolve a wire code -> full metadata (undefined on unknown code).
const info: ErrorCodeInfo | undefined =
  errorCodeRegistry.resolve("LIB_AUTH_JWT_EXPIRED");
if (info !== undefined) {
  info.code; // "LIB_AUTH_JWT_EXPIRED"
  info.httpStatus; // 401
  info.category; // "validation_failure"
  info.userMessageKey; // TK.auth.errors.UNAUTHORIZED (TKMessage)
  info.factoryName; // "JwtExpired"
  info.factoryShape; // "standard"
  info.doc; // "JWT is expired..."
  info.domain; // "auth"
  info.owner; // "lib"
  info.isDeprecated; // false
}

// Membership check.
errorCodeRegistry.has("GEN_NOT_FOUND"); // true

// Iterate all registered codes.
for (const entry of errorCodeRegistry.all) {
  /* ... */
}
```

## ErrorCodeInfo fields

The rows below are in the order the .NET `ErrorCodeInfo` record declares them,
which is the order the cross-runtime parity test compares.

| Field            | Type                    | Source                                         |
| ---------------- | ----------------------- | ---------------------------------------------- |
| `code`           | `string`                | SCREAMING_SNAKE owner-prefixed wire code       |
| `httpStatus`     | `number`                | HTTP status integer                            |
| `category`       | `ErrorCategory`         | Closed string-union (schema `category`)        |
| `userMessageKey` | `TKMessage`             | Typed TK constant from `@dcsv-io/d2-i18n-keys` |
| `factoryName`    | `string`                | PascalCase factory symbol                      |
| `factoryShape`   | `ErrorCodeFactoryShape` | `standard` / `none`                            |
| `doc`            | `string`                | Developer documentation text                   |
| `domain`         | `string`                | Business domain token (see below)              |
| `owner`          | `string`                | Owner token: `gen` / `lib` in this registry    |
| `isDeprecated`   | `boolean`               | Spec deprecation flag                          |

## Domain tokens (public catalogs)

The `domain` / `owner` fields come from catalog identity (filename map):

- `contracts/error-codes/error-codes.spec.json` -> domain `"common"`, owner `"gen"`, prefix `GEN_`
- `contracts/auth-error-codes/auth-error-codes.spec.json` -> domain `"auth"`, owner `"lib"`, prefix `LIB_AUTH_`
- `contracts/messaging-error-codes/messaging-error-codes.spec.json` -> domain `"messaging"`, owner `"lib"`, prefix `LIB_MESSAGING_`
- `contracts/pagination-error-codes/pagination-error-codes.spec.json` -> domain `"pagination"`, owner `"lib"`, prefix `LIB_PAGINATION_`

## Build

```sh
pnpm --filter @dcsv-io/d2-error-codes-registry run build
pnpm --filter @dcsv-io/d2-error-codes-registry run test
```
