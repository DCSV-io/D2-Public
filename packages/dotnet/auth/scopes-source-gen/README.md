<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Auth.Scopes.SourceGen

> Parent: [`packages/dotnet/`](../../README.md)

**Input contract:** [`contracts/auth-scopes/`](../../../../contracts/auth-scopes/README.md)

Roslyn incremental source generator that emits scope catalogs from `contracts/auth-scopes/scopes.spec.json` via
`<AdditionalFiles>`.

Assembly-name gate: the consuming assembly `DcsvIo.D2.Auth.Abstractions` receives `Scopes` under the
`DcsvIo.D2.Auth.Abstractions` namespace, built from its own `scopes.spec.json` AdditionalFiles entry. Another assembly
receives a scope class only when its project sets the MSBuild properties `D2ScopesExtensionNamespace` and
`D2ScopesExtensionClassPrefix` and declares each as a `CompilerVisibleProperty` item. `D2ScopesExtensionNamespace` sets
the namespace, and `D2ScopesExtensionClassPrefix` sets the identifier placed before `Scopes` in the class name. That
class covers the union of every `scopes.spec.json` the assembly lists in AdditionalFiles and is written to the class
name followed by `.g.cs`. The public assembly ignores these properties, and an assembly that sets neither receives no
scope class.

The spec file is the single source of truth for the platform's scope catalog. Every scope a handler can require, every
scope the token issuer mints into a token, and every grant-matrix entry is defined in one JSON file, so no hand-written
parallel constants exist that could drift per feature.

Convention: the generator follows the spec-driven Roslyn `IIncrementalGenerator` pattern (file layout, diagnostic ID
convention, generator anatomy, `<AdditionalFiles>` wiring).

---

## Build-time diagnostics

| ID         | Severity | Trigger                                                                                                                                                        |
| ---------- | -------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `D2SCP001` | Error    | Spec file is malformed JSON or violates the schema                                                                                                             |
| `D2SCP002` | Error    | `grantedTo` references unknown `OrgType` / `Role` enum value                                                                                                   |
| `D2SCP003` | Error    | Scope name violates naming convention (lowercase dot-separated; segments must be valid C# identifiers; >= 2 segments; no repeated / edge dots)                 |
| `D2SCP004` | Error    | Duplicate scope name                                                                                                                                           |
| `D2SCP005` | Warning  | Anonymous scope marked `impersonationBlocked` (meaningless - anon scopes are pre-auth)                                                                         |
| `D2SCP006` | Error    | `grantedTo` entry has empty role array - invalid config (omit the entry instead)                                                                               |
| `D2SCP007` | Error    | Two scopes collide at the same tree position (one is a strict dot-prefix of the other)                                                                         |
| `D2SCP008` | Error    | Non-anonymous scope that does not declare `internal` omits `grantedTo` (unreachable scope)                                                                     |
| `D2SCP009` | Error    | No `scopes.spec.json` found in `AdditionalFiles`                                                                                                               |
| `D2SCP010` | Error    | Declared `internal` flag disagrees with the `internal.` name prefix, in either direction                                                                       |
| `D2SCP011` | Error    | `D2ScopesExtensionNamespace` is not a dot-separated sequence of C# identifiers                                                                                 |
| `D2SCP012` | Error    | `D2ScopesExtensionNamespace` is set and `D2ScopesExtensionClassPrefix` is absent or not a C# identifier                                                        |
| `D2SCP013` | Warning  | `D2ScopesExtensionClassPrefix` is set and `D2ScopesExtensionNamespace` is not; nothing is emitted                                                              |

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "scopes": [
    {
      "name": "anon.public.health",
      "description": "Public health-check endpoint.",
      "impersonationBlocked": false
    },
    {
      "name": "orders.refund.approve",
      "description": "Approve a refund on an order.",
      "impersonationBlocked": true,
      "grantedTo": { "Support": ["Officer"], "Admin": ["Owner", "Officer"] }
    },
    {
      "name": "orders.payment.capture",
      "description": "Capture the payment for an order.",
      "impersonationBlocked": true,
      "grantedTo": { "Customer": ["Owner"] }
    }
  ]
}
```

### Field rules

- **`name`**: dot-separated, lowercase, >= 2 segments, each segment `^[a-z][a-z0-9]*$`. Anonymous scopes start with
  `anon.`.
- **`description`**: free-form. Renders as XML `<summary>` on the emitted constant.
- **`impersonationBlocked`**: `true` -> the token issuer strips this scope from impersonated tokens at mint time
  (defense in depth: a `RequiredScopes` check also rejects an impersonated token that lacks the scope). Meaningless on
  anon scopes (`D2SCP005`).
- **`grantedTo`**: per-(`OrgType`, `Role`) grant matrix. Keys: `OrgType` name (PascalCase) or `"*"`. Values: array of
  `Role` names (PascalCase) or `["*"]`. **Empty role arrays are forbidden** (`D2SCP006`) - for "no grant," omit the
  entry. Required for non-anon scopes (`D2SCP008`); omitted on `anon.*` (universal pre-auth grant by namespace
  convention) and on an entry declaring `internal` (a workload scope granted by the internal transaction-token mint at
  the application boundary, never by the org-role matrix - no user org-role can ever hold it, which is intended).
- **`internal`**: `true` -> the scope is workload-only. It exempts the entry from `grantedTo`, and the contract
  validator refuses any operation that carries a public HTTP route and requires it. Must agree with the `internal.` name
  prefix, in either direction (`D2SCP010`), because the runtime strips by the prefix while the catalog decides by the
  flag.
- **`deprecated`**: `true` -> the emitted C# constant carries `[Obsolete(...)]` and the emitted TypeScript leaf carries
  `@deprecated`. An entry is never deleted - deleting one is a wire break - so retiring a scope marks it where the
  consumer's compiler can see it instead of removing it.
- **`deprecatedReason`**: developer-facing English rendered into the `[Obsolete]` message and the `@deprecated` line. An
  entry marked deprecated without one gets a message that names the scope.
- **`replacedBy`**: the successor entry's scope name, appended to that message so the reader knows which scope replaces
  it. Omitted for a retirement with no successor, and not repeated where `deprecatedReason` already names it.
- **`sunset`**: an ISO-8601 retirement date in the form of the RFC 8594 `Sunset` response header, stored in the
  contract. No emitter reads it, and the generated catalog does not contain it.

### Wildcard expansion

`*` for org type expands against `DcsvIo.D2.Auth.Abstractions.OrgType` enum members; `*` for role expands against
`DcsvIo.D2.Auth.Abstractions.Role` members. Expansion happens at **codegen time**, not runtime - adding a new enum
member requires re-running the build to pick it up (the emitted `Scopes.g.cs` has an `// auto-generated` header
listing the enum members it expanded against).

---

## Emitted `Scopes.g.cs` shape

```csharp
public static partial class Scopes
{
    public static class Anon
    {
        public static class Public
        {
            public const string Health = "anon.public.health";
        }
        // ...
    }

    public static class Orders
    {
        public static class Refund
        {
            public const string Approve = "orders.refund.approve";
            public const string Force = "orders.refund.force";
        }
        // ...
    }

    public static bool IsImpersonationBlocked(string scope);
    public static bool IsAnonymous(string scope);
    public static bool IsKnown(string scope);
    public static bool IsGrantedTo(string scope, OrgType orgType, Role role);

    public static IReadOnlySet<string> AllScopes;
    public static IReadOnlySet<string> AllAnonymousScopes;
    public static IReadOnlySet<string> AllImpersonationBlockedScopes;
    public static IReadOnlyDictionary<(OrgType, Role), IReadOnlySet<string>> GrantedScopes;
}
```

All lookup helpers are O(1) - backed by `HashSet<string>` / `Dictionary<,>`. `GrantedScopes` is the canonical
`(OrgType, Role) -> scope-set` map the token issuer consumes at JWT mint time.

---

## Reference

- [`contracts/auth-scopes/schema.json`](../../../../contracts/auth-scopes/schema.json) - JSON Schema for the spec
- [`contracts/auth-scopes/scopes.spec.json`](../../../../contracts/auth-scopes/scopes.spec.json) - the source-of-truth
  scope catalog
- [`DcsvIo.D2.I18n.SourceGen`](../../i18n/source-gen/README.md) - sibling source generator whose pattern
  this one follows (same incremental-generator + diagnostic-split pattern)
- This generator classifies nothing. The spec, `ScopeEntry`, `ScopeSpecLoader` and `ScopesEmitter` have no
  `actionSensitivity` field, because how sensitive an action is belongs to the operation rather than to a
  scope: one scope can be granted to several operations, and a scope entry carrying a sensitivity would be a second
  declaration of something the operation already states. Sensitivity reaches the runtime as endpoint metadata, as
  [`contracts/auth-scopes/README.md`](../../../../contracts/auth-scopes/README.md) describes, and this package neither
  reads it nor emits it.

---

## Validation ledger

| Emitter           | Validated against                                                                                                                                                                                                                                     | Test double stands in for                                                                      | What retires the double              |
| ----------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- | ------------------------------------ |
| `ScopesEmitter`   | `ScopesEmitterTests` drives the real emitter over spec entries and asserts the emitted source; `ScopesGeneratorTests` runs the real `IIncrementalGenerator` through a `GeneratorDriver`; `ScopesGeneratedTests` asserts the compiled `Scopes` output. | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| `ScopeSpecLoader` | `ScopeSpecLoaderTests` parses spec JSON through the real loader, including the malformed and schema-violating shapes.                                                                                                                                 | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| Diagnostics       | `DiagnosticIdsTests` asserts each declared descriptor's id, severity and category.                                                                                                                                                                    | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
