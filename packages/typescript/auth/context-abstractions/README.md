<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-auth-context-abstractions

`IAuthContext` interface + supporting enums/types. Emitted from
`contracts/auth-context/IAuthContext.spec.json` (generated sources committed).
Mirrors `DcsvIo.D2.AuthContext.Abstractions` (.NET).

## Install

```bash
pnpm add @dcsv-io/d2-auth-context-abstractions
```

## Public API

Generated artifacts (committed to git):

| Export                                                 | Source                                                   |
| ------------------------------------------------------ | -------------------------------------------------------- |
| `IAuthContext`                                         | `IAuthContext.g.ts`                                      |
| `IAuthContextRedactPaths`                              | `IAuthContext.g.ts` (PII paths from spec `redact: true`) |
| `OrgType` / `Role` / `ImpersonationKind` / `ActorKind` | `enums/*.g.ts`                                           |
| `ActorEntry`                                           | `types/actor-entry.g.ts`                                 |

## Codegen workflow

`prebuild` runs the `auth-context-emit.ts` script before `tsc -b`, so
`pnpm -r build` regenerates the generated files. The generated files ship
committed, and codegen in the repository that produces this package refreshes them
when the contract they are emitted from changes.

## Dependencies

None at the package level (interfaces only). The codegen runner
(codegen tooling when present) is a build-time dependency, not a runtime one.

## Usage example

```ts
import type { IAuthContext } from "@dcsv-io/d2-auth-context-abstractions";
import {
  OrgType,
  IAuthContextRedactPaths,
} from "@dcsv-io/d2-auth-context-abstractions";
import { setupLogger } from "@dcsv-io/d2-logging";

const log = setupLogger({
  serviceName: "orders",
  redactPaths: [IAuthContextRedactPaths],
});

function describe(ctx: IAuthContext): string {
  return `${ctx.userId ?? "anon"} on ${ctx.orgId ?? "no-org"} (${
    ctx.orgType ?? OrgType.Customer
  })`;
}
```

## Parity with .NET

Mirrors `DcsvIo.D2.AuthContext.Abstractions`:

- `IAuthContext` - same property set, camelCased per TS conventions.
- `OrgType` / `Role` / `ImpersonationKind` / `ActorKind` - same wire
  values (string-literal unions).
- `ActorEntry` - same field shape.

The .NET-side codegen lives in `DcsvIo.D2.Context.SourceGen` (Roslyn
incremental generator); the TS-side catalogs are committed generated sources.
Both consume the same `IAuthContext.spec.json`.

## Nullability convention

Spec-emitted property types use `T | null` rather than the TypeScript
`T?` / `T | undefined` convention. This mirrors the .NET side, where
`Nullable<T>` carries an explicit `null` value; serializing the context
envelope across the language boundary preserves the `null` literal so
both sides round-trip the same shape. Optional-chaining and nullish-
coalescing handle either `null` or `undefined` at consumer call sites,
so most callers do not notice this convention. It matters only when you
construct or destructure an `IAuthContext` literal directly.

New domain code outside the codegen-emitted surface should use the
default TS convention (`T?` / `T | undefined`) per the wider TS
codebase's nullability rule. The spec's `?` suffix on a type entry
(e.g. `"string?"` in `IAuthContext.spec.json`) is what triggers the
`| null` emission.

## Edge cases

- `IAuthContextRedactPaths` is empty when no spec entry has
  `redact: true`. Hand-register additional paths via
  `markRedactedFields()` from `@dcsv-io/d2-logging` if needed.
- Generated files (`*.g.ts`) are committed to git, so consumers do not
  run codegen on their first build.
- `pnpm exec eslint` ignores generated files - formatting drift is
  irrelevant for spec-derived output.
