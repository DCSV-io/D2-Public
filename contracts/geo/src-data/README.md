<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/geo/src-data/` — pipeline-raw geo specs

## What's here

JSON specs produced end-to-end by the geo data pipeline from upstream sources (CLDR / IANA tzdb / libphonenumber /
datasets/\* / Wikidata SPARQL / debian iso-codes). Every entry traces to upstream data, and no entry is hand-written or
AI-generated. Each spec file records per-source provenance (URL + sha256 + license + fetchedAt) at its top.

| File                     | Source pipeline                                                                                                                                       | Entries                        |
| ------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------ |
| `countries.spec.json`    | datasets/country-codes + CLDR localenames-full + CLDR supplemental (week / measurement / currency / territoryInfo) + libphonenumber + Wikidata SPARQL | 249                            |
| `subdivisions.spec.json` | debian/iso-codes iso_3166-2 + CLDR cldr-subdivisions-full + Wikidata SPARQL                                                                           | 5,046                          |
| `timezones.spec.json`    | IANA zone1970.tab + IANA backward + Node ICU `Intl.DateTimeFormat`                                                                                    | 312                            |
| `languages.spec.json`    | datasets/language-codes + Wikidata SPARQL P218 + CLDR scriptMetadata/languageData + CLDR cldr-localenames-full                                        | 183                            |
| `locales.spec.json`      | CLDR availableLocales + CLDR likelySubtags + Node ICU Intl.Locale/DisplayNames + CLDR cldr-numbers-full + CLDR cldr-dates-full                        | 1,089                          |
| `currencies.spec.json`   | datasets/currency-codes + CLDR currencyData + CLDR cldr-numbers-full                                                                                  | 326 (178 active + 148 retired) |

## Not directly consumed by codegen

These files are pipeline-raw. They include:

- Per-entry `_provenance` fields (which source contributed which fields)
- Top-level `sources[]` array (full provenance per upstream pull)
- Top-level `fieldCoverage`, `orderBreakdown`, `wikidataFills`, `tagShape` and other build-time diagnostics
- `$note`, which records the build version and the remaining gaps

The codegen-ready specs that `DcsvIo.D2.Geo.Default` / `@dcsv-io/d2-geo-default` consume are one level up, at
`contracts/geo/*.spec.json`. The Tier 2 clean pass produces them: it strips the diagnostics, backfills cross-catalog
many-to-many references, denormalizes Locale, and derives `IsSelectable`/`IsSupported`.

See [`../README.md`](../README.md) for the full three-tier layout (src-data -> Tier 2 -> hand-rolled
GeopoliticalEntity).

## How to refresh

The geo data pipeline tooling regenerates Tier 1. Consumers of the published packages do not run it, because the specs
committed here are the source inputs. Each refresh pulls from upstream (with a cache TTL) and overwrites the
corresponding `*.spec.json` in this directory.

## License attribution

Sources are mixed-license but all Apache-2.0-compatible (this open tree's license posture):

- **CLDR**: Unicode-3.0 (no share-alike)
- **IANA tzdb**: Public domain
- **datasets/country-codes**: PDDL (public domain dedication)
- **libphonenumber**: Apache-2.0
- **Wikidata**: CC0
- **debian/iso-codes**: LGPL-2.1+

The composite spec files inherit derived-work licensing: the union of the source licenses listed above.
