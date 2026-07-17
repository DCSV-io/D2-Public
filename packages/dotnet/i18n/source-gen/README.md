<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.I18n.SourceGen

> Parent: [`packages/dotnet/`](../../README.md)

**Input contract:** [`contracts/messages/`](../../../../contracts/messages/README.md)

Roslyn incremental source generator that emits translation-key catalogs from `contracts/messages/*.json` via `<AdditionalFiles>`. The en-US.json file is the source-of-truth for the catalog; other locales contribute coverage diagnostics (`D2I18N002` per missing key, `D2I18N004` per orphan key). The generator is the original SrcGen pattern in this codebase — sibling generators (`auth/scopes-source-gen`, `auth/audiences-source-gen`, `context/source-gen`, `messaging/source-gen`) all mirror its file layout + diagnostic-decoupling design.

**Dual-target** (assembly-name gate):

| Consuming assembly | Emitted type | Values |
| --- | --- | --- |
| `DcsvIo.D2.I18n.Keys` | `TK` under `DcsvIo.D2.I18n` | public messages only |
| Host extension assembly (optional) | `ProductTK` under the host root namespace | public catalog ∪ host-supplied messages |

Any other assembly → no emit. `TKMessage` remains the shared public primitive (`InternalsVisibleTo` on `DcsvIo.D2.I18n.Abstractions` grants the Extensions host). Hosts that need product-only keys register an extension assembly that includes both public and host message catalogs.

The catalog is the single source of truth for translation keys. Every `TK.Common.Errors.NOT_FOUND` reference compiles against an emitted constant — adding a key is a one-line edit to `en-US.json` (the SrcGen picks it up next build); renaming a key breaks every consumer at compile time.

**Convention**: spec-driven Roslyn `IIncrementalGenerator` pattern (file layout, diagnostic ID convention, generator anatomy, `<AdditionalFiles>` wiring).

---

## Build-time diagnostics

| ID          | Severity | Trigger                                                                                                             |
| ----------- | -------- | ------------------------------------------------------------------------------------------------------------------- |
| `D2I18N001` | Error    | Translation key violates the segment / identifier / reserved-word rules (see Key decomposition)                     |
| `D2I18N002` | Warning  | Key present in en-US is missing from another locale file (per-locale coverage gap)                                  |
| `D2I18N003` | Error    | Two source keys decompose to the same TK constant path (e.g. `common_errors_NOT_FOUND` + `Common_Errors_NOT_FOUND`) |
| `D2I18N004` | Warning  | Key present in another locale is orphaned (no en-US equivalent)                                                     |
| `D2I18N005` | Error    | No `en-US.json` found in `AdditionalFiles` for a target assembly                                                    |
| `D2I18N006` | Error    | A locale file failed to parse (malformed JSON)                                                                      |

---

## Key decomposition

Translation keys follow the `<domain>_<category>_<NAME>` shape. The `KeyDecomposer` enforces:

- ≥ 2 underscore-separated segments (a single `flat` key is rejected).
- Each segment is a valid C# identifier (`^[A-Za-z_][A-Za-z0-9_]*$`).
- No leading / trailing / consecutive underscores in a segment.
- Decomposed `Domain` and `Category` are PascalCase'd from the segment text. `Identifier` is the raw segment (preserves original case — `NOT_FOUND` stays `NOT_FOUND`, `ip_required` stays `ip_required`).
- Decomposed identifiers don't collide with C# reserved words (the static `sr_csharpReservedWords` set covers contextual + new-since-Roslyn lowercase tokens).

A decomposition failure produces `D2I18N001` and the offending key is dropped from the emitted catalog.

---

## Spec format (translation catalog)

```json
{
  "common_errors_NOT_FOUND": "The requested resource was not found.",
  "common_errors_UNAUTHORIZED": "You are not authorized.",
  "auth_errors_INVALID_ROLE": "The role '{role}' is not valid for org '{org}'."
}
```

- **Keys**: snake*case, `<domain>*<category>\_<NAME>` shape.
- **Values**: free-form translation strings. Embedded `{name}` placeholders correspond to `TKMessage.With(name, value)` parameter substitution at render time.
- **One JSON file per locale** in `contracts/messages/` (e.g. `en-US.json`, `fr-FR.json`).

---

## Emitted `TK.g.cs` shape

```csharp
public static partial class TK
{
    public static class Common
    {
        public static class Errors
        {
            public static readonly TKMessage NOT_FOUND = new("common_errors_NOT_FOUND");
            public static readonly TKMessage UNAUTHORIZED = new("common_errors_UNAUTHORIZED");
        }
    }

    public static class Auth
    {
        public static class Errors
        {
            public static readonly TKMessage INVALID_ROLE = new("auth_errors_INVALID_ROLE");
        }
    }
    // ... etc.
}
```

Each constant is a `static readonly TKMessage` carrying just the key — parameter substitution happens lazily at render time via `TKMessage.With(...)`. The `internal`-ctor design of `TKMessage` (per `DcsvIo.D2.I18n.Abstractions`) means raw-string `D2Result.messages = ["untranslated literal"]` is structurally unrepresentable; every consumer is forced through `TK.*`.

---

## Cross-platform parity

The Node side typically uses [Paraglide](https://inlang.com/m/gerre34r) (or an equivalent host i18n compiler) for translation key compilation. This is intentional platform exclusivity — the .NET SrcGen and the Node compiler both consume the same `contracts/messages/*.json` catalogs but produce platform-native artifacts. No `@dcsv-io/d2-i18n-source-gen` Node mirror exists.

---

## Reference

- [`contracts/messages/en-US.json`](../../../../contracts/messages/en-US.json) — source-of-truth catalog
- [`DcsvIo.D2.I18n.Abstractions`](../abstractions/README.md) — emission target (defines `TKMessage`)
- [`DcsvIo.D2.Auth.Scopes.SourceGen`](../../auth/scopes-source-gen/README.md) — sibling SrcGen modeled on this one
