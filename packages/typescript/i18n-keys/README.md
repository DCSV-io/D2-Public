<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-i18n-keys

Exports the type-safe `TK` constants catalog. It mirrors `DcsvIo.D2.I18n.Keys` on the .NET side. Its only dependency
is the i18n-abstractions package, which supplies the `TKMessage` type and the `tk()` factory, so any package in the
graph can import `TK` constants without creating a circular dependency.

## Install

```bash
pnpm add @dcsv-io/d2-i18n-keys
```

## Public API

| Export  | Purpose                                                                                                                                                             |
| ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `TK`    | Nested object of `TKMessage` instances. Each instance's `.key` is its snake-case message key (e.g. `TK.common.errors.NOT_FOUND.key === "common_errors_NOT_FOUND"`). |
| `TKKey` | `string` type alias for a raw TK key value.                                                                                                                         |

Both are generated from `contracts/messages/en-US.json` by the D2 TypeScript catalog codegen, and the generated source
is committed. Do not edit `src/generated/tk-keys.g.ts` by hand: the next codegen run overwrites the changes.

## Dependencies

`@dcsv-io/d2-i18n-abstractions` (the `TKMessage` type + `tk()` factory the generated
constants are built from). That package has no dependencies, so this package is shallow in the dependency graph.

## Usage example

```ts
import { TK } from "@dcsv-io/d2-i18n-keys";

// TK.common.errors.NOT_FOUND === { key: "common_errors_NOT_FOUND" }
// Drop straight into D2Result.messages - no tk() wrapper needed:
//   notFound({ messages: [TK.common.errors.NOT_FOUND] })
console.log(TK.common.errors.NOT_FOUND.key);
```

## Why a separate package

Both `@dcsv-io/d2-result` (for factory default messages) and `@dcsv-io/d2-i18n` (the Translator and Paraglide runtime)
need the TK constants. If the constants lived in `@dcsv-io/d2-i18n`, packages that depend on `@dcsv-io/d2-result`
could not also import the constants transitively without creating a cycle (`result -> i18n -> result`). This package
keeps the constants out of that cycle: its only dependency is the dependency-free `@dcsv-io/d2-i18n-abstractions`, so
any package may import `@dcsv-io/d2-i18n-keys` regardless of where it sits in the dependency graph.

`@dcsv-io/d2-i18n` re-exports from this package, so `import { TK } from "@dcsv-io/d2-i18n/keys"` (the
`@dcsv-io/d2-i18n/keys` re-export) also works.

## Parity with .NET

Mirrors `DcsvIo.D2.I18n.Keys` (`TK.g.cs`). Both are generated from the same `contracts/messages/en-US.json` source
by separate .NET and TypeScript emitters, and both expose the TK constants as `TKMessage` instances built on the
abstractions package. The .NET and TypeScript catalogs therefore cannot drift apart.
