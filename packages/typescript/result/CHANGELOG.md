<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - @dcsv-io/d2-result

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.5.8 - 2026-10-25
### Changed

- `package.json` has a `repository` field with the URL https://github.com/DCSV-io/D2-Public and this package's folder in it.
- Dependency update: `@dcsv-io/d2-error-category` bumped to 0.1.11. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-abstractions` bumped to 0.1.9. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.11. This package's own surface is unchanged.
## 0.5.7 - 2026-10-21
### Changed

- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.10. This package's own surface is unchanged.
## 0.5.6 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-error-category` bumped to 0.1.10. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-abstractions` bumped to 0.1.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.4. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.9. This package's own surface is unchanged.
## 0.5.5 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-error-category` bumped to 0.1.9. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-abstractions` bumped to 0.1.7. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.3. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.8. This package's own surface is unchanged.
## 0.5.4 - 2026-10-17
### Changed

- Punctuation in comments and documentation only.
- Dependency update: `@dcsv-io/d2-error-category` bumped to 0.1.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-abstractions` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.2. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.7. This package's own surface is unchanged.
## 0.5.3 - 2026-09-11
### Changed

- Dependency update: `@dcsv-io/d2-error-category` bumped to 0.1.7. This package's own surface is unchanged.
## 0.5.2 - 2026-09-10
### Changed

- Keyboard-ASCII punctuation normalization in the `combine` source comments and in test names. Comment text only: no exported symbol, value or wire format changed.
- A test fixture error code no longer contains a product service name; it uses a neutral placeholder of the same
  shape. Test text only.
- Dependency update: `@dcsv-io/d2-error-category` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-abstractions` bumped to 0.1.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.1. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.6. This package's own surface is unchanged.
## 0.5.1 - 2026-09-09
### Changed

- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.0. This package's own surface is unchanged.
## 0.5.0 - 2026-09-06
### Added

- `ErrorCodes.GEN_FORGERY_REFUSED`, the `forgeryRefused` factory and the code's `getErrorHttpStatus` arm, generated from
  the new `GEN_FORGERY_REFUSED` entry in `contracts/error-codes/error-codes.spec.json`. The code is 403 `policy_denied`
  and reuses `TK.Common.Errors.FORBIDDEN`.
## 0.4.0 - 2026-08-31

### Added

- `noContent` - a success factory returning HTTP `204`. A caller implementing a
  protocol whose success response has no body can now return that status through a
  factory rather than building the result by hand. `HttpStatusCode.NoContent`
  was added to the status catalog for the same reason.

## 0.3.0 - 2026-08-06

### Wire-breaking

- `getErrorHttpStatus("GEN_PARTIAL_SUCCESS")` returns `503` instead of `207`,
  matching the .NET catalog it is pinned against. A consumer that branched on
  `207`, or that treated a `2xx` status as done, no longer sees a half-applied
  multi-target write that way. `GEN_SERVICE_UNAVAILABLE` shares the status; the
  error code distinguishes them.

### Changed

- The `GEN_PARTIAL_SUCCESS` documentation states that the outcome reports
  failure and that a consumer which may accept a partial write opts in by
  testing the code.

  This package ships no `partialSuccess` factory, so nothing here produced a
  partial write that reported success. The `isPartialSuccess` getter is derived
  from the `206` status and therefore tracks the read-side partial rather than
  this code; that is unchanged and is not what moved.

## 0.2.3 - 2026-08-04
### Changed

- Regenerated the emitted factories. The committed banner contained an em dash the
  emitter no longer writes; the file now matches what its pipeline produces.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
## 0.2.2 - 2026-08-03
### Changed

- Dependency update: @dcsv-io/d2-error-category bumped.
- Dependency update: @dcsv-io/d2-i18n-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
### Fixed

- Release fingerprint recomputed: `vitest.config.ts` is no longer hashed as shipped package content. It is test-runner configuration and is not published, so a coverage-threshold change was bumping this package's version and every package depending on it. No shipped file changed.
## 0.2.1 - 2026-07-26
### Changed

- Keyboard-ASCII punctuation normalization in generated JSDoc comments (em dash, section sign and arrow glyphs replaced by `-`, `section` and `->`). Comment text only: no exported symbol, value or wire format changed.
- Dependency update: @dcsv-io/d2-error-category bumped.
## 0.2.0 - 2026-07-22
### Changed

- Dependency update: @dcsv-io/d2-utilities bumped.
## 0.1.2 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-error-category bumped.
- Dependency update: @dcsv-io/d2-i18n-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-error-category bumped.
- Dependency update: @dcsv-io/d2-i18n-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
