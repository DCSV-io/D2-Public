<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-auth-abstractions

Auth-related constants for TypeScript consumers - `Scopes` tree, `AuthErrorCodes`,
`AuthFailures` factories, `JwtClaimTypes`. Mirrors `DcsvIo.D2.Auth.Abstractions`; the .NET
auth error-code catalog and failure factories are not in a public package.

## Install

```bash
pnpm add @dcsv-io/d2-auth-abstractions
```

## Public API

| Export                                                  | Source                            | Mirror                                      |
| ------------------------------------------------------- | --------------------------------- | ------------------------------------------- |
| `Scopes` (nested constants)                             | `scopes.g.ts` (codegen)           | `Scopes.*.*` (.NET)                         |
| `ALL_SCOPES`                                            | `scopes.g.ts`                     | `Scopes.AllScopes`                          |
| `AuthErrorCodes`                                        | `auth-error-codes.g.ts` (codegen) | `AuthErrorCodes` (.NET, not public)         |
| `ALL_AUTH_ERROR_CODES` / `getAuthErrorHttpStatus(code)` | `auth-error-codes.g.ts`           | `AuthErrorCodes.AllCodes` / `GetHttpStatus` |
| `AuthFailures.<factory>()`                              | `auth-failures.g.ts` (codegen)    | `AuthFailures.*` (.NET, not public)         |
| `JwtClaimTypes`                                         | `jwt-claim-types.g.ts` (codegen)  | `DcsvIo.D2.Auth.Abstractions.JwtClaimTypes` |
| `JwtPayload`                                            | `jwt-payload.g.ts` (codegen)      | TS-only typed view over the same spec       |

## Codegen workflow

`prebuild` runs the scopes, error-codes and JWT-claims emitter scripts before `tsc -b`, so
`pnpm -r build` regenerates the generated files. The error-codes emitter writes both
`auth-error-codes.g.ts` and `auth-failures.g.ts`. Generated files (`*.g.ts`) are committed to git.

`JwtClaimTypes` and `JwtPayload` both emit from
`contracts/jwt-claims/jwt-claims.spec.json` (one generator, two outputs; sources committed):

- `JwtClaimTypes` - string-constant catalog (every claim's wire name).
- `JwtPayload` - TypeScript interface typed on every `standard` + `d2-custom`
  claim with stable per-field types. `inside-act` claims are nested
  inside `act` and are not top-level fields. The last field,
  `raw: Readonly<Record<string, unknown>>`, contains the raw decoded claims,
  for access to claims the spec does not list.

The .NET side consumes the same spec for `JwtClaimTypes` constants;
.NET reads claim values via `ClaimsPrincipal`, so it needs no typed payload.
The constant catalog cannot drift between the languages, because both read the same spec.

## Header constants

> Wire-protocol header catalogs live in the per-transport packages (`@dcsv-io/d2-headers-http`, `@dcsv-io/d2-headers-grpc`, `@dcsv-io/d2-headers-amqp`, `@dcsv-io/d2-headers-common`).

## Dependencies

- `@dcsv-io/d2-result` - `D2Result` shape returned by `AuthFailures.*` factories.

## Usage example

```ts
import {
  Scopes,
  AuthFailures,
  AuthErrorCodes,
  JwtClaimTypes,
} from "@dcsv-io/d2-auth-abstractions";
import { HttpHeaders } from "@dcsv-io/d2-headers-http";

// Scope check.
if (!ctx.scopes.has(Scopes.auth.user.impersonate.consent)) {
  return AuthFailures.scopeInsufficient(ctx.traceId);
}

// Header read.
const idempotency = req.headers[HttpHeaders.IDEMPOTENCY_KEY];

// Claim read.
const sub = jwtPayload[JwtClaimTypes.SUB];

// Error-code discrimination.
if (result.errorCode === AuthErrorCodes.LIB_AUTH_JWT_EXPIRED) {
  // refresh the token and retry
}
```

## Parity with .NET

Mirrors `DcsvIo.D2.Auth.Abstractions`, and the .NET auth error-code catalog, which is not public:

- `Scopes` tree - same dot-segmented spec names emitted as nested
  constants (e.g. `Scopes.auth.user.impersonate.consent`).
- `AuthErrorCodes` - same string values (every constant's value equals its name).
- `AuthFailures` - every factory returns `D2Result.fail(...)` with
  matching `errorCode` + `statusCode` + default `messageKey`.
- `JwtClaimTypes` - codegen-emitted from `contracts/jwt-claims/jwt-claims.spec.json`;
  same constant names + values on both sides.
- `JwtPayload` - TypeScript-only typed view emitted from the same spec; .NET reads
  claims via `ClaimsPrincipal` rather than a typed shape.

## Edge cases

- `getAuthErrorHttpStatus` returns 500 for unknown codes - a defensive
  default; every shipped `LIB_AUTH_*` code is in the table.
- `Scopes` includes a `_self` key at a branch node when an entry is both a leaf scope and has children. This case is
  rare; the `_self` key exists for forward compatibility.
- Generated files (`*.g.ts`) are committed to git.
