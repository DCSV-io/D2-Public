<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Geo.SourceGen

> Parent: [`packages/dotnet/README.md`](../../README.md)

> **Audience**: D2 framework engineers extending or maintaining the geo data pipeline and the codegen emitting
> strongly-typed geo records into `DcsvIo.D2.Geo.Abstractions` and `DcsvIo.D2.Geo.Default`.

Roslyn `IIncrementalGenerator` that turns the pipeline-assembled geo spec files
under `contracts/geo/` into typed .NET source - consumed by `DcsvIo.D2.Geo.Abstractions`
(TYPES) and `DcsvIo.D2.Geo.Default` (DATA).

## Who consumes this

Any .NET service that needs typed ISO geo lookups uses the packages that reference this analyzer.
`DcsvIo.D2.Geo.Abstractions` references this analyzer to receive its enums /
wrapper structs / record shapes / JsonConverters / `GeoCatalog`
constants; `DcsvIo.D2.Geo.Default` references the same analyzer to receive
the per-entity static instance data and lookup tables.

## Inputs

These spec files under `contracts/geo/`:

| Spec                              | Source           |
| --------------------------------- | ---------------- |
| `countries.spec.json`             | Pipeline-derived |
| `subdivisions.spec.json`          | Pipeline-derived |
| `currencies.spec.json`            | Pipeline-derived |
| `languages.spec.json`             | Pipeline-derived |
| `locales.spec.json`               | Pipeline-derived |
| `timezones.spec.json`             | Pipeline-derived |
| `geopolitical-entities.spec.json` | Hand-written     |

Each spec carries the envelope shape `{ catalogVersion, generatedAt, entries: [...] }`.

## Multi-assembly dispatch

The single `GeoGenerator` inspects `compilation.AssemblyName` and
dispatches per target:

- `DcsvIo.D2.Geo.Abstractions` -> emit TYPES.
- `DcsvIo.D2.Geo.Default` -> emit DATA.
- Anything else -> emit nothing.

Pattern mirrors `DcsvIo.D2.Context.SourceGen`'s assembly-based dispatch.

## Diagnostic IDs

| ID         | Severity | Trigger                                                                                                     |
| ---------- | -------- | ----------------------------------------------------------------------------------------------------------- |
| `D2GEO001` | Error    | Malformed JSON / parse failure of a spec file                                                               |
| `D2GEO002` | Info     | FK code refers to entity not present in target catalog                                                      |
| `D2GEO004` | Info     | Currency code, geopolitical-entity short code or time zone segment cannot form a valid C# identifier        |
| `D2GEO006` | Error    | Missing or invalid `catalogVersion` / `generatedAt` in a spec                                               |
| `D2GEO007` | Error    | Required spec file missing from `AdditionalFiles` (one of the canonical spec file names)                    |

The Default-target emitters report `D2GEO002` and `D2GEO004` at Info where they skip a reference or a member, so
neither fails the build. The `Timezones` nested class maps a hyphen in an IANA segment to an underscore, so
`America/Port-au-Prince` emits as `Timezones.America.Port_au_Prince`.

## Layering

- TFM: `netstandard2.0` (Roslyn analyzer host requirement).
- Build-time data structures inside the generator use plain
  `HashSet<string>` / `Dictionary<TKey, TValue>` -
  `System.Collections.Frozen` is .NET 8+ only and unavailable on
  `netstandard2.0`. The emitted text (compiled on the consumer's `net10`
  target) references `FrozenSet` / `FrozenDictionary` as string
  literals; the generator itself never invokes `ToFrozenSet`.
- Packaged with `IncludeBuildOutput=false` + `PrivateAssets="all"` - the
  analyzer is referenced from the consumer csprojs and does not propagate
  at runtime.
- Shared scaffolding (`SpecFile`, `LoadResult<TSpec>`, `EmitDiagnostic`,
  `StringExt` / `IsExternalInit` polyfills) is included via the
  `$(D2SourceGenSharedRoot)**\*.cs` `<Compile Include>` glob.

## Telemetry

N/A - this is a build-time analyzer with no runtime surface.

## Configuration

N/A - inputs are JSON spec files declared via `<AdditionalFiles>` in
the consumer csproj. No runtime configuration.
