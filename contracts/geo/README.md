<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/geo/` — geo reference data contracts

> Parent: [`/`](../../README.md)

Audience: developers contributing to or consuming the geo data pipeline (Tier 1 src-data, Tier 2 codegen-ready specs, Tier 3 generated downstream code).

## Consumed by

The Tier 2 `*.spec.json` files at this directory's root are the codegen inputs:

- **.NET** — [`packages/dotnet/geo/source-gen/`](../../packages/dotnet/geo/source-gen/README.md) (Roslyn source-gen → record shapes, branded code types, lookups, and catalog data in `DcsvIo.D2.Geo.Abstractions` / `DcsvIo.D2.Geo.Default`)
- **TypeScript** — constants/types in `@dcsv-io/d2-geo-abstractions` / `@dcsv-io/d2-geo-default` (generated from this spec; sources committed)

Both read the same Tier 2 specs, so the .NET and TypeScript outputs come from one source.
See [contracts catalog](../README.md) for the full contract index.

## Three-tier story

The geo data moves through the tiers below, from upstream ingestion to the code that consumers call:

- **Tier 1** - `src-data/` - the output of the ingestion pipeline tooling. It is verbose: one JSON file per catalog,
  with `_provenance` per entry, `sources[]` and `fieldCoverage` diagnostics. Codegen does not consume it directly.
  Upstream sources: CLDR / IANA tzdb / libphonenumber / datasets/\* / Wikidata SPARQL / debian/iso-codes.
- **Tier 2** - `*.spec.json` at this directory's root - the Tier 1 data denormalized and reorganized to match the
  canonical entity record shapes (`Country` / `Locale` / `Currency` / `Language` / `Subdivision` / `Timezone` /
  `GeopoliticalEntity`) 1:1. Codegen consumes these files.
- **Tier 3** - the C# and TypeScript code that codegen generates from Tier 2 (`DcsvIo.D2.Geo.Default` /
  `@dcsv-io/d2-geo-default` packages). It lives in the geo libraries, not in this directory.

## Layout

```
contracts/geo/
├── src-data/                          ← TIER 1: ingestion pipeline output (verbose, per-catalog)
│   ├── countries.spec.json
│   ├── countries.schema.json
│   ├── subdivisions.spec.json
│   ├── subdivisions.schema.json
│   ├── currencies.spec.json
│   ├── currencies.schema.json
│   ├── languages.spec.json
│   ├── languages.schema.json
│   ├── locales.spec.json
│   ├── locales.schema.json
│   ├── timezones.spec.json
│   ├── timezones.schema.json
│   └── README.md
├── overlays/                          ← Trackable manual patches applied at Tier 2 build time
│   ├── countries.overlays.spec.json     (additions/overrides/removals with reason + addedAt)
│   ├── countries.overlays.schema.json
│   ├── locales.overlays.spec.json
│   ├── locales.overlays.schema.json
│   ├── subdivisions.overlays.spec.json
│   ├── subdivisions.overlays.schema.json
│   └── README.md
├── fixtures/                          ← Name-resolver confusables parity fixture and its schema
│   ├── confusables.fixture.json
│   └── confusables.schema.json
├── countries.spec.json                ← TIER 2: codegen-ready (pipeline-derived + overlays applied: $generated: true)
├── countries.schema.json
├── subdivisions.spec.json
├── subdivisions.schema.json
├── currencies.spec.json
├── currencies.schema.json
├── languages.spec.json
├── languages.schema.json
├── locales.spec.json
├── locales.schema.json
├── timezones.spec.json
├── timezones.schema.json
├── geopolitical-entities.spec.json    ← TIER 2 peer: hand-rolled ($generated: false, $source: manual)
├── geopolitical-entities.schema.json
├── KNOWN_WARNINGS.md
└── README.md (this file)
```

**Subdirectories** with their own READMEs:

- [`src-data/`](src-data/README.md) - Tier 1 pipeline-raw geo specs (verbose, per-catalog ingestion output; codegen does
  not consume them directly)
- [`overlays/`](overlays/README.md) - Tier 2 manual patches applied at build time (additions / overrides / removals with
  `reason` + `addedAt`)

See [KNOWN_WARNINGS.md](KNOWN_WARNINGS.md) for the documented build-time warnings and design-rationale entries of the
geo data pipeline (expected D2GEO010 / D2GEO011 warnings, allow-listed duplicates, and ambiguity-sentinel behavior).

**Overlays** are the extension point for entities that upstream sources omit (e.g., Kosovo) or get wrong. Each overlay
entry has a `reason` + `addedAt`, so the file records each policy decision. See
[`overlays/README.md`](./overlays/README.md) for when to overlay vs fix upstream vs hand-roll.

Every file at this directory's root contains `"$generated": true | false` + `"$source"`, so consumers and tooling can
tell pipeline-derived files from hand-rolled ones.

## Tier 2 details

### Pipeline-derived files

The Tier 2 clean pass of the geo data pipeline produces these files from the Tier 1 src-data and the hand-rolled
GeopoliticalEntity catalog. It strips `_provenance` and the pipeline diagnostics, backfills cross-catalog many-to-many
references, denormalizes Locale, and derives `IsSupported` and `IsSelectable`. Each file contains
`"$generated": true, "$source": "pipeline-derived"`. Each entry count below is the length of the `entries` array of the
file named in its row.

| File                     | Entries                        | Shape             |
| ------------------------ | ------------------------------ | ----------------- |
| `countries.spec.json`    | 250                            | `CountrySpec`     |
| `subdivisions.spec.json` | 5,046                          | `SubdivisionSpec` |
| `currencies.spec.json`   | 326 (178 active + 148 retired) | `CurrencySpec`    |
| `languages.spec.json`    | 183                            | `LanguageSpec`    |
| `locales.spec.json`      | 1,090                          | `LocaleSpec`      |
| `timezones.spec.json`    | 312                            | `TimezoneSpec`    |

Do not hand-edit these files. Regenerate them from source with the geo data pipeline (see
[How to refresh](#how-to-refresh)).

### Hand-rolled peer

`geopolitical-entities.spec.json` is a sibling of the pipeline-derived Tier 2 specs, but no upstream source produces it:
CLDR / ISO / IANA do not ship supranational grouping data (continents, regions, trade blocs, military alliances). It is
a hand-maintained catalog of supranational groupings, their country memberships and a type value for each grouping. It
contains `"$generated": false, "$source": "manual"`.

Codegen treats this file like the pipeline-derived Tier 2 peers: same shape conventions, same downstream
`GeopoliticalEntity` record type. The only difference is provenance.

## Cross-tier integrity (parity tests)

The pipeline parity tests check:

1. **Schema shape** - every pipeline-derived Tier 2 file has `$generated: true`, and the hand-rolled GE peer has
   `$generated: false`
2. **Cross-catalog foreign-key integrity** - every referenced ID (subdivision -> country, locale -> country, etc.)
   resolves to a real entry
3. **Many-to-many inverse-navigation symmetry** - `Country.subdivisionISO31662Codes` <->
   `Subdivision.countryISO31661Alpha2Code`, and the same for timezones, locales, GE, languages
4. **Denormalization integrity** - `Locale.firstDayOfWeek` must equal `Country[locale.country].firstDayOfWeek` (the
   drift guard)
5. **Derived-flag consistency** - `Currency.isSupported` / `Language.isSupported` derive from which
   `contracts/messages/*.json` files exist
6. **Encoding integrity** - invisible characters (NBSP, NNBSP, RLM) survive a write/read round-trip without
   normalization
7. **GE-Country references** - every country code in the hand-rolled catalog resolves (except the documented known
   orphans)

Any drift between catalogs fails the pipeline build.

## How to refresh

The geo data pipeline tooling regenerates Tier 1 and Tier 2. Consumers of the published packages do not run it, because
the Tier 2 specs and the generated package sources are committed.

## License attribution

All sources are Apache-2.0-compatible (this open tree's license posture):

- **CLDR**: Unicode-3.0 (no share-alike)
- **IANA tzdb**: Public domain
- **datasets/country-codes**, **datasets/currency-codes**, **datasets/language-codes**: PDDL (public domain)
- **libphonenumber**: Apache-2.0
- **Wikidata**: CC0
- **debian/iso-codes**: LGPL-2.1+

The composite Tier 2 output inherits derived-work licensing. The list above gives the license of each source.

---

## Navigation

**Consumed by:**
- `packages/dotnet/geo/source-gen/` - the .NET Roslyn source generator; it emits geo record types, code wrapper structs,
  lookup tables, and enum constants into `DcsvIo.D2.Geo.Abstractions` and `DcsvIo.D2.Geo.Default`
- `@dcsv-io/d2-geo-abstractions` / `@dcsv-io/d2-geo-default` - TypeScript record shapes, branded code types, Zod
  schemas, and catalog data (generated from this spec; sources committed)

**Generated output** is committed in the consuming packages.

Part of the [contracts catalog](../README.md).
