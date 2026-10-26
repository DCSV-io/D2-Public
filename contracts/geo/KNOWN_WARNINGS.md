<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Known warnings — `contracts/geo/`

This document lists the build-time and pipeline-time warnings that are expected and accepted on purpose. The geo source
generators (`DcsvIo.D2.Geo.SourceGen` and the TypeScript geo emitter) and the Tier 1 subdivisions transformer of the geo
data pipeline emit them. Compare any new warning with this document:

- **In the list, with matching parameters** -> expected; no action needed
- **In the list, with different parameters** (different entity ids, different normalized name) -> escalate; it may be a
  new bug
- **Not in the list** -> escalate as a new bug, and investigate before suppressing

## Subdivision source-priority hierarchy

The subdivisions transformer uses this priority order for English displayName / officialName:

1. **Wikidata.en (P300 SPARQL label)** - the primary source. It tracks Wikipedia 1:1, reflects ISO 3166-2 reassignments,
   and labels almost every currently-active code: `missingWikidataEnCount` in `src-data/subdivisions.spec.json` gives
   the number of codes it has no label for. It wins on conflict with all other sources.
2. **debian/iso-codes `name` field** - the fallback. It supplies the names of the codes for which Wikidata has no
   `en` label (microstate territorial entries, less-trafficked dependencies, etc.). It is also the authority for which
   codes exist (the current-codes list).
3. **CLDR `cldr-subdivisions-full/en-subdivisions.json`** - not used for English displayName. CLDR is stale for many
   post-2020 ISO reassignments (Iran 2020-11-24, Norway 2020 county mergers, Estonia EE county restructuring). The
   pipeline keeps it only as a secondary seed for the localized labels of the supported locales, before Wikidata
   overrides it on conflict.
4. **Hand-rolled overlay at `contracts/geo/overlays/subdivisions.overlays.spec.json`** - last. The Tier 2 build (the
   spec-assembly step of the geo data pipeline) applies it as an explicit override. The file is empty; add an entry
   only when Wikidata.en is wrong and the Debian fallback is also unacceptable.

For non-English labels (the other supported locales: es/fr/de/it/ja/nl/ko/zh/pt/pl), Wikidata wins over CLDR on
conflict.

For endonyms (the label in the country's primary language), Wikidata is the source. Norway uses a separate cascade
`nb → nn → no → da → sv` because Wikidata stores Norwegian under multiple Bokmål/Nynorsk variants and lacks a unified
`no` locale for many subdivisions.

## D2GEO010 — catalog-uniqueness duplicates

D2GEO010 fires when two entities in the same catalog have normalized names that collide: the names are the same string
after NFD normalization, removal of combining marks, lowercasing and trimming. These are warnings, not build-blocking
errors. At runtime the resolver detects the ambiguity and returns `D2Result.NotFound` (see the ambiguity-sentinel
section below).

### Expected dupes - do not escalate

| Catalog        | Pair               | Normalized name | Category                | Why expected                                                                                                                                                                                                                                        | Escalation trigger                                                                    |
| -------------- | ------------------ | --------------- | ----------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| `subdivisions` | `BF-03` / `BF-KAD` | `kadiogo`       | parent-child legitimate | Burkina Faso region BF-03 (display name Kadiogo) + Kadiogo Province (BF-KAD, child of BF-03). The child province's endonym matches the parent region's display name.                                                                                | If a third entity joins the pair, or if either side's label changes upstream.         |
| `subdivisions` | `ES-NA` / `ES-NC`  | `navarra`       | parent-child legitimate | Navarra Province (ES-NA, historical/legacy ISO code) + Navarra Chartered Community (ES-NC, modern autonomous-community parent). Spain restructured Navarra as a chartered community in 1982 but the ISO code ES-NA persists.                        | If upstream finally retires ES-NA and the pair becomes a single entry.                |
| `subdivisions` | `GN-B` / `GN-BK`   | `boke`          | parent-child legitimate | Guinea Boké Region (GN-B) + Boké Prefecture (GN-BK, child of GN-B). Region/prefecture share the namesake city's label.                                                                                                                              | If a third entity joins, or if either label changes upstream.                         |
| `subdivisions` | `GN-F` / `GN-FA`   | `faranah`       | parent-child legitimate | Guinea Faranah Region (GN-F) + Faranah Prefecture (GN-FA, child of GN-F). Same Region/prefecture pattern.                                                                                                                                           | Same as GN-B / GN-BK.                                                                 |
| `subdivisions` | `GN-K` / `GN-KA`   | `kankan`        | parent-child legitimate | Guinea Kankan Region (GN-K) + Kankan Prefecture (GN-KA, child of GN-K).                                                                                                                                                                             | Same as GN-B / GN-BK.                                                                 |
| `subdivisions` | `GN-L` / `GN-LA`   | `labe`          | parent-child legitimate | Guinea Labé Region (GN-L) + Labé Prefecture (GN-LA, child of GN-L).                                                                                                                                                                                 | Same as GN-B / GN-BK.                                                                 |
| `subdivisions` | `GN-N` / `GN-NZ`   | `nzerekore`     | parent-child legitimate | Guinea Nzérékoré Region (GN-N) + Nzérékoré Prefecture (GN-NZ, child of GN-N).                                                                                                                                                                       | Same as GN-B / GN-BK.                                                                 |
| `subdivisions` | `ID-PA` / `ID-PP`  | `papua`         | parent-child legitimate | Indonesia Papua Province (ID-PA) + Papua Islands geographical grouping (ID-PP). Both legitimately labeled "Papua" in CLDR; child/parent semantics depend on the consumer.                                                                           | If upstream resolves the grouping ambiguity, or if a third "Papua" entity appears.    |
| `subdivisions` | `MT-45` / `MT-46`  | `rabat`         | real-world ambiguity    | Malta has two localities named Rabat: one on Gozo (MT-45) and one on Malta (MT-46). Both genuinely "Rabat" in Maltese. The resolver returns `D2Result.NotFound` (ambiguous) for bare "Rabat" input; the caller must specify the island via context. | If Malta administratively renames one of the localities; otherwise this is permanent. |
| `subdivisions` | `MT-65` / `MT-66`  | `zebbug`        | real-world ambiguity    | Same Malta dual-Żebbuġ situation: Żebbuġ on Gozo (MT-65) + Żebbuġ on Malta (MT-66). Both legitimately named Żebbuġ.                                                                                                                                 | Same as MT-45 / MT-46.                                                                |

### Checking for drift

Check this list again after each geo data refresh. The source-gen build output reports the actual D2GEO010 count;
compare it with the [expected duplicates](#expected-dupes---do-not-escalate) to detect drift.

### Escalation triggers (when to investigate)

- **Count increases beyond 10** - a new collision class appeared. Investigate before suppressing.
- **An entry leaves the list** - upstream data shifted (e.g., a rename eliminated a collision). Update this document in
  the same change.
- **An entry's normalized form changes** - either the algorithm or an upstream label changed. Verify which, and update
  either the document or the algorithm.
- **A new D2GEO00X warning code appears** that this document does not describe - investigate it, and document it if it
  is expected.

## D2GEO011 — CLDR-zombie codes filtered

D2GEO011 fires when CLDR's `cldr-subdivisions-full/en-subdivisions.json` ships a subdivision code that debian/iso-codes
no longer considers a current ISO 3166-2 code (ISO retired the code by reassignment, and CLDR did not drop the stale
label). The Tier 1 transformer drops these codes from the catalog output (Debian is the authority for which codes exist)
and emits one D2GEO011 warning per drop, so the operator can confirm what was dropped.

### Expected behavior

- The diagnostic fires at transform time (Tier 1 subdivisions transformer), not at codegen time.
- Output format: `[D2GEO011] subdivision '{code}' in {country}: CLDR label="{cldrEnLabel}"; not in current Debian iso-codes; filtered.`
- A summary line precedes the per-code detail: `[D2GEO011] dropped {N} CLDR-zombie codes`.
- Known zombies (per current upstream caches): IR-31, IR-32 (post-2020 Iran reassignment);
  NO-01/02/04/05/06/07/08/09/10/12/14/16/17/19/20 (post-2020 Norway county merger); EE-44/57/59 (Estonia county
  restructuring); plus the other codes across many countries (mostly historic reassignments), which `cldrZombieWarnings`
  in `src-data/subdivisions.spec.json` lists.
- The dropped codes have no Tier 2 spec entry, so they do not ship to consumers. The resolver and lookup tables do not
  surface them; a caller that passes a retired code gets a NotFound.

### Escalation trigger

- **A code expected to be current appears as a zombie** - investigate. Either Wikidata has the code (and is correct) but
  Debian does not (Debian is stale: file an upstream report with Debian iso-codes), or Wikidata and Debian both say the
  code is retired and only CLDR still lists it (CLDR is stale: this is the expected D2GEO011 behavior).
- **The zombie count drops significantly between refreshes** - CLDR may have shipped a cleanup pass. Check that no
  currently-active code appears in the zombie list.

## Missing Wikidata.en — operator triage log

Wikidata.en labels almost every currently-active subdivision code; the codes it has no label for, counted by
`missingWikidataEnCount` in `src-data/subdivisions.spec.json`, fall back to debian/iso-codes' `name` field. The pipeline
emits a triage list of missing Wikidata.en labels as a refresh artifact.

### When to review

- After every geo data refresh.
- Before a major release.

### Triage flow

For each row in the log:

1. Inspect the `debianFallbackName` field.
2. If the fallback is canonical English (e.g., "Kabul Province", "Tashkent Region"), take no action and leave the
   fallback in place.
3. If the fallback is awkward (e.g., non-canonical transliteration with macrons that conflict with modern usage, an
   outdated colonial-era name, etc.), add an overlay entry at `contracts/geo/overlays/subdivisions.overlays.spec.json`
   with `reason: "Wikidata.en missing; Debian fallback awkward — preferring <chosen-form>"`.

## Per-country divergence summary

After each refresh, the pipeline emits a per-country count of subdivisions where Wikidata.en disagrees with the `name`
field of debian/iso-codes. It writes the count with console.error in the `[divergence]` log lines (top 10 countries by
divergent count).

This is informational only. Divergence is the normal case: Wikidata uses modern English-canonical forms with Province /
Region suffixes, while Debian uses the ISO 3166-2 raw name field, which is often unsuffixed or has macrons /
transliteration variants. The summary helps the operator gauge data churn over time.

## Pinned canonical truths regression test

A pipeline unit test pins the canonical truths that the post-refresh
`subdivisions.spec.json` must satisfy. Examples: IR-22 contains "Hormoz", IR-00 contains "Markazi", NO-30 contains
"Viken", US-CA contains "California". The assertions match substrings (so "Tehran" matches "Tehran Province"), which
tolerates upstream format drift and still catches wholesale shifts.

### Escalation flow when this fails

1. **Inspect the failing code's row** in `subdivisions.spec.json`.
2. **Verify the underlying upstream change** - the Wikidata SPARQL label, the debian/iso-codes entry, and (if relevant)
   the CLDR alignment.
3. **One of two outcomes**:
   - Upstream cache is stale (rare; the 24h TTL covers most cases): delete the relevant file in the pipeline's
     Wikidata cache and re-run the geo data refresh.
   - Upstream is genuinely wrong (and the Debian fallback is not acceptable): add an overlay entry at
     `contracts/geo/overlays/subdivisions.overlays.spec.json` per the source-priority hierarchy above.

A failure here is a signal about the process, not a code bug: the pipeline pulled what upstream provided, and that
upstream data is unexpected.

## Manual refresh cadence guidance

No scheduled job refreshes the data. The operator runs the geo data refresh when the operator decides to.

### Recommended cadence

- Review monthly or before a major release, whichever comes first.
- After each refresh, check: the D2GEO011 zombie count, the missing-wikidata-en log, the per-country divergence summary,
  the pinned-truths test results, and the full D2GEO010 count (compare it with the expected duplicates in the table
  above).

### Out-of-cycle refresh triggers

- A pinned-truth assertion fails on someone's local run (re-pull may have new data).
- A new ISO 3166-2 reassignment is publicly announced.
- A consumer reports an obviously-wrong displayName / endonym.

## Design rationale: allow-listed instead of forced-fix

- **Parent-child cases** - inherent to how ISO 3166-2 models hierarchical subdivisions (region and sub-region share
  names). They are not a data bug, and fixing them would mean renaming legitimate entities. The resolver fail-closes at
  runtime, which is the correct behavior for genuinely ambiguous lookups.
- **Real-world ambiguity (Rabat, Żebbuġ)** - Malta really has two Rabats and two Żebbuġs, and no upstream fix is
  possible. The resolver fail-closes, and consumers must disambiguate via island context.
- **CLDR zombies (D2GEO011)** - the pipeline filters them at transform time, so the pipeline is the fix and the warning
  records each drop.
- **CLDR English drift** - Wikidata.en is the primary English label source, so no overlay is needed. The
  `contracts/geo/overlays/subdivisions.overlays.spec.json` file is empty; add an entry only when Wikidata.en is wrong
  and the Debian fallback is also unacceptable.

## Language enum scope: ISO 639-1 only (current limitation)

The `LanguageCode` enum in `DcsvIo.D2.Geo.Abstractions` covers only ISO 639-1 (the 2-letter codes that
`languages.spec.json` lists). Some Tier 2 spec data references languages with 3-letter codes that exist in ISO 639-2 /
639-3 but not 639-1:

- **Locales** whose tag starts with a 3-letter language code (see `locales.spec.json`) are such references, e.g.
  `tzm-Tfng-MA` (Standard Moroccan Tamazight), `vai-Vaii-LR` (Vai), `agq-CM` (Aghem). The spec preserves the raw
  language string; the typed `Locale.Language` nav is `null` / `undefined` for those entries.
- **Countries** with a primary language outside 639-1: `MP/fil` (Filipino), `NU/niu` (Niuean), `PW/pau` (Palauan),
  `SG/cmn` (Mandarin), `TK/tkl` (Tokelauan), `TL/tet` (Tetum), `TV/tvl` (Tuvaluan), `WF/wls` (Wallisian). The typed
  `Country.PrimaryLanguage` nav is `null` / `undefined` for those entries.

**Current handling**: `Country.PrimaryLanguage` and `Locale.Language` are nullable. They are emitted as `null` (.NET) /
`undefined` (TS) when the spec references a non-639-1 code. The raw 3-letter language string stays available in the
spec-derived scalar field on the same record.

**Other nullable fields**:

- `Country.PrimaryCurrency` is nullable for AQ (Antarctica), which has no single primary currency.
- `Country.PrimaryLocale` is nullable for the uninhabited sovereign territories AQ, BV and HM, whose CLDR data lacks a
  primary locale.
- `Country.PhoneNumberMinDigits` is nullable for the territories that lack subscriber-number data: AQ, BV, GS, HM, PN,
  TF and UM. The spec preserves null end-to-end, so consumers can tell "no data" from "0 digits".

When the set of legitimate duplicates changes (countries added, ISO renumberings, etc.), update this document in the
same commit as the change, so that a reader of a D2GEO010 / D2GEO011 warning finds every expected case here.

## Geo name-resolver cache — ambiguity-sentinel behavior

`DefaultGeoNameResolver` (`.NET`) and `tryResolveCountryByName` / `tryResolveSubdivisionByName` (TS
`@dcsv-io/d2-geo-default`) build their cache from normalized name to record on first lookup (cache-aside). When two
or more records normalize to the same name, the cache stores a single ambiguous sentinel at that key rather than picking
one record arbitrarily. Pass-1 lookups hitting the sentinel return `D2Result.NotFound` (with the
`TK.Geo.Errors.NAME_RESOLUTION_AMBIGUOUS` translation key). Pass-2 / 3 / 4 walks exclude ambiguous entries from the
candidate pool, so they cannot become a fuzzy-match winner.

This runtime behavior complements the codegen-time `D2GEO010` warning (the catalog-uniqueness duplicates under [expected
duplicates](#expected-dupes---do-not-escalate)). Codegen accepts the duplicates and the resolver fails closed at
runtime: a consumer that passes an ambiguous name (e.g. `"Papua"` for the ID-PA / ID-PP pair) gets `NotFound`, never the
wrong record. The cache build orders catalog entries deterministically (by ISO code, `StringComparer.Ordinal`) so two
processes building the cache independently mark the same set of keys as ambiguous.
