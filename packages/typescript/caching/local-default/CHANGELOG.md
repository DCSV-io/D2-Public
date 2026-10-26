<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - @dcsv-io/d2-caching-local-default

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.4.14 - 2026-10-25
### Changed

- `package.json` has a `repository` field with the URL https://github.com/DCSV-io/D2-Public and this package's folder in it.
- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.13. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.11. This package's own surface is unchanged.
## 0.4.13 - 2026-10-21
### Changed

- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.12. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.10. This package's own surface is unchanged.
## 0.4.12 - 2026-10-20
### Changed

- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.11. This package's own surface is unchanged.
## 0.4.11 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.10. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.4. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.9. This package's own surface is unchanged.
## 0.4.10 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.9. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.3. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.8. This package's own surface is unchanged.
## 0.4.9 - 2026-10-17
### Changed

- Punctuation in comments and documentation only.
- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.2. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.7. This package's own surface is unchanged.
## 0.4.8 - 2026-09-14
### Fixed

- Token-bucket clock rollback preserves the accrual timestamp and remainder, preventing duplicate refill on recovery.
## 0.4.7 - 2026-09-11
### Changed

- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.7. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.3. This package's own surface is unchanged.
## 0.4.6 - 2026-09-10
### Changed

- README expands `SoT` to `source of truth`. Documentation only.
- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.6. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.1. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.6. This package's own surface is unchanged.
### Fixed

- `setIfEqual` and `removeIfEqual` no longer treat two different values as equal when the value has its state outside
  its own enumerable properties. A `Date`, a `Temporal` value or an object holding one in a field now compares on the
  JSON form the contract specifies, and a value that neither the structural comparison nor the JSON form can tell
  apart - a `Map`, a `Set` - returns `ok(false)` and leaves the stored value in place. Two different instants
  previously compared equal, so the write or the delete was applied to a value that was not the expected one.
## 0.4.5 - 2026-09-09
### Changed

- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.0. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.1. This package's own surface is unchanged.
## 0.4.4 - 2026-09-06
### Changed

- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.4. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.0. This package's own surface is unchanged.
## 0.4.3 - 2026-09-03
### Changed

- Depends on `@dcsv-io/d2-caching-abstractions` 0.4.3, which removed an
  internal source comment from `InputFailures.tokenBucket`. No export, type
  or behavior changed in that release, and none changed here.
## 0.4.2 - 2026-09-03
### Changed

- `consumeToken` rejects a fractional-millisecond interval with the same bound the .NET equivalent applies.
## 0.4.1 - 2026-09-02
### Fixed

- `consumeToken` keeps the accrual a refill could not spend on a whole
  thousandth of a token, in a third stored value, so a bucket taking arrivals
  more often than one thousandth accrues still refills at the configured rate.
  The stored instant used to advance to now on every call while the floored
  remainder was dropped, which reduced the refill by the arrival rate and, at a
  high enough rate on one key, stopped it entirely.

## 0.4.0 - 2026-09-02
### Added

- `consumeToken` - the token-bucket primitive over the injected clock. Every
  operation body is synchronous, so the refill and the consume cannot be split.
  A key that holds a value other than bucket state returns `conflict`, as an increment on a
  non-numeric key does.

## 0.3.0 - 2026-09-02
### Added

- `removeIfEqual<T>` - compare-and-delete over the same structural comparison
  `setIfEqual` uses. An absent or expired key returns `ok(false)` and removes
  nothing.

## 0.2.0 - 2026-08-17
### Added

- `setIfEqual`, comparing structurally over the JSON form. The store holds values
  in a `Map` with no equality of its own, and reference identity would reject
  nearly every write a caller expects to succeed, and would do so without an
  error.

## 0.1.7 - 2026-08-06

### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-result bumped.

## 0.1.6 - 2026-08-04
### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-result bumped.
## 0.1.5 - 2026-08-03
### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
### Fixed

- Release fingerprint recomputed: `vitest.config.ts` is no longer hashed as shipped package content. It is test-runner configuration and is not published, so a coverage-threshold change was bumping this package's version and every package depending on it. No shipped file changed.
## 0.1.4 - 2026-07-26
### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-result bumped.
## 0.1.3 - 2026-07-22
### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
## 0.1.2 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
