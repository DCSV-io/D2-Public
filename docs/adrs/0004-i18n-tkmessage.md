<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# ADR-0004: i18n - `TKMessage` (translation-key-as-type) + source-generated `TK` constants

- **Status**: Accepted
- **Date**: 2026-05-30

## Context

D2 is a multi-locale SvelteKit + .NET system. Every handler and domain factory that can fail must communicate failure
reasons to users, and many operations (email notifications, validation messages, not-found errors) contain user-visible
text. The engineering problem: how does a domain layer produce a user-facing message without (a) fixing a locale into
it, (b) coupling to a translation runtime, or (c) allowing a string literal to silently bypass the translation catalog?

The decision rests on these constraints:

**The domain isolation constraint.** `D2Result.Messages`, `InputError.Errors`, and notification template references
are authored deep in domain and application layers. Those layers are, by convention, pure; they must not depend on
I/O, DI containers, or configuration loading. Any i18n design that puts the translation runtime in the same assembly
as the message primitive would force every domain project to transitively import `Microsoft.Extensions.Configuration`,
`Microsoft.Extensions.DependencyInjection`, and file I/O - dependencies the zero-dep convention forbids.

**The locale-rendering boundary.** HTTP responses must be CDN-cacheable. Server-side translation before the HTTP
response would require a `Vary: Accept-Language` header, fragmenting edge caches. The SvelteKit BFF and browser already
have the user's locale and Paraglide's per-key translation functions. The server stays locale-agnostic on the
request/response path; rendering happens at the client. The server-side `ITranslator` exists only for outbound
notifications (emails, SMS, push), where the recipient's preferred locale comes from the user profile and the
rendered string must be inlined before delivery.

**The string-key drift problem.** The conventional approach - domain code returns a plain `string` key such as
`"common_errors_NOT_FOUND"`, the runtime looks it up in a resource file - allows the key to go stale silently. A
rename in the catalog is not a compile error; a typo is not a compile error; a removed key is not a compile error. The
mismatch surfaces only at runtime as an untranslated literal or a fallback raw-key in the UI.

## Decision

Every user-facing translatable string in the codebase is typed as `TKMessage` - a sealed record containing a translation
key and optional parameter bindings (`packages/dotnet/i18n/abstractions/TKMessage.cs`). Its constructor is `internal`;
the only public way to obtain a `TKMessage` is through the source-generated `TK` static class (`TK.g.cs`, committed
under `packages/dotnet/i18n/keys/Generated/`). "Untranslated literal in `D2Result.Messages`" is structurally
unrepresentable: the type system enforces it.

The `TK` constants are emitted at build time by `DcsvIo.D2.I18n.SourceGen` - a Roslyn `IIncrementalGenerator`
(`packages/dotnet/i18n/source-gen/TKGenerator.cs`, `TKEmitter.cs`) that reads `contracts/messages/en-US.json` as an
`AdditionalFile`. Each flat JSON key (`common_errors_NOT_FOUND`) is decomposed into a three-segment path
(`TK.Common.Errors.NOT_FOUND`) and emitted as a `static readonly TKMessage` constant. The generator also cross-checks
every other locale catalog against `en-US` and reports per-locale coverage gaps and orphan keys as Roslyn diagnostics
at build time (`D2I18N*`).

The abstractions assembly (`DcsvIo.D2.I18n.Abstractions`) contains `TKMessage` and `ITranslator`, and has no non-BCL
dependencies - matching the relationship of `Microsoft.Extensions.Logging.Abstractions` to
`Microsoft.Extensions.Logging` (the general pattern is ADR-0006). The source-generated `TK` constants are in the keys
assembly (`DcsvIo.D2.I18n.Keys`). The runtime assembly (`DcsvIo.D2.I18n`) adds `Translator`, `SupportedLocales`, and
`AddD2I18n`; it is referenced only by composition roots and outbound-notification handlers, never by domain code.

HTTP responses send `TKMessage` objects unchanged: `{ "key": "common_errors_NOT_FOUND" }`, or - for a message carrying
substitution parameters - `{ "key": "...", "params": { "minLength": "12" } }`. The SvelteKit client translates them
via Paraglide in the user's active locale. The JSON property names (`key`, `params`) are themselves spec-derived:
`contracts/tk-message/tk-message.spec.json` drives `DcsvIo.D2.WireShapes.SourceGen`, which emits
`TkMessageWireShape.g.cs` containing `TkMessageWireShape.KEY` / `.PARAMS`. `TKMessageJsonConverter` references those
constants, not inline literals; cross-language wire drift on the property names is structurally impossible (this is an
instance of ADR-0002).

On the TypeScript side, `contracts/messages/en-US.json` drives a second generator that emits
`packages/typescript/i18n-keys/src/generated/tk-keys.g.ts` (sources committed). The TS `TK` object uses the same
nested path structure (`TK.common.errors.NOT_FOUND`), and each leaf value is a `TKMessage` instance built by `tk(...)`
(e.g. `{ key: "common_errors_NOT_FOUND" }`), as on the .NET side. Paraglide itself generates per-key typed functions for
UI components; the TS `TK` catalog and Paraglide are parallel codegen outputs from the same source, and they serve
different call sites. The functional invariant (drift impossible; a key rename = compile/build error on both sides)
holds on both sides.

## Consequences

**Positive.**

- A translation-key rename in `contracts/messages/en-US.json` immediately breaks the build on both the .NET side (the constant no longer exists after regeneration; all reference sites fail to compile) and the TS side. Stale keys are not silently swallowed.
- JSON<->constant drift is structurally impossible: the constant in `TK.g.cs` is emitted directly from the JSON key;
  it cannot exist without the JSON entry, and the entry cannot be renamed without updating every code reference.
- Domain code - handlers, factories, smart-constructor `Create` methods - returns `D2Result` with `TKMessage` values
  without importing any translation runtime. The zero-dep constraint is preserved.
- Per-locale translation coverage gaps appear as build-time Roslyn warnings rather than blank strings in production.
- The server never selects a locale on the HTTP path: responses are CDN-cacheable without `Vary: Accept-Language` fragmentation.
- Wire format is single-source: `tk-message.spec.json` drives both the .NET serializer property names and the TS parser; both reference generated constants, not inline literals.

**Negative / risks.**

- Adding a key requires a `contracts/messages/en-US.json` edit AND corresponding additions to every other locale catalog
  (or accepting a coverage warning until translations are available). Contributors must maintain all locale files.
- The internal constructor on `TKMessage` means deserialization must go through `TKMessageJsonConverter`; boundary
  code that receives an arbitrary JSON string cannot construct a `TKMessage` directly - it calls `ITranslator.HasKey`
  first. This adds a small amount of code at inbound wire boundaries.
- .NET codegen (Roslyn) and TS codegen are independent tools sharing one source spec; a toolchain-level defect could produce divergent outputs. Partially mitigated by the exhaustive round-trip test in `packages/typescript/i18n/tests/tk-keys.test.ts`, which verifies every decomposable key in `en-US.json` is reachable in the TS `TK` catalog at its expected path.

## Alternatives considered

**Raw localized strings in domain code.** Domain factories return `string` messages in the server's default locale. Rejected: permanently couples domain code to a locale, requires server-side locale selection on the HTTP path (Vary-header CDN fragmentation), and makes locale coverage a runtime concern rather than a build-time one.

**Stringly-typed resource-file lookup by string key.** Domain code returns a `string` key directly; a runtime resolver
looks it up - the conventional .NET `.resx` pattern and the path that takes the least effort. Rejected: a key rename or
typo does not fail the build; the mismatch surfaces only at runtime; JSON<->constant drift is detectable only by a test,
not the compiler. This approach does not remove the "stale key" bug class by construction.

**Server-side locale rendering before the HTTP response.** The server renders in the request locale and sends the
translated string. Rejected: breaks CDN cacheability (requires `Vary: Accept-Language`); moves locale selection to the
server, which must then trust and validate the request's locale header; contradicts the architecture where Paraglide
renders the locale in the BFF and browser. The server-side `Translator` is retained only for the outbound-notification
path where rendering before send is not optional.

**Single combined i18n assembly (no abstractions split).** `TKMessage` and `Translator` in one project. Rejected: forces every domain project to transitively import Configuration + DI + file I/O just to reference the message primitive. The split mirrors `Microsoft.Extensions.Logging.Abstractions` / `Microsoft.Extensions.Logging` exactly (see ADR-0006).

## References

- `packages/dotnet/i18n/abstractions/` - `TKMessage.cs` (internal ctor + immutable `With()` parameter binding),
  `ITranslator.cs`, `TKMessageJsonConverter.cs`, the zero-non-BCL-deps csproj, and the committed
  `Generated/.../TkMessageWireShape.g.cs`.
- `packages/dotnet/i18n/keys/` - the committed `Generated/.../TK.g.cs` (`DcsvIo.D2.I18n.Keys`).
- `packages/dotnet/i18n/source-gen/` - `TKGenerator.cs`, `TKEmitter.cs`; en-US as source of truth; per-locale coverage diagnostics.
- `packages/dotnet/i18n/core/` - `Translator.cs` (locale fallback + raw-key fallback; outbound-notification only) + `AddD2I18n` registration.
- `packages/dotnet/i18n/abstractions/README.md` - canonical i18n reference (zero-dep rationale + decomposition-rule table).
- `packages/typescript/i18n-keys/src/generated/tk-keys.g.ts`; `packages/typescript/i18n/tests/tk-keys.test.ts` - TS `TK`
  catalog + exhaustive round-trip test.
- `contracts/messages/en-US.json` - single source of truth for all keys; `contracts/tk-message/tk-message.spec.json` - wire-shape property-name spec.
- [ADR-0002](0002-spec-driven-codegen.md) - the spec-driven codegen pattern this applies.
  [ADR-0003](0003-d2result-errors-as-values.md) - `D2Result.Messages: TKMessage[]` is the consumer that depends on it.
  [ADR-0006](0006-abstractions-implementation-split.md) - the abstractions/runtime split applied here.
