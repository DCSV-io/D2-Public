<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - @dcsv-io/d2-geo-abstractions

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.2.1 - 2026-10-25
### Changed

- `package.json` has a `repository` field with the URL https://github.com/DCSV-io/D2-Public and this package's folder in it.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.11. This package's own surface is unchanged.
## 0.2.0 - 2026-10-21
### Changed

- `CATALOG_PUBLISHED_AT` is 2026-10-21T06:05:33.378Z, the time the geo catalog specs were regenerated. The constant is
  exported with its value as a literal type, so the exported type changed with the value (`etc/geo-abstractions.api.md`),
  which is a minor bump in 0.x.
## 0.1.14 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.9. This package's own surface is unchanged.
## 0.1.13 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.8. This package's own surface is unchanged.
## 0.1.12 - 2026-10-17
### Changed

- Punctuation and wording in comments and documentation only; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.7. This package's own surface is unchanged.
## 0.1.11 - 2026-09-11
### Changed

- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.3. This package's own surface is unchanged.
## 0.1.10 - 2026-09-10
### Changed

- Corrected the doc comment on `IGeoReference` describing the reference-data catalogs it looks up. The published
  surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.6. This package's own surface is unchanged.
## 0.1.9 - 2026-09-09
### Changed

- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.1. This package's own surface is unchanged.
## 0.1.8 - 2026-09-06
### Changed

- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.0. This package's own surface is unchanged.
## 0.1.7 - 2026-08-06
### Changed

- Dependency update: @dcsv-io/d2-result bumped.
## 0.1.6 - 2026-08-04
### Changed

- Dependency update: @dcsv-io/d2-result bumped.
## 0.1.5 - 2026-08-03
### Changed

- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
### Fixed

- Release fingerprint recomputed: `vitest.config.ts` is no longer hashed as shipped package content. It is test-runner configuration and is not published, so a coverage-threshold change was bumping this package's version and every package depending on it. No shipped file changed.
## 0.1.4 - 2026-07-26
### Changed

- Dependency update: @dcsv-io/d2-result bumped.
## 0.1.3 - 2026-07-22
### Changed

- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
## 0.1.2 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
