<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Unified error-codes generation engine

> Parent: [`source-gen-shared`](../README.md)

The single, parameterized generation engine behind every `*-error-codes` Roslyn catalog. One engine drives the generic
cross-cutting catalog (`ErrorCodes`) and every per-domain catalog (for example `AuthErrorCodes` and `AuthFailures`).
Each consuming assembly includes a `[Generator]` shell that calls `ErrorCodesEngine.Run` with its own `CatalogConfig`,
so no per-catalog generation logic is duplicated.

## Design rationale: shared engine and one shell per catalog

The generated path on disk is `Generated/{analyzerAssemblyName}/{generatorTypeFQN}/{sourceFileName}`. The catalogs
target different consuming assemblies and keep distinct analyzer identities (assembly name plus the fully qualified name
of the `ErrorCodesGenerator` type), so each catalog's generated tree is written to its own committed path. One merged
analyzer would move every tree to a new path. The logic is therefore shared in this engine and each shell keeps its own
identity, the same way `source-gen-shared/core/` is shared across every generator without merging their analyzer
identities.

## Pieces

| File                      | Role                                                                                                                                                                                                                    |
| ------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CatalogConfig.cs`        | Per-catalog configuration: class names, namespace, banner, summary and doc blocks, domain prefix, emit flags and per-catalog diagnostic ids. Every string that affects the emitted bytes comes from this configuration. |
| `ErrorCodeEntry.cs`       | The superset entry record: the always-present fields `code`, `httpStatus` and `doc`, and the nullable factory fields `category`, `userMessageKey`, `factoryName` and `factoryShape`.                                    |
| `ErrorCodesSpec.cs`       | The parsed spec, an array of entries.                                                                                                                                                                                   |
| `ErrorCodeSpecLoader.cs`  | JSON-shape loader. It reads the factory fields as optional, so a spec without them still loads.                                                                                                                         |
| `ConstantsEmitter.cs`     | Emits the `<Domain>ErrorCodes` constants class (consts, `AllCodes`, `GetHttpStatus` and the optional `KebabCase`) and runs the validations of every catalog.                                                            |
| `FactoryHost.cs`          | The `base` / `domain` axis. It selects between constructing onto `D2Result` and delegating to the base factories.                                                                                                       |
| `BaseFactoriesEmitter.cs` | Emits the generic catalog's constructing factories onto the `D2Result` / `D2Result<TData>` partials, plus the per-code booleans (`FactoryHost.Base`).                                                                   |
| `FailuresEmitter.cs`      | Emits a per-domain catalog's delegating `<Domain>Failures` (-> `D2Result`) and `<Domain>Failures<T>` (-> `D2Result<T>`) classes (`FactoryHost.Domain`).                                                                 |
| `ErrorCodesEngine.cs`     | Builds the incremental pipeline, gates on the consuming assembly, runs the loader and the emitters, and runs the engine diagnostics.                                                                                    |
| `MessageKeySet.cs`        | Value-equatable key set of `en-US.json`, so that a change to a translation value does not re-run code generation.                                                                                                       |
| `TkKeyTransform.cs`       | Inverse of `KeyDecomposer`: `TK.Auth.Errors.UNAUTHORIZED` -> `auth_errors_UNAUTHORIZED`, for the cross-check that each translation key exists.                                                                          |
| `EngineDiagnostic*.cs`    | The catalog-neutral engine diagnostics (`D2ERC*`).                                                                                                                                                                      |

## FactoryHost emission modes

`CatalogConfig.FactoryHost` selects how a catalog's failure factories are emitted. It is independent of the per-entry
`factoryShape`, which selects the call signature:

- `base` (the generic cross-cutting catalog): the factories are the base. They construct a `D2Result` /
  `D2Result<TData>` directly and are emitted as members of the `D2Result` / `D2Result<TData>` partial classes
  (`D2Result.Factories.g.cs` and `D2Result.Generic.Factories.g.cs`), together with the per-code boolean discriminators
  (`D2Result.Booleans.g.cs`). No `<Domain>Failures` class is emitted, because the host is `D2Result`. Every error
  factory has the one universal `standard` shape `(messages?, inputErrors?, errorCode?, category?, traceId?)`, with
  every parameter optional; `none` emits only the constant and the boolean. The hand-written `Ok` / `Created` / `Fail` /
  `Bubble*` / `SomeFound` / `PartialSuccess` factories and the composite and status booleans (`IsOk` / `IsCreated` /
  `IsPartialOrMissing` / `IsTransientRetryable`) stay on the same partials.
- `domain` (per-domain catalogs, for example auth): the factories delegate to the base factory that the `httpStatus`
  selects (`401 -> Unauthorized`, `409 -> Conflict`, `400 -> ValidationFailed`, `500 -> UnhandledException`,
  `503 -> ServiceUnavailable`, ...) and stamp the domain `code` and `userMessageKey`. The mode emits a non-generic
  `<Domain>Failures` class (-> `D2Result`, in `<Domain>Failures.g.cs`) and a generic `<Domain>Failures<T>` class (->
  `D2Result<T>`, in the sibling `<Domain>Failures.Generic.g.cs`), and both have the same method names. The two are
  distinct types because their arity differs, as `D2Result` and `D2Result<TData>` are. The delegating path emits the
  universal `standard` shape for every entry of every domain catalog and skips `none`; the non-generic class also has
  a legacy typed `<T>` overload on `503` entries. The call signature is the `standard` shape: an optional
  `IReadOnlyList<TKMessage>? messages = null` parameter. When it is omitted, the factory uses the spec's
  `userMessageKey`; when it is supplied, it replaces that key, so a caller can bind the offending argument with
  `TKMessage.With(...)` (for example to name a null lifecycle argument). The TypeScript generator
  mirrors this with an `{ messages?, traceId? }` opts object.

## Engine diagnostics

These diagnostics are catalog-neutral and fire for any catalog.

| ID         | Severity | Fires when                                                                                                                                                                                                  | Exempt                                                                                                 |
| ---------- | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| `D2ERC001` | Error    | A code does not start with the catalog's enforced full `DomainPrefix`, which includes the owner (for example a non-`LIB_AUTH_` code in the auth gate catalog, or a non-`GEN_` code in the generic catalog). | None. Every catalog shell supplies a non-empty `DomainPrefix` (`GEN_`, `LIB_AUTH_`, `DOM_AUTH_`, ...). |
| `D2ERC002` | Error    | A `userMessageKey` does not inverse-resolve to a key in `contracts/messages/en-US.json`.                                                                                                                    | Catalogs whose entries have no `userMessageKey`.                                                       |
| `D2ERC003` | Error    | A `factoryShape` value other than the universal `standard` shape or `none` appears on the delegating per-domain path (a hand-malformed spec). The schema constrains `factoryShape` to `standard` / `none`.  | `standard` and `none` delegating entries. A conforming spec never fires this diagnostic.               |

Each catalog's existing per-catalog validation diagnostics (`D2EC*` generic, `D2AEC*` auth) stay in their shells and are
mapped to descriptors there. Only the `D2ERC*` [engine diagnostics](#engine-diagnostics) are catalog-neutral.

The merged-registry generator (`DcsvIo.D2.ErrorCodes.Registry.SourceGen`) adds diagnostics that fire when it aggregates
the catalogs, because only it sees every catalog at once:

| ID         | Severity | Fires when                                                                                                                                                                                                                                                                                                                                                                                                             |
| ---------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `D2ERC004` | Error    | Two catalogs declare the same `code` (a cross-catalog collision). It fires at registry build time, and no registry is emitted until the collision is resolved.                                                                                                                                                                                                                                                         |
| `D2ERC005` | Error    | A code violates the owner-prefix or reserved-namespace grammar across the full aggregate: a code lacks its catalog's full `DomainPrefix`, or a code with a foreign prefix is in the wrong catalog (for example `LIB_AUTH_*` in the generic `GEN_*` catalog, or a bare or legacy shape with no owner). Each shell's `D2ERC001` checks its own catalog; `D2ERC005` checks the same grammar across all catalogs together. |

## Adding a catalog

1. Add `contracts/<domain>-error-codes/<domain>-error-codes.spec.json` and a `schema.json`. The schema is a copy of
   `contracts/error-codes/error-codes.canonical.schema.json` specialized to the domain, with narrower constraints on
   the `code` prefix, `httpStatus` and `category`.
2. Add an `<AdditionalFiles>` line and an analyzer `<ProjectReference>` to the consuming csproj. A catalog that has
   factories also adds `<AdditionalFiles Include="$(D2PublicContractsRoot)messages\en-US.json" />` for the `D2ERC002`
   cross-check.
3. If the catalog targets a new assembly, add a `[Generator]` shell (with its own analyzer csproj) that supplies that
   catalog's `CatalogConfig`, and reference the shared engine with `<Compile Include="$(D2ErrorCodesEmitRoot)**\*.cs">`.
   The shell needs no new emitter logic.
