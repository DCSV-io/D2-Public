<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Auth.Audiences.SourceGen

> Parent: [`packages/dotnet/`](../../README.md)

**Input contract:** [`contracts/auth-audiences/`](../../../../contracts/auth-audiences/README.md)

Roslyn incremental source generator that emits audience catalogs from `contracts/auth-audiences/audiences.spec.json` via
`<AdditionalFiles>`.

Assembly-name gate: the consuming assembly `DcsvIo.D2.Auth.Abstractions` receives `Audiences` under the
`DcsvIo.D2.Auth.Abstractions` namespace, built from its own `audiences.spec.json` AdditionalFiles entry. Another
assembly receives an audience class only when its project sets the MSBuild properties `D2AudiencesExtensionNamespace`
and `D2AudiencesExtensionClassPrefix` and declares each as a `CompilerVisibleProperty` item.
`D2AudiencesExtensionNamespace` sets the namespace, and `D2AudiencesExtensionClassPrefix` sets the identifier placed
before `Audiences` in the class name. That class covers the union of every `audiences.spec.json` the assembly lists in
AdditionalFiles and is written to the class name followed by `.g.cs`. The public assembly ignores these properties, and
an assembly that sets neither receives no audience class.

The spec file is the single source of truth for the platform's JWT-audience catalog. Every value an inbound JWT's `aud`
claim can carry - including the broad internal audience every internal service accepts under the forward-unchanged
model - and every token-exchange `targetAudience` value is defined in one JSON file, so no hand-written parallel
constants exist that could drift per feature.

Convention: the generator follows the spec-driven Roslyn `IIncrementalGenerator` pattern (file layout, diagnostic ID
convention, generator anatomy, `<AdditionalFiles>` wiring).

---

## Build-time diagnostics

| ID         | Severity | Trigger                                                                                                                                         |
| ---------- | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| `D2AUD001` | Error    | Spec file is malformed JSON or violates the schema (missing required `audiences` array, missing required per-entry `name` / `url`, wrong types) |
| `D2AUD002` | Error    | Audience name violates the C# identifier convention (must match `^[A-Z][A-Za-z0-9]*$`)                                                          |
| `D2AUD003` | Error    | Two audience entries share the exact same name (would produce a duplicate `const string`)                                                       |
| `D2AUD004` | Error    | Two audience entries share the exact same URL (would silently alias one to the other at JWT `aud` validation time)                              |
| `D2AUD005` | Error    | Audience URL is not a parseable absolute URI (would never match a real JWT `aud` claim value)                                                   |
| `D2AUD006` | Error    | No `audiences.spec.json` found in `AdditionalFiles`                                                                                             |
| `D2AUD007` | Error    | `D2AudiencesExtensionNamespace` is not a dot-separated sequence of C# identifiers                                                               |
| `D2AUD008` | Error    | `D2AudiencesExtensionNamespace` is set and `D2AudiencesExtensionClassPrefix` is absent or invalid                                               |
| `D2AUD009` | Warning  | `D2AudiencesExtensionClassPrefix` is set and `D2AudiencesExtensionNamespace` is not                                                             |

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "audiences": [
    {
      "name": "ExampleAudience",
      "url": "https://example.internal",
      "description": "Example service audience."
    }
  ]
}
```

| Field         | Required | Description                                                                  |
| ------------- | -------- | ---------------------------------------------------------------------------- |
| `name`        | Yes      | PascalCase identifier emitted as the `const` name. `^[A-Z][A-Za-z0-9]*$`.    |
| `url`         | Yes      | Absolute URI used as the `aud` claim value. Must parse as an absolute `Uri`. |
| `description` | No       | Free-form description; emitted as XML doc on the constant.                   |

---

## Emitted shape

For each audience the generator emits:

```csharp
public static partial class Audiences
{
    /// <summary>Example service audience.</summary>
    public const string ExampleAudience = "https://example.internal";

    // ... other audiences ...

    public static bool IsKnown(string audience);
    public static string? Resolve(string name);
    public static string? ResolveByUrl(string url);
    public static IReadOnlySet<string> AllUrls { get; }
    public static IReadOnlyDictionary<string, string> ByName { get; }
}
```

`IsKnown(audience)` is the canonical inbound-validation helper - JWT validation calls it on the `aud` claim at every
hop. `Resolve(name)` resolves a name like `"ExampleAudience"` to its URL - used to address a token-exchange
`targetAudience`. `ResolveByUrl(url)` is the inverse for telemetry / logging that wants a
friendly name.

---

## Sourcegen-specific notes

The inbound JWT validator checks the `aud` claim against these strings at every hop, and token exchanges use them as
`targetAudience`. Because the catalog is spec-driven,
duplicate names, duplicate URLs, malformed URLs, and bad identifiers fail at compile time rather than at the first JWT
validation in production.

---

## References

- [`contracts/auth-audiences/schema.json`](../../../../contracts/auth-audiences/schema.json) - JSON Schema (editor-time
  gate)
- [`contracts/auth-audiences/audiences.spec.json`](../../../../contracts/auth-audiences/audiences.spec.json) - the
  catalog
- [`DcsvIo.D2.Auth.Abstractions`](../abstractions/README.md) - the consuming lib (`Audiences.g.cs` is emitted into it)

---

## Validation ledger

| Emitter              | Validated against                                                                                                                                                                                                                                                 | Test double stands in for                                                                      | What retires the double              |
| -------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- | ------------------------------------ |
| `AudiencesEmitter`   | `AudiencesEmitterTests` drives the real emitter over spec entries and asserts the emitted source; `AudiencesGeneratorTests` runs the real `IIncrementalGenerator` through a `GeneratorDriver`; `AudiencesGeneratedTests` asserts the compiled `Audiences` output. | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| `AudienceSpecLoader` | `AudienceSpecLoaderTests` parses spec JSON through the real loader, including the malformed and schema-violating shapes.                                                                                                                                          | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| Diagnostics          | `AudiencesDiagnosticIdsTests` asserts each declared descriptor's id, severity and category.                                                                                                                                                                       | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
