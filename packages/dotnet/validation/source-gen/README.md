<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Validation.SourceGen

> Parent: [`packages/dotnet/`](../../README.md)
>
> **Audience**: engineers who maintain the shared field-constraints catalog (field-length bounds and closed-list
> taxonomy enums). The domain value objects, the TypeScript Zod schemas that a host's gateway validates with, and any
> backend module consume the catalog.

**Input contract:** [`contracts/validation/`](../../../../contracts/validation/README.md)

A Roslyn incremental source generator that emits `FieldConstraints.g.cs` (field-length and digit-count `const int`
bounds) and `Taxonomy.g.cs` (the `NamePrefix` / `NameSuffix` / `BiologicalSex` closed-list enums) into
`DcsvIo.D2.Validation.Abstractions`. It reads `contracts/validation/field-constraints.spec.json` through
`<AdditionalFiles>`. It is single-target: it emits only when the consuming assembly is
`DcsvIo.D2.Validation.Abstractions`.

The spec file is the single source of truth for the platform's shared field bounds and the name and sex taxonomy. The
bounds apply to every value-object `Create(...)` call (contacts and Location). The enums are the closed wire
vocabularies for name prefixes, name suffixes and biological sex. The same spec drives the TypeScript
`@dcsv-io/d2-validation-abstractions` catalog (generated sources committed), so the .NET and TypeScript wire formats
cannot drift apart.

**Convention**: spec-driven Roslyn `IIncrementalGenerator` pattern, covering file layout, diagnostic ID convention,
generator anatomy and `<AdditionalFiles>` wiring.

---

## Single-target dispatch

The single `FieldConstraintsGenerator` gates on `compilation.AssemblyName`:

- `DcsvIo.D2.Validation.Abstractions` -> emit `FieldConstraints.g.cs` + `Taxonomy.g.cs`.
- Anything else -> emit nothing.

The generator uses the single-target dispatch (one consuming assembly) of the `DcsvIo.D2.Result.ErrorCodes.SourceGen`
generator. It emits `FieldConstraints.g.cs` and `Taxonomy.g.cs` through one `AddSource` call each, as the geo generator
does for its multi-type emission.

---

## Build-time diagnostics

| ID        | Severity | Trigger                                                              |
| --------- | -------- | -------------------------------------------------------------------- |
| `D2FC001` | Error    | Spec file is malformed JSON or violates the schema                   |
| `D2FC002` | Error    | Two field-length entries share the same `name`                       |
| `D2FC003` | Error    | A field-length `name` is empty or does not match `^[A-Z][A-Z0-9_]*$` |
| `D2FC004` | Error    | A field-length `value` is not a positive integer                     |
| `D2FC005` | Error    | Two taxonomy enums share the same `name`                             |
| `D2FC006` | Error    | A taxonomy enum `name` is empty or not a valid PascalCase identifier |
| `D2FC007` | Error    | A taxonomy enum declares an empty `members` list                     |
| `D2FC008` | Error    | Two members of the same enum share the same `name`                   |
| `D2FC009` | Error    | A taxonomy enum member `name` is empty or not a valid C# identifier  |

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "constraints": [
    { "name": "EMAIL_MAX", "value": 254, "doc": "Maximum length of an email address." }
  ],
  "enums": [
    {
      "name": "BiologicalSex",
      "backing": "byte",
      "doc": "Closed list of biological-sex classifications.",
      "members": [{ "name": "Unspecified", "doc": "Not specified / unknown." }]
    }
  ]
}
```

### Field rules

- `constraints[].name` - wire format `^[A-Z][A-Z0-9_]*$`. Unique. It is the name of the emitted `public const int`.
- `constraints[].value` - positive integer (`> 0`).
- `enums[].name` - PascalCase `^[A-Z][A-Za-z0-9]*$`. Unique. It is the name of the emitted `enum` type
  (`enum X : byte`).
- `enums[].backing` - only `byte` is supported, because closed lists are small.
- `enums[].members[].name` - identifier-safe (`^[A-Za-z_][A-Za-z0-9_]*$`). Unique within the enum. The member name is
  the wire form, written as a string through `JsonStringEnumConverter`.
- `doc` (on every node) - XML `<summary>` text on the emitted .NET symbol and JSDoc on the symbol that the TypeScript
  side emits.

---

## Emitted output

The generator emits these `.g.cs` files into the consuming assembly (`DcsvIo.D2.Validation.Abstractions`). The committed
copies are written under the `EmitCompilerGeneratedFiles` output path in `validation/abstractions/Generated/`, in a
folder named for the analyzer assembly (`DcsvIo.D2.Validation.SourceGen`) and then one named for the generator's fully
qualified name (`DcsvIo.D2.Validation.SourceGen.FieldConstraintsGenerator`).

- `FieldConstraints.g.cs` - the `DcsvIo.D2.Validation.Abstractions.FieldConstraints` static class, with one
  `public const int` per `constraints` entry.
- `Taxonomy.g.cs` - one `public enum X : byte` per `enums` entry (`NamePrefix`, `NameSuffix`, `BiologicalSex`), each
  carrying `[JsonConverter(typeof(JsonStringEnumConverter))]`.

---

## Layering

- TFM: `netstandard2.0`, which the Roslyn analyzer host requires.
- The package sets `IncludeBuildOutput=false` and `PrivateAssets="all"`, so the analyzer is available to the consumer's
  csproj and does not propagate at runtime.
- The shared scaffolding (`SpecFile`, `LoadResult<TSpec>`, `EmitDiagnostic`, and the `StringExt` / `IsExternalInit`
  polyfills) is compiled in through the `$(D2SourceGenSharedRoot)**\*.cs` `<Compile Include>` glob.

## Telemetry

Not applicable: this is a build-time analyzer with no runtime surface.

## Configuration

Not applicable: the inputs are JSON spec files declared through `<AdditionalFiles>` in the consumer csproj. There is no
runtime configuration.

---

## Reference

- [`contracts/validation/schema.json`](../../../../contracts/validation/schema.json) - JSON Schema for the spec
- [`contracts/validation/field-constraints.spec.json`](../../../../contracts/validation/field-constraints.spec.json) -
  the source-of-truth catalog
- `@dcsv-io/d2-validation-abstractions` - TypeScript catalog emitted from the same spec

---

## Validation ledger

| Emitter                      | Validated against                                                                                                                                                                                                                                                                                                  | Test double stands in for                                                                      | What retires the double              |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------- | ------------------------------------ |
| `FieldConstraintsEmitter`    | `FieldConstraintsEmitterTests` drives the real emitter over spec entries and asserts the emitted source; `FieldConstraintsGeneratorTests` runs the real `IIncrementalGenerator` through a `GeneratorDriver`; `FieldConstraintsRuntimeEmissionTests` and `TaxonomyRuntimeEmissionTests` assert the compiled output. | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| `FieldConstraintsSpecLoader` | `FieldConstraintsSpecLoaderTests` parses spec JSON through the real loader, including the malformed and schema-violating shapes.                                                                                                                                                                                   | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
| Diagnostics                  | `FieldConstraintsDiagnosticIdsTests` asserts each declared descriptor's id, severity and category.                                                                                                                                                                                                                 | Nothing. No collaborator is doubled - the emitter, the loader and the spec shape are all real. | Not applicable - there is no double. |
