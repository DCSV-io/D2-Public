<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - @dcsv-io/d2-caching-abstractions

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.4.13 - 2026-10-25
### Changed

- `package.json` has a `repository` field with the URL https://github.com/DCSV-io/D2-Public and this package's folder in it.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.8. This package's own surface is unchanged.
## 0.4.12 - 2026-10-21
### Changed

- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.7. This package's own surface is unchanged.
## 0.4.11 - 2026-10-20
### Changed

- The README example cache keys now use `Order:{orderId}` in place of the earlier user-session example key. No code changed.
## 0.4.10 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.4. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.6. This package's own surface is unchanged.
## 0.4.9 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.3. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.5. This package's own surface is unchanged.
## 0.4.8 - 2026-10-17
### Changed

- Punctuation in comments and documentation only.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.2. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.4. This package's own surface is unchanged.
## 0.4.7 - 2026-09-11
### Changed

- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.3. This package's own surface is unchanged.
## 0.4.6 - 2026-09-10
### Changed

- README table columns re-padded to line up character-for-character. Documentation only.
- Dependency update: `@dcsv-io/d2-i18n-keys` bumped to 0.3.1. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.2. This package's own surface is unchanged.
## 0.4.5 - 2026-09-08
### Changed

- `ICacheAtomic.setIfEqual` and `ICacheAtomic.removeIfEqual` document what the tiered flavor publishes on a backplane:
  when the write succeeds, it invalidates the other instances; when the comparison fails, it publishes nothing; and a
  failed publish is logged rather than returned. No export, type or behavior changed.

## 0.4.4 - 2026-09-06
### Changed

- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.0. This package's own surface is unchanged.
## 0.4.3 - 2026-09-02
### Changed

- Removed an internal source comment from `InputFailures.tokenBucket`. No
  export, type or behavior changed.

## 0.4.2 - 2026-09-02
### Changed

- `InputFailures.tokenBucket` states its interval bound as a whole number of
  milliseconds, at least one, and says how the .NET equivalent reaches the same set.
  The rule the code applied did not change; the .NET equivalent was widened to match
  it in `DcsvIo.D2.Caching.Abstractions` 0.4.2.

## 0.4.1 - 2026-09-02
### Fixed

- `consumeToken` keeps the accrual a refill could not spend on a whole
  thousandth of a token, so a bucket taking arrivals more often than one
  thousandth accrues still refills at the configured rate. The stored instant
  used to advance to now on every call while the floored remainder was dropped,
  which reduced the refill by the arrival rate and, at a high enough rate on one
  key, stopped it entirely. The wait that a refused request returns is now one
  a caller can use in that case.
- `consumeToken` declares the `conflict` outcome for a key that already holds
  a value other than bucket state, which the in-process flavor already
  returned and the contract did not list.

## 0.4.0 - 2026-09-02
### API-breaking

- Every implementation of `ICacheAtomic` gains `consumeToken`, so an implementer
  outside this repository has to supply it.

### Added

- `ICacheAtomic.consumeToken` - an atomic token-bucket refill-then-consume,
  with `TokenBucketDecision` for its result and `InputFailures.tokenBucket` for
  the argument checks every flavor shares. The bucket refills by the time
  elapsed since its last use, clamps to its capacity, and the cost is consumed
  in the same step, so there is no window boundary to spend an allowance twice
  across. The store's clock supplies the instant, not the caller. A refused
  request includes the wait until the bucket has the cost, in milliseconds.

## 0.3.0 - 2026-09-02
### API-breaking

- Every implementation of `ICacheAtomic` gains `removeIfEqual<T>`, so an
  implementer outside this repository has to supply it.

### Added

- `ICacheAtomic.removeIfEqual<T>` - atomic compare-and-delete, the removal
  counterpart of `setIfEqual<T>`. It removes the key only while the stored value is
  still the one the caller specifies, and returns `ok(false)` on a mismatch and on
  an absent key alike. The comparison is by value on the terms `setIfEqual`
  states, and the tiered flavor drops the local tier on every outcome. An
  owner-scoped release calls this member: it reads the entry, then removes it
  only while it is still the entry that was read.

## 0.2.0 - 2026-08-17
### Added

- `ICacheAtomic.setIfEqual<T>` - the TypeScript counterpart of the .NET compare-and-set, with the
  same result for each outcome. `ok(true)` when written, `ok(false)` when the stored
  value was not `expected` (which includes an absent key), and a failure on a
  backing-store error.

## 0.1.7 - 2026-08-06

### Changed

- Dependency update: @dcsv-io/d2-result bumped.

## 0.1.6 - 2026-08-04
### Changed

- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-result bumped.
## 0.1.5 - 2026-08-03
### Changed

- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-result bumped.
### Fixed

- Release fingerprint recomputed: `vitest.config.ts` is no longer hashed as shipped package content. It is test-runner configuration and is not published, so a coverage-threshold change was bumping this package's version and every package depending on it. No shipped file changed.
## 0.1.4 - 2026-07-26
### Changed

- Dependency update: @dcsv-io/d2-result bumped.
## 0.1.3 - 2026-07-22
### Changed

- Dependency update: @dcsv-io/d2-result bumped.
## 0.1.2 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-result bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-i18n-keys bumped.
- Dependency update: @dcsv-io/d2-result bumped.
