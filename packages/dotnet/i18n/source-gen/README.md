<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.I18n.SourceGen

> Parent: [`packages/dotnet/`](../../README.md)

**Input contract:** [`contracts/messages/`](../../../../contracts/messages/README.md)

Roslyn incremental source generator that emits translation-key catalogs from `contracts/messages/*.json` via
`<AdditionalFiles>`. The en-US.json file is the source-of-truth for the catalog; other locales produce coverage
diagnostics (`D2I18N002` per missing key, `D2I18N004` per orphan key). The sibling generators
(`auth/scopes-source-gen`, `auth/audiences-source-gen`, `context/source-gen`, `messaging/source-gen`) all follow its
file layout + diagnostic-decoupling design.

**Assembly-name gate:** the consuming assembly `DcsvIo.D2.I18n.Keys` receives `TK` under the `DcsvIo.D2.I18n`
namespace, built from its own message catalogs. Another assembly receives a key class only when its project sets the
MSBuild properties `D2TkExtensionNamespace` and `D2TkExtensionClassPrefix` and declares each as a
`CompilerVisibleProperty` item. `D2TkExtensionNamespace` sets the namespace, and `D2TkExtensionClassPrefix` sets the
identifier placed before `TK` in the class name. That class covers the union of every catalog the assembly lists in
AdditionalFiles and is written to the class name followed by `.g.cs`. It calls the internal `TKMessage` constructor, so
that assembly also needs an `InternalsVisibleTo` grant from `DcsvIo.D2.I18n.Abstractions`. The public assembly ignores
these properties, and an assembly that sets neither receives no key class. Each emitted constant is a `TKMessage`, the
primitive `DcsvIo.D2.I18n.Abstractions` declares.

The catalog is the single source of truth for translation keys. Every `TK.Common.Errors.NOT_FOUND` reference compiles
against an emitted constant - adding a key is a one-line edit to `en-US.json` (the source generator emits it in the
next build); renaming a key breaks every consumer at compile time.

**Convention**: spec-driven Roslyn `IIncrementalGenerator` pattern (file layout, diagnostic ID convention, generator
anatomy, `<AdditionalFiles>` wiring).

---

## Build-time diagnostics

| ID          | Severity | Trigger                                                                                                             |
| ----------- | -------- | ------------------------------------------------------------------------------------------------------------------- |
| `D2I18N001` | Warning  | Translation key violates the segment / identifier rules (see Key decomposition)                                     |
| `D2I18N002` | Warning  | Key present in en-US is missing from another locale file (per-locale coverage gap)                                  |
| `D2I18N003` | Error    | Two source keys decompose to the same TK constant path (e.g. `common_errors_NOT_FOUND` + `Common_Errors_NOT_FOUND`) |
| `D2I18N004` | Warning  | Key present in another locale has no en-US equivalent (orphan key)                                                  |
| `D2I18N005` | Error    | No `en-US.json` found in `AdditionalFiles` for a target assembly                                                    |
| `D2I18N006` | Error    | A locale file failed to parse (malformed JSON)                                                                      |
| `D2I18N007` | Error    | `D2TkExtensionNamespace` is not a dot-separated sequence of C# identifiers                                          |
| `D2I18N008` | Error    | `D2TkExtensionNamespace` is set and `D2TkExtensionClassPrefix` is absent or invalid                                 |
| `D2I18N009` | Warning  | `D2TkExtensionClassPrefix` is set and `D2TkExtensionNamespace` is not                                               |

---

## Key decomposition

Translation keys follow the `<domain>_<category>_<NAME>` shape. The `KeyDecomposer` enforces:

- >= 3 underscore-separated segments (a key such as `common_errors` is rejected).
- Each segment is a valid C# identifier (`^[A-Za-z_][A-Za-z0-9_]*$`).
- No leading / trailing / consecutive underscores in a segment.
- Decomposed `Domain` and `Category` are PascalCase'd from the segment text. The constant name is every segment after
  the category, joined by `_` and upper-cased (`NOT_FOUND` stays `NOT_FOUND`, `ip_required` becomes `IP_REQUIRED`).
  C# keywords are lower case, so an upper-cased constant name is never one and `CLASS` is a valid name.

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
- **Values**: free-form translation strings. Embedded `{name}` placeholders correspond to `TKMessage.With(name, value)`
  parameter substitution at render time.
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

Each constant is a `static readonly TKMessage` containing only the key - parameter substitution happens lazily at
render time via `TKMessage.With(...)`. The `internal`-ctor design of `TKMessage` (per `DcsvIo.D2.I18n.Abstractions`)
means raw-string `D2Result.messages = ["untranslated literal"]` is structurally unrepresentable; every consumer must
go through `TK.*`.

---

## Cross-platform parity

The Node side typically uses [Paraglide](https://inlang.com/m/gerre34r) (or an equivalent host i18n compiler) for
translation key compilation. This is deliberate - the .NET source generator and the Node compiler both
read the same `contracts/messages/*.json` catalogs and each produces artifacts native to its platform. No
`@dcsv-io/d2-i18n-source-gen` Node mirror exists.

---

## Reference

- [`contracts/messages/en-US.json`](../../../../contracts/messages/en-US.json) - source-of-truth catalog
- [`DcsvIo.D2.I18n.Keys`](../keys/README.md) - emission target
- [`DcsvIo.D2.I18n.Abstractions`](../abstractions/README.md) - defines `TKMessage`
- [`DcsvIo.D2.Auth.Scopes.SourceGen`](../../auth/scopes-source-gen/README.md) - sibling source generator
  with the same layout

---

## Validation ledger

| Emitter         | Validated against                                                                                                                                                                                                                   | Test double stands in for                                                                      | What retires the double              |
| --------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- | ------------------------------------ |
| `TKEmitter`     | `TKEmitterTests` runs the real emitter over spec entries and asserts the emitted source; `TKGeneratorTests` runs the real `IIncrementalGenerator` through a `GeneratorDriver`; `TKGeneratedTests` asserts the compiled `TK` output. | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| `KeyDecomposer` | `KeyDecomposerTests` runs the real decomposer over the key shapes the spec admits and the ones it rejects.                                                                                                                          | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| Diagnostics     | `DiagnosticIdsTests` asserts each declared descriptor's id, severity and category.                                                                                                                                                  | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
