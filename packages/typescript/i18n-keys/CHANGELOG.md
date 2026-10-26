<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - @dcsv-io/d2-i18n-keys

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.3.5 - 2026-10-25
### Changed

- `package.json` has a `repository` field with the URL https://github.com/DCSV-io/D2-Public and this package's folder in it.
- Dependency update: `@dcsv-io/d2-i18n-abstractions` bumped to 0.1.9. This package's own surface is unchanged.
## 0.3.4 - 2026-10-19
### Changed

- Dependency update: `@dcsv-io/d2-i18n-abstractions` bumped to 0.1.8. This package's own surface is unchanged.
## 0.3.3 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-i18n-abstractions` bumped to 0.1.7. This package's own surface is unchanged.
## 0.3.2 - 2026-10-17
### Changed

- Punctuation in comments and documentation only.
- Dependency update: `@dcsv-io/d2-i18n-abstractions` bumped to 0.1.6. This package's own surface is unchanged.
## 0.3.1 - 2026-09-10
### Changed

- Dependency update: `@dcsv-io/d2-i18n-abstractions` bumped to 0.1.5. This package's own surface is unchanged.
## 0.3.0 - 2026-09-09
### Added

- `TK.Common.Pagination.SEARCH_NOT_SUPPORTED`, the message key that the pagination code uses. Every shipped locale file
  supplies its text.
## 0.2.0 - 2026-08-04
### API-breaking

- The `TK.comms.*`, `TK.files.*`, `TK.keycustodian.*`, `TK.middleware.*` and
  `TK.webclient.*` key families were removed, and so were `TK.auth.email.*`,
  `TK.auth.forgot.*`, `TK.auth.otp.*`, `TK.auth.phone.*`, `TK.auth.reset.*`,
  `TK.auth.sign.*` and `TK.auth.verify.*`.
- `TK.auth.errors` was not kept whole either. `TK.auth` now contains
  `TK.auth.errors.TEMPORARILY_UNAVAILABLE` and `TK.auth.errors.UNAUTHORIZED`,
  and nothing else: `contracts/messages` declares those message keys and
  none of the others that family contained. A reference to
  `TK.auth.errors.EMAIL_REQUIRED`, or to any other constant it contained, no
  longer resolves.
- Those families, and the `TK.auth.errors` constants that were removed with them, correspond to no message in
  `contracts/messages`, the catalog this package is generated
  from, so no locale file contains text for them. The constants were reachable
  only until the package was next regenerated.
- A consumer referencing any of them has no message to render for it and no
  replacement in this package.

### Added

- `TK.common.pagination.*`: the message keys the pagination error-code catalog
  references, so a TypeScript caller can render any message that catalog produces.
## 0.1.3 - 2026-08-03
### Changed

- Dependency update: @dcsv-io/d2-i18n-abstractions bumped.
### Fixed

- Release fingerprint recomputed: `vitest.config.ts` is no longer hashed as shipped package content. It is test-runner configuration and is not published, so a coverage-threshold change was bumping this package's version and every package depending on it. No shipped file changed.
## 0.1.2 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-i18n-abstractions bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-i18n-abstractions bumped.
