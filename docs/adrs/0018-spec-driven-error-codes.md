<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# ADR-0018: Spec-driven cross-service error codes + a closed `ErrorCategory` semantic set

- **Status**: Accepted
- **Date**: 2026-06-09, 2026-08-28

## Context

Every handler in every D2 service fails in a finite, well-understood set of ways, and each failure must travel a long
path: a domain factory stamps an error code, a transport carries it (gRPC envelope / HTTP ProblemDetails), and a remote
consumer - possibly in the other runtime - has to interpret it. For that path to be sound, an error code is not just
a string: it is a contract that links an HTTP status, a semantic class, and a user-facing translation key, and
that binding must agree byte-for-byte across .NET and TypeScript.

With the links split across separately maintained sources, each of these failures occurs:

- **Codes are free text.** A handler can `Fail()` with any string. Nothing enforces that the string is declared
  anywhere, is unique, or is spelled the same on both runtimes.
- **The code<->status<->message links are hand-maintained and drift.** When the HTTP status and the default
  `TKMessage` for a code sit inside C# factory bodies, each factory hard-codes both.
- **Disconnected catalogs.** Separate stacks for generic, auth and DB codes, with near-identical but
  independently-maintained generators, drift apart.
- **No cross-service resolution.** Given a wire code, no component can determine "what status / category / localized
  message does this map to?" without importing the producing service's catalog - which a central boundary
  resolving *anybody's* code cannot do.
- **No semantic class on the wire.** A consumer wanting to "retry any infrastructure failure" has no way to classify
  a failure without a code-by-code lookup against the producer's catalog.

This decision is a concrete, large instance of the spec-driven codegen architecture (ADR-0002): error codes are one
more cross-language catalog, generated rather than hand-copied. It extends the `D2Result` model (ADR-0003) by making the
`(code, httpStatus, default message)` triple spec-sourced rather than factory-body-sourced, and it depends on the
`TKMessage` translation-key-as-type guarantee (ADR-0004) for the `userMessageKey` link.

## Decision

Every error code in the system is declared in a `*-error-codes.spec.json` file conforming to one canonical schema, and
the whole error layer - constants, factories, the cross-service registry, and the `ErrorCategory` enum - is generated
from those specs.

**One canonical schema per catalog entry.** `contracts/error-codes/error-codes.canonical.schema.json` (draft-07) defines
each entry as `code` (SCREAMING_SNAKE, unique, **owner-prefixed**) - `httpStatus` (closed enum) - `category` (closed
semantic enum) - `userMessageKey` (a `TK.*` symbol-path reference) - `factoryName` (the emitted factory method) -
`factoryShape` (a closed enum - `standard`, the one universal error-factory shape
`messages?, inputErrors?, errorCode?, category?, traceId?` (all optional), or `none` (constant + boolean only) - that
drives the generated factory's *signature* so regeneration is byte-identical) - `doc` (optional `deprecated`). The
generic catalog (`contracts/error-codes/error-codes.spec.json`) and every per-domain catalog
(`contracts/<domain>-error-codes/<domain>-error-codes.spec.json`) conform to this shape; per-domain `schema.json` files
copy it verbatim (narrowed) rather than `$ref` it, because a `$ref`+`allOf` overlay collides with the canonical's
`additionalProperties: false`.

**Codegen emits both runtimes from the same spec.** Per catalog the generator (one generalized .NET Roslyn engine in
`source-gen-shared/error-codes-emit/` shared by per-catalog generator shells; one TypeScript emitter) emits
`<Domain>ErrorCodes` constants, typed `<Domain>Failures` / `<Domain>Failures<T>` `D2Result` factories (delegating to the
`httpStatus`-selected base factory and stamping the domain `code` + `userMessageKey`), and per-code booleans.
Framework-universal codes stay on `D2Result` / `D2Result<T>` directly; domain codes live on the `<Domain>Failures`
classes - the call site identifies the scope.

**Code uniqueness is structural via owner prefixes.** Wire codes use a closed owner taxonomy: **`GEN_*`** (generic /
framework), **`LIB_<DOMAIN>_*`** (portable public libraries, e.g. `LIB_AUTH_*`), **`DOM_<DOMAIN>_*`** (application
product domains). Each catalog shell declares `DomainPrefix` + `Owner` + `DomainToken`; the generator requires every
code to start with its full DomainPrefix. Prefix-as-namespace makes collisions structurally hard; the merged-registry
build is the final check that hard-fails on any cross-catalog collision (`D2ERC004`) or reserved-namespace violation
(`D2ERC005`).

**Configuration section names carry the same owners.** Settings from two owners can collide exactly as error codes
can, so a configuration section name declares its owner with the same prefixes: `LIB_<PACKAGE>`, with an optional
`_<REST>`, for a section a shared library owns (`LIB_OUTBOX`, `LIB_DATA_GOVERNANCE`, `LIB_ASPNETCORE_FORGERY`), and
`DOM_<DOMAIN>_<REST>` for a section an application product domain owns. The `public const string SECTION` on the
options class is the single declaration of the name, and every operator surface - an environment template, a compose
file, a README - spells the section the way the constant does. Other key shapes are not section constants and
have neither prefix: a deployable service's own composition-root keys take the service name as their prefix, and a
backing-store endpoint shared across a deployment takes a bare name ending in `_URL`.

**A merged registry.** A second generator aggregates the GEN and public LIB catalogs into
`DcsvIo.D2.ErrorCodes.Registry` / `@dcsv-io/d2-error-codes-registry`, with an `ErrorCodeInfo` record (Code, HttpStatus,
Category, UserMessageKey, FactoryName, FactoryShape, Doc, Domain, **Owner**, **IsDeprecated**) plus a resolution API
(`TryResolve` / `Resolve` / `All` <-> `resolve` / `has` / `all`). The registry contains no `DOM_*` code.

Application-boundary hosts resolve foreign codes without importing the producer's catalog; services that handle
*specific* codes import only the catalogs they branch on; generic class-based handling uses the wire `category`
instead.

**`ErrorCategory` is a closed semantic set in a foundational zero-dep lib.** `category` is a closed enum -
`validation_failure`, `not_found`, `conflict`, `policy_denied`, `rate_limited`, `payload_too_large`,
`infrastructure_unavailable`, `internal_error`, `partial_success` - that classifies a failure for telemetry and for
generic class-based handling. It is **not** the factory selector (`httpStatus` selects the base factory). The enum, its
wire mapping (`ErrorCategoryWire.ToWire` / `TryFromWire`), and its `[JsonConverter]` are generated from a dedicated
shared spec `contracts/error-category/error-category.spec.json` into a foundational per-runtime leaf - .NET
`DcsvIo.D2.ErrorCodes.Category` and TS `@dcsv-io/d2-error-category` - that depends on the BCL / nothing. Because the
enum is generated in this leaf and not in the registry, `result-core` can reference `ErrorCategory` *downward* for a
typed `D2Result.Category`. The registry does not emit the enum; it references `DcsvIo.D2.ErrorCodes.Category`
(`packages/dotnet/error-codes/category/`). On the wire `category` is a snake-case string serialized via the
`[JsonConverter]`.

**Generator diagnostics hard-fail on malformed input.** The error-codes generator enforces the domain prefix and the
`userMessageKey`-resolves-to-a-real-TK check (inverse-transforming the symbol path to its snake key and asserting
membership in `contracts/messages/en-US.json`). The merged-registry generator reports build errors for cross-catalog
collision (`D2ERC004`), reserved-namespace violation (`D2ERC005`), malformed spec (`D2ERC006`), and a category not in
the closed set (`D2ERC007`). The category generator reports its own `D2ECAT*` family for a malformed spec, duplicate
wire string, invalid wire shape, or empty doc. All are `DiagnosticSeverity.Error`.

**Byte-parity gates and cross-runtime parity tests cover every generated file.** Every generated `.g.cs` / `.g.ts` carries a
committed byte-parity gate test that fails if the file is hand-edited or the generator drifts; cross-runtime fixture
parity tests prove .NET<->TS agreement on the constants, the factories' produced `category`, and the registry's resolved
metadata.

## Consequences

**Positive.**

- A wire error code is a self-describing, structurally-validated contract: declared once, unique by construction,
  emitted identically into both runtimes, and resolvable to status + category + localized message anywhere - including
  by a boundary that never imported the producing service.
- Codes are not free text. A code that is not in a spec cannot reach a `D2Result` factory, and a `userMessageKey`
  that does not resolve to a real translation key fails the build.
- The `(code, httpStatus, default message)` triple is single-sourced in the spec, not duplicated across factory bodies -
  the drift between duplicated copies of the triple cannot occur.
- Generic class-based handling ("retry any `infrastructure_unavailable`") works off the wire `category` without coupling the consumer to the producer's catalog.
- Adding a domain catalog is "drop a spec + register it" - no new generator, because the engine and the TS emitter are
  generalized and parameterized.
- The closed `ErrorCategory` set lives in a zero-dep leaf that `result-core` can reference downward, so
  `D2Result.Category` is typed (not a plain string), and the same enum is byte-aligned across runtimes from one shared
  spec.

**Negative / risks.**

- The error layer affects more code than any other part of the codebase: a change to the generic factories reaches every
  `D2Result` consumer.
- Each catalog requires setup: design the spec, ensure each `userMessageKey` exists in all locales, regenerate both
  runtimes, write factory + parity tests. This is worthwhile only for codes with cross-service consumers.
- The error-codes, registry and category generators must stay in lockstep; a category added to the closed set is a
  coordinated edit across the category spec, the canonical schema's `category` enum, and the locales behind any new
  `userMessageKey`.
- The registry is an aggregate over all specs; a malformed or colliding spec anywhere fails the registry build for
  everyone - intended (fail early and visibly), but it makes the registry a single point of failure for every catalog.
- The `factoryShape` enum is the one universal `standard` shape (every optional param) plus `none`; every error factory shares that signature so a new code is a spec-only edit. Introducing a genuinely new factory signature beyond `standard` would be a canonical-schema change plus a generator change, not a spec-only edit.

## Alternatives considered

**Per-service hand-written enums / constants.** Disconnected catalogs and free-text
codes. Rejected: it creates as many sources of truth as there are services x runtimes, with no structural uniqueness, no
cross-service resolution, and silent .NET<->TS drift. It is exactly the failure mode ADR-0002 exists to close, applied
to the error layer.

**Hand-written constants paired with a hand-maintained code->message map.** Keep typing the constants but maintain a
lookup table for status / message. Rejected: the table is a fourth source of truth that drifts from the constants and
the factory bodies; a code rename does not fail the build.

**A runtime registry built by reflection / config at startup.** Resolve codes by scanning assemblies or loading config
when the service boots. Rejected: defers every validation (uniqueness, category membership, TK existence) to runtime or
to the first integration test, adds a startup dependency, and turns a malformed catalog into a runtime exception instead
of a build error - compile-time enforcement is better for a framework that fails early and visibly.

**`ErrorCategory` as a hand-authored enum in `result-core`.** Avoid the relocation by declaring the category values
where `D2Result` needs them. Rejected: it splits the category source of truth (the spec carries each code's category
string; a hand-enum carries the members) so the registry and `result-core` can drift, and it breaks the "every closed
wire enum is spec-derived" convention. The zero-dep generated leaf keeps one source and stays byte-aligned across
runtimes.

**Owner prefixes on error codes only, with configuration sections named freely.** Applying the taxonomy to
only some configuration sections lets an operator template spell a section without the prefix its constant carries.
A section named in two spellings binds nothing from the misspelled one, and the service
starts on its defaults without saying so. One taxonomy for both namespaces, with the constant as the only spelling,
removes that class of mismatch.

## References

- `contracts/error-codes/error-codes.canonical.schema.json` - the canonical entry schema (draft-07).
- `contracts/error-codes/error-codes.spec.json` - generic `GEN_*` catalog; `contracts/auth-error-codes/auth-error-codes.spec.json` - public `LIB_AUTH_*` catalog precedent.
- `contracts/error-category/error-category.spec.json` - the category wire strings (the shared cross-runtime source).
- `packages/dotnet/source-gen-shared/error-codes-emit/` - the generalized .NET emit engine (constants + factories); TypeScript packages commit matching catalogs.
- `packages/dotnet/error-codes/registry/` + `registry-source-gen/` (`RegistryDiagnosticDescriptors.cs` -
  `D2ERC004`-`D2ERC007`) and `packages/typescript/error-codes-registry/` - the merged cross-service registry, both
  runtimes.
- `packages/dotnet/error-codes/category/` + `category-source-gen/` (`DiagnosticDescriptors.cs` - `D2ECAT*`) and
  `packages/typescript/error-category/` - the foundational `ErrorCategory` leaf, both runtimes.
- `LIB_*` section constants: `packages/dotnet/outbox/core/OutboxOptions.cs`,
  `packages/dotnet/data-governance/entity-framework-core/AnonymizationEngineOptions.cs` and
  `packages/dotnet/aspnetcore/Forgery/D2ForgeryProtectionOptions.cs`.
- Cross-runtime parity fixtures (constants, factory-produced category, registry metadata); byte-parity gate tests under `packages/dotnet/tests/Unit/SpecsConsistency/`.
- [ADR-0002](0002-spec-driven-codegen.md) - the spec-driven codegen architecture this is a concrete instance of.
  [ADR-0003](0003-d2result-errors-as-values.md) - `D2Result`, whose `(code, httpStatus, message)` triple becomes
  spec-sourced. [ADR-0004](0004-i18n-tkmessage.md) - the `TKMessage` translation-key-as-type guarantee behind
  `userMessageKey`.
