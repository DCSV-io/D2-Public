<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/geo/overlays/` — trackable manual patches

## What this is

Overlay files are written by hand and applied at Tier 2 build time on top of Tier 1 src-data. Use one when an upstream
source (CLDR / IANA tzdb / datasets/\* / Wikidata SPARQL / debian iso-codes) omits, mislabels, or wrongly includes
something that D2 must handle differently.

Each overlay entry has `id`, `addedAt` and `reason` (and optionally `addedBy`), so the file records each policy
decision.

## When to overlay vs fix upstream vs hand-roll

| Situation                                                                                                                | Decision                                                             |
| ------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------- |
| Upstream is wrong and can be fixed in datasets/CLDR/etc.                                                                 | Fix upstream, wait for the next refresh.                             |
| Upstream is wrong or incomplete and fixing upstream is not viable (political deadlock, license issue, abandoned project) | **Add an overlay**. Document the why in `reason`.                    |
| Entity has no upstream source (supranational groupings, internal-only classifications)                                   | **Hand-roll a Tier 2 peer** (see `geopolitical-entities.spec.json`). |
| Field-level policy disagreement with upstream (e.g., disputed border, contested currency status)                         | **Add an override**. Document the disagreement in `reason`.          |
| Upstream ships an entry D2 does not surface (e.g., a withdrawn ISO code that lingers in caches)                          | **Add a removal**. Document why in `reason`.                         |

## Apply order

The Tier 2 build runs these steps:

1. Load Tier 1 src-data (upstream-faithful)
2. Load `overlays/*.overlays.spec.json` (manual patches)
3. **Apply additions** — append new entries to the per-catalog list
4. **Apply overrides** — patch named fields on existing entries
5. **Apply removals** — drop named entries from the per-catalog list
6. Compute cross-catalog many-to-many references and denormalization
7. Write Tier 2 output

Overlay-added entries go through the same Tier 2 logic as upstream entries, including the cross-catalog
inverse-navigation backfill. For example, the Kosovo (XK) overlay addition gets
`geopoliticalEntityShortCodes: ["EU", "BALK"]` because the hand-rolled GE catalog references it; no manual step is
needed.

## File-per-entity convention

One overlay file per Tier 1 catalog: `{entity}.overlays.spec.json`. The files are:

| File                              | Patches                                                                                                                                                      | Notes                                                                                                                                                                                                                                                                                                                              |
| --------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `countries.overlays.spec.json`    | XK (Kosovo addition); `widelyAcceptedCurrencies` overrides for AR, CH, LB, HK, KH; `primaryLanguageISO6391Code` overrides for MP, NU, PW, SG, TK, TL, TV, WF | Adds the country that upstream omits; the currency overrides add currencies that retail widely accepts beside the legal tender; the language overrides replace an ISO 639-3 primary language from datasets/country-codes, which the ISO 639-1 language catalog has no entry for, with the language of the country's primary locale |
| `subdivisions.overlays.spec.json` | none (`additions`, `overrides` and `removals` are empty)                                                                                                     | Wikidata.en is the primary English label source, so no label needs an override (see [KNOWN_WARNINGS.md](../KNOWN_WARNINGS.md))                                                                                                                                                                                                     |
| `locales.overlays.spec.json`      | fr-TF (French Southern Territories addition)                                                                                                                 | Fills CLDR availableLocales gap (no fr-TF entry shipped upstream)                                                                                                                                                                                                                                                                  |

Add any further per-entity overlay as one more file per Tier 1 catalog.

## Entry shape

Three operation types, all sharing `id` / `addedAt` / `reason` (+ optional `addedBy`):

```json
{
  "additions": [
    {
      "id": "XK",
      "addedAt": "2026-05-19",
      "addedBy": "manual policy review",
      "reason": "Why this entry exists. Future maintainers + auditors read this without git-archaeology.",
      "data": {
        /* full Tier 1 SrcData<Entity> shape */
      }
    }
  ],
  "overrides": [
    {
      "id": "AR",
      "addedAt": "...",
      "reason": "Argentina uses USD as de-facto secondary; CLDR doesn't flag this.",
      "fields": {
        /* partial: only the fields being patched */
      }
    }
  ],
  "removals": [
    {
      "id": "XX",
      "addedAt": "...",
      "reason": "Upstream ships withdrawn ISO code lingering in their cache; we don't want to surface it."
    }
  ]
}
```

## Schema validation

Each overlay file has a sibling `{entity}.overlays.schema.json` JSON Schema. Validate a file with:

```bash
pnpm dlx ajv-cli@5 validate --spec=draft2020 \
  -s contracts/geo/overlays/countries.overlays.schema.json \
  -d contracts/geo/overlays/countries.overlays.spec.json \
  --strict=false
```

## Design rationale: overlays vs hand-editing Tier 1 src-data

Tier 1 is upstream-faithful and is never hand-edited, because a full pipeline refresh overwrites any manual edit.
Overlays survive a refresh because the pipeline applies them after Tier 1 ingestion.

The `addedAt` and `reason` fields record when and why each overlay entry was added. A maintainer reading the overlay
sees why XK is there without searching commit messages.
