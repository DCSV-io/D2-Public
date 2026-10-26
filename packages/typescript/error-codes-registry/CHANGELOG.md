<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - @dcsv-io/d2-error-codes-registry

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.3.10 - 2026-10-25
### Changed

- `package.json` has a `repository` field with the URL https://github.com/DCSV-io/D2-Public and this package's folder in it.
- Dependency update: `@dcsv-io/d2-error-category` bumped to 0.1.11. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-abstractions` bumped to 0.1.9. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.5. This package's own surface is unchanged.
## 0.3.9 - 2026-10-20
### Changed

- The comment in the generated `error-code-registry.g.ts` says that the `prebuild` script of this package regenerates
  the file, in place of a `pnpm --filter ts-codegen` command. No code changed.
## 0.3.8 - 2026-10-19
### Changed

- Code comments and the `ErrorCodeInfo` `Doc` text, regenerated from the spec, rewritten to the repository writing
  standard; no code or public API shape changed.
- Dependency update: `@dcsv-io/d2-error-category` bumped to 0.1.10. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-abstractions` bumped to 0.1.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.4. This package's own surface is unchanged.
## 0.3.7 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-error-category` bumped to 0.1.9. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-abstractions` bumped to 0.1.7. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.3. This package's own surface is unchanged.
## 0.3.6 - 2026-10-17
### Changed

- Punctuation in comments and documentation only.
- Dependency update: `@dcsv-io/d2-error-category` bumped to 0.1.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-abstractions` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.2. This package's own surface is unchanged.
## 0.3.5 - 2026-09-11
### Changed

- Dependency update: `@dcsv-io/d2-error-category` bumped to 0.1.7. This package's own surface is unchanged.
## 0.3.4 - 2026-09-10
### Changed

- `ErrorCodeInfo` documentation now describes only the catalogs this registry merges, and no longer tallies the field set. The `domain` and `owner` doc comments now say what decides each token, the README field table is introduced as being in the .NET record's field order, and the catalog-to-domain map lists every catalog this registry merges. No exported symbol, value or wire format changed.
- The README regeneration note no longer gives a regeneration command. It states that the generated catalog ships committed and arrives regenerated as a release.
- Dependency update: `@dcsv-io/d2-error-category` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-abstractions` bumped to 0.1.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.1. This package's own surface is unchanged.
## 0.3.3 - 2026-09-09
### Added

- The `LIB_PAGINATION_SEARCH_NOT_SUPPORTED` entry, from the new code in `contracts/pagination-error-codes/pagination-error-codes.spec.json`. It resolves with domain `pagination`, owner `lib`, HTTP status 400, category `validation_failure` and factory name `SearchNotSupported`.
### Changed

- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.0. This package's own surface is unchanged.
## 0.3.2 - 2026-09-06
### Added

- The `GEN_FORGERY_REFUSED` entry, from the new code in `contracts/error-codes/error-codes.spec.json`. It resolves with
  domain `common`, owner `gen`, HTTP status 403, category `policy_denied` and factory name `ForgeryRefused`.
## 0.3.1 - 2026-08-20
### Added

- `LIB_MESSAGING_PUBLISH_UNROUTABLE` resolves through the registry, from the new
  `contracts/messaging-error-codes/messaging-error-codes.spec.json` catalog (owner `lib`,
  domain `messaging`). A publish that reached no queue returns this code instead of the generic
  `GEN_SERVICE_UNAVAILABLE`.
## 0.3.0 - 2026-08-06

### Wire-breaking

- The registry entry for `GEN_PARTIAL_SUCCESS` has `httpStatus` `503`
  instead of `207`, and documentation stating that the outcome reports failure.
  A consumer resolving that code for its transport status gets the new value.

## 0.2.3 - 2026-08-04
### Added

- The merged registry now contains the pagination catalog, so every
  `LIB_PAGINATION_*` code resolves to its status, category and message key in
  this runtime as well.
### Changed

- Dependency update: @dcsv-io/d2-i18n-keys bumped.
## 0.2.2 - 2026-08-03
### Changed

- Dependency update: @dcsv-io/d2-error-category bumped.
- Dependency update: @dcsv-io/d2-i18n-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
### Fixed

- Release fingerprint recomputed: `vitest.config.ts` is no longer hashed as shipped package content. It is test-runner configuration and is not published, so a coverage-threshold change was bumping this package's version and every package depending on it. No shipped file changed.
## 0.2.1 - 2026-07-26
### Changed

- Keyboard-ASCII punctuation normalization in generated JSDoc comments (em dash, section sign and arrow glyphs replaced by `-`, `section` and `->`). Comment text only: no exported symbol, value or wire format changed.
- One generated registry entry's `doc` text uses `-` instead of an em dash. Error codes, HTTP statuses and categories are unchanged.
- Dependency update: @dcsv-io/d2-error-category bumped.
## 0.2.0 - 2026-07-22
## 0.1.3 - 2026-07-21
### Wire-breaking

- Wire codes use owner prefixes: `GEN_*` generic, `LIB_AUTH_*` public auth.
- `ErrorCodeInfo` expands to **10 fields** - adds `owner` (9th) and `isDeprecated` (10th).
- Registry membership pinned at 30 (15 GEN + 15 LIB_AUTH); no `DOM_*` code is included.

### Changed

- Emitter annotates owner-aware catalog identity + DomainPrefix reserved-namespace checks.

## 0.1.2 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-error-category bumped.
- Dependency update: @dcsv-io/d2-i18n-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-error-category bumped.
- Dependency update: @dcsv-io/d2-i18n-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
