<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - @dcsv-io/d2-geo-default

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.2.1 - 2026-10-25
### Changed

- `package.json` has a `repository` field with the URL https://github.com/DCSV-io/D2-Public and this package's folder in it.
- Dependency update: `@dcsv-io/d2-geo-abstractions` bumped to 0.2.1. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-request-context-abstractions` bumped to 0.2.11. This package's own surface is
  unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.11. This package's own surface is unchanged.
## 0.2.0 - 2026-10-21
### API-breaking

- `Timezones.America["Port-au-Prince"]` is renamed `Timezones.America.Port_au_Prince`, and
  `Timezones.Asia["Ust-Nera"]` is renamed `Timezones.Asia.Ust_Nera`, the .NET member names. The geo emitter writes a
  hyphen in an IANA segment as an underscore. The values, `"America/Port-au-Prince"` and `"Asia/Ust-Nera"`, and the
  `TimezoneLookup.byCode` keys are unchanged.
### Changed

- `CountryLookup` gives a primary language to these countries, which had none: `en` for MP, NU, PW, TK and TV, `zh`
  for SG, `pt` for TL and `fr` for WF, the language of each country's primary locale. Their geo data named an
  ISO 639-3 code that the ISO 639-1 language catalog has no entry for, and the countries overlay now sets the code.
- Dependency update: `@dcsv-io/d2-geo-abstractions` bumped to 0.2.0. This package's own surface is unchanged.
## 0.1.17 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-geo-abstractions` bumped to 0.1.14. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.4. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-request-context-abstractions` bumped to 0.2.9. This package's own surface is
  unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.9. This package's own surface is unchanged.
## 0.1.16 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-geo-abstractions` bumped to 0.1.13. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.3. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-request-context-abstractions` bumped to 0.2.8. This package's own surface is
  unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.8. This package's own surface is unchanged.
## 0.1.15 - 2026-10-17
### Changed

- Punctuation in comments and documentation only.
- Dependency update: `@dcsv-io/d2-geo-abstractions` bumped to 0.1.12. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.2. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-request-context-abstractions` bumped to 0.2.7. This package's own surface is
  unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.7. This package's own surface is unchanged.
## 0.1.14 - 2026-09-11
### Changed

- README table columns line up in the name-resolution table and the public-API table. Text only: the published surface
  and the emitted catalog data are unchanged.
- Dependency update: `@dcsv-io/d2-geo-abstractions` bumped to 0.1.11. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.3. This package's own surface is unchanged.
## 0.1.13 - 2026-09-10
### Changed

- Corrected documentation text: two README sentences about the generated catalogs, and comment and test-case-name text
  in the confusables parity test. The published surface and the emitted catalog data are unchanged.
- Dependency update: `@dcsv-io/d2-geo-abstractions` bumped to 0.1.10. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.1. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-request-context-abstractions` bumped to 0.2.6. This package's own surface is
  unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.6. This package's own surface is unchanged.
## 0.1.12 - 2026-09-09
### Changed

- Dependency update: `@dcsv-io/d2-geo-abstractions` bumped to 0.1.9. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.0. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.1. This package's own surface is unchanged.
## 0.1.11 - 2026-09-06
### Changed

- Dependency update: `@dcsv-io/d2-geo-abstractions` bumped to 0.1.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.0. This package's own surface is unchanged.
## 0.1.10 - 2026-08-05
### Changed

- Dependency update: @dcsv-io/d2-request-context-abstractions bumped.

## 0.1.9 - 2026-08-05
### Changed

- Dependency update: @dcsv-io/d2-geo-abstractions bumped.

## 0.1.8 - 2026-08-04
### Fixed

- The doc comment on the generated records-meta catalog no longer contains a
  path outside this package. It now states that the shape is pinned by a
  cross-language parity test without giving its location.
## 0.1.7 - 2026-08-04
### Changed

- Dependency update: @dcsv-io/d2-geo-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-result bumped.
## 0.1.6 - 2026-08-03
### Changed

- Dependency update: @dcsv-io/d2-geo-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-request-context-abstractions bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
### Fixed

- Release fingerprint recomputed: `vitest.config.ts` is no longer hashed as shipped package content. It is test-runner configuration and is not published, so a coverage-threshold change was bumping this package's version and every package depending on it. No shipped file changed.
## 0.1.5 - 2026-07-26
### Changed

- Dependency update: @dcsv-io/d2-geo-abstractions bumped.
- Dependency update: @dcsv-io/d2-request-context-abstractions bumped.
- Dependency update: @dcsv-io/d2-result bumped.
## 0.1.4 - 2026-07-22
### Changed

- Dependency update: @dcsv-io/d2-geo-abstractions bumped.
- Dependency update: @dcsv-io/d2-request-context-abstractions bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
## 0.1.3 - 2026-07-20
### Changed

- Dependency update: @dcsv-io/d2-request-context-abstractions bumped.
## 0.1.2 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-geo-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-request-context-abstractions bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-geo-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-request-context-abstractions bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
