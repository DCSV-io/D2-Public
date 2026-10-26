<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - @dcsv-io/d2-i18n

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.1.12 - 2026-10-25
### Changed

- `package.json` has a `repository` field with the URL https://github.com/DCSV-io/D2-Public and this package's folder in it.
- Dependency update: `@dcsv-io/d2-i18n-abstractions` bumped to 0.1.9. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.11. This package's own surface is unchanged.
## 0.1.11 - 2026-10-21
### Changed

- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.10. This package's own surface is unchanged.
## 0.1.10 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-i18n-abstractions` bumped to 0.1.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.4. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.9. This package's own surface is unchanged.
## 0.1.9 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-i18n-abstractions` bumped to 0.1.7. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.3. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.8. This package's own surface is unchanged.
## 0.1.8 - 2026-10-17
### Changed

- Punctuation and wording in comments and documentation only; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-i18n-abstractions` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.2. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.7. This package's own surface is unchanged.
## 0.1.7 - 2026-09-10
### Changed

- Corrected doc comments on `supported-locales.ts`: the wire-boundary carve-out, which lets `resolve` accept
  `string | null` from a header or cookie value, now cites the repository rule for it by identifier, and the BCP 47
  casing note uses keyboard punctuation. The published surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-abstractions` bumped to 0.1.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.1. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.6. This package's own surface is unchanged.
## 0.1.6 - 2026-09-09
### Changed

- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.0. This package's own surface is unchanged.
## 0.1.5 - 2026-08-04
### Changed

- Dependency update: @dcsv-io/d2-i18n-keys bumped.
## 0.1.4 - 2026-08-03
### Changed

- Dependency update: @dcsv-io/d2-i18n-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
### Fixed

- Release fingerprint recomputed: `vitest.config.ts` is no longer hashed as shipped package content. It is test-runner configuration and is not published, so a coverage-threshold change was bumping this package's version and every package depending on it. No shipped file changed.
## 0.1.3 - 2026-07-22
### Changed

- Dependency update: @dcsv-io/d2-utilities bumped.
## 0.1.2 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-i18n-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-i18n-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
