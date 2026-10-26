<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - @dcsv-io/d2-caching-distributed-redis

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.5.14 - 2026-10-25
### Changed

- `package.json` has a `repository` field with the URL https://github.com/DCSV-io/D2-Public and this package's folder in it.
- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.13. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.17. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.11. This package's own surface is unchanged.
## 0.5.13 - 2026-10-21
### Changed

- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.12. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.16. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.10. This package's own surface is unchanged.
## 0.5.12 - 2026-10-20
### Changed

- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.11. This package's own surface is unchanged.
## 0.5.11 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.10. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.4. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.15. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.9. This package's own surface is unchanged.
## 0.5.10 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.9. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.3. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.14. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.8. This package's own surface is unchanged.
## 0.5.9 - 2026-10-17
### Changed

- Punctuation and wording in comments and documentation only; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.2. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.13. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.7. This package's own surface is unchanged.
## 0.5.8 - 2026-09-28
### Changed

- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.12. This package's own surface is unchanged.
## 0.5.7 - 2026-09-14
### Fixed

- Token-bucket clock rollback preserves the accrual timestamp and remainder, preventing duplicate refill on recovery.
## 0.5.6 - 2026-09-11
### Changed

- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.7. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.11. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.3. This package's own surface is unchanged.
## 0.5.5 - 2026-09-10
### Changed

- README table columns re-padded to line up character-for-character. Documentation only.
- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.6. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.1. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.10. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.6. This package's own surface is unchanged.
### Fixed

- `setIfEqual` rejects a resolved lifetime that is not positive - the argument when one is given, the store's
  `defaultExpirationMs` otherwise - with a validation failure on `expirationMs`. It previously reached the script's
  no-expiry branch and wrote an entry Redis never expires. The .NET equivalent already rejects the same resolved
  value.
- The integration test for the wait that a refused token bucket returns asserts a band inside the refill interval
  rather than an exact 6000 ms. The script refills from the server's own clock, so the exact value held only when the
  calls spending the burst ran within one server millisecond. Matches the .NET equivalent. No shipped file changed.
## 0.5.4 - 2026-09-09
### Changed

- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.0. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.9. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.1. This package's own surface is unchanged.
## 0.5.3 - 2026-09-06
### Changed

- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.4. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.0. This package's own surface is unchanged.
## 0.5.2 - 2026-09-03
### Changed

- Depends on `@dcsv-io/d2-caching-abstractions` 0.4.3, which removed an
  internal source comment from `InputFailures.tokenBucket`. No export, type
  or behavior changed in that release, and none changed here.
## 0.5.1 - 2026-09-03
### Changed

- The package's own suite asserts the exported Lua script bodies directly -
  each one is exported, non-empty, trimmed, and references the key it acts on - and
  no longer reads an external fixture. The cross-runtime comparison against the
  .NET equivalent is asserted outside this package and no longer runs here.
## 0.5.0 - 2026-09-02
### Added

- `REMOVE_IF_EQUAL` is added to the exported script-body constants beside the other script
  bodies, which pin the bodies to the .NET scripts. It shipped at 0.3.0 without an export, so the
  cross-runtime parity suite had no way to read it and a divergence from the .NET body passed the gate.

### Fixed

- `CONSUME_TOKEN` keeps the accrual a refill could not spend on a whole
  thousandth of a token, in a third hash field, so a bucket taking arrivals more
  often than one thousandth accrues still refills at the configured rate. The
  stored instant used to advance to now on every call while the floored
  remainder was dropped, which reduced the refill by the arrival rate and, at a
  high enough rate on one key, stopped it entirely.
- `consumeToken` returns `conflict` for a key that already holds a value other
  than a bucket hash. The `WRONGTYPE` error the server raises used to be returned as a
  store outage, which the caller treats as a fault instead of as the type
  collision the in-process flavor reports.

## 0.4.0 - 2026-09-02
### Added

- `consumeToken` - the token-bucket primitive through the new `CONSUME_TOKEN`
  Lua script, exported for dual-runtime parity checks and byte-equivalent to the
  .NET body. The instant comes from the server's own `TIME`, so two nodes whose
  clocks disagree still charge one bucket consistently.

## 0.3.0 - 2026-09-02
### Added

- `removeIfEqual<T>` - compare-and-delete through the new `REMOVE_IF_EQUAL` Lua
  script, which compares the stored bytes against the re-serialized expected
  value and `DEL`s the key in the same evaluation. The script body matches
  `RELEASE_LOCK_IF_OWNER`; the two are kept apart because they compare
  different things.

## 0.2.1 - 2026-08-18
### Fixed

- The README did not mention `setIfEqual` or the `SET_IF_EQUAL` Lua constant the barrel exports,
  and its Atomics section still said the script constants were three. A consumer reading the
  gallery README had no way to find a public export this package ships.

## 0.2.0 - 2026-08-17
### Added

- `setIfEqual` over a new `SET_IF_EQUAL` Lua constant, re-exported from the
  barrel beside `RELEASE_LOCK_IF_OWNER` and byte-equivalent to the .NET script
  the cross-runtime fixture pins.

## 0.1.7 - 2026-08-06
### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
## 0.1.6 - 2026-08-04
### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-result bumped.
## 0.1.5 - 2026-08-03
### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
### Fixed

- Release fingerprint recomputed: `vitest.config.ts` is no longer hashed as shipped package content. It is test-runner configuration and is not published, so a coverage-threshold change was bumping this package's version and every package depending on it. No shipped file changed.
## 0.1.4 - 2026-07-26
### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-result bumped.
## 0.1.3 - 2026-07-22
### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
## 0.1.2 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
