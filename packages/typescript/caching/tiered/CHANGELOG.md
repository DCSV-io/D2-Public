<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - @dcsv-io/d2-caching-tiered

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.4.14 - 2026-10-25
### Changed

- `package.json` has a `repository` field with the URL https://github.com/DCSV-io/D2-Public and this package's folder in it.
- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.13. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-caching-distributed-redis` bumped to 0.5.14. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-caching-local-default` bumped to 0.4.14. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.17. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.8. This package's own surface is unchanged.
## 0.4.13 - 2026-10-21
### Changed

- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.12. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-caching-distributed-redis` bumped to 0.5.13. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-caching-local-default` bumped to 0.4.13. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.16. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.7. This package's own surface is unchanged.
## 0.4.12 - 2026-10-20
### Changed

- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.11. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-caching-distributed-redis` bumped to 0.5.12. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-caching-local-default` bumped to 0.4.12. This package's own surface is unchanged.
## 0.4.11 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.10. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-caching-distributed-redis` bumped to 0.5.11. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-caching-local-default` bumped to 0.4.11. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.15. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.6. This package's own surface is unchanged.
## 0.4.10 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.9. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-caching-distributed-redis` bumped to 0.5.10. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-caching-local-default` bumped to 0.4.10. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.14. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.5. This package's own surface is unchanged.
## 0.4.9 - 2026-10-17
### Changed

- Punctuation in comments and documentation only.
- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-caching-distributed-redis` bumped to 0.5.9. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-caching-local-default` bumped to 0.4.9. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.13. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.4. This package's own surface is unchanged.
## 0.4.8 - 2026-09-28
### Changed

- Dependency update: `@dcsv-io/d2-caching-distributed-redis` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.12. This package's own surface is unchanged.
## 0.4.7 - 2026-09-11
### Changed

- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.7. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-caching-distributed-redis` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-caching-local-default` bumped to 0.4.7. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.11. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.3. This package's own surface is unchanged.
## 0.4.6 - 2026-09-10
### Changed

- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.6. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-caching-distributed-redis` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-caching-local-default` bumped to 0.4.6. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.10. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.2. This package's own surface is unchanged.
## 0.4.5 - 2026-09-08
### Changed

- `setIfEqual` and `removeIfEqual` now publish the key on a backplane passed at construction, when the conditional
  write takes effect, so the other instances drop the local copy of the value it replaced. When the comparison fails,
  nothing is published, and a failed publish is logged rather than returned.
- New log function `logInvalidationPublishFailed` records a publish that failed on the backplane.
- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.5. This package's own surface is unchanged.

## 0.4.4 - 2026-09-06
### Changed

- Dependency update: `@dcsv-io/d2-caching-abstractions` bumped to 0.4.4. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-caching-distributed-redis` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-caching-local-default` bumped to 0.4.4. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.0. This package's own surface is unchanged.
## 0.4.3 - 2026-09-03
### Changed

- Depends on `@dcsv-io/d2-caching-abstractions` 0.4.3, which removed an
  internal source comment from `InputFailures.tokenBucket`. No export, type
  or behavior changed in that release, and none changed here.
- Dependency update: `@dcsv-io/d2-caching-distributed-redis` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-caching-local-default` bumped to 0.4.3. This package's own surface is unchanged.
## 0.4.2 - 2026-09-03
### Changed

- `consumeToken` input bounds match the .NET equivalent; no behavior change for whole-millisecond intervals.
- Dependency update: `@dcsv-io/d2-caching-distributed-redis` bumped to 0.5.1. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-caching-local-default` bumped to 0.4.2. This package's own surface is unchanged.
## 0.4.1 - 2026-09-02
### Changed

- Depends on `@dcsv-io/d2-caching-abstractions` at 0.4.1 (the token-bucket refill
  keeps its accrual remainder); no content change in this package.

## 0.4.0 - 2026-09-02
### Added

- `consumeToken` - the token-bucket primitive, pure delegation to L2 as the
  lock members are. The bucket state belongs to the shared store, and a local
  copy of it would apply to one node rather than to the cluster.

## 0.3.0 - 2026-09-02
### Added

- `removeIfEqual<T>` - compare-and-delete through L2, then the local tier is
  dropped on every outcome, in the order `setIfEqual` uses for its write.
- `TieredCacheOp.REMOVE_IF_EQUAL`, the operation name the L1-drop warning uses
  for the new member.

## 0.2.0 - 2026-08-17
### Added

- `setIfEqual`, decided at L2 and dropping the local tier on every outcome -
  written, comparison failed and store failure alike, exactly as the .NET tiered
  flavor does.
- `logL1DropFailed`, so the drop-on-every-outcome path logs a message that does
  not claim L2 succeeded.

## 0.1.7 - 2026-08-06
### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-caching-distributed-redis bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
## 0.1.6 - 2026-08-04
### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-caching-distributed-redis bumped.
- Dependency update: @dcsv-io/d2-caching-local-default bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-result bumped.
## 0.1.5 - 2026-08-03
### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-caching-distributed-redis bumped.
- Dependency update: @dcsv-io/d2-caching-local-default bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-result bumped.
### Fixed

- Release fingerprint recomputed: `vitest.config.ts` is no longer hashed as shipped package content. It is test-runner configuration and is not published, so a coverage-threshold change was bumping this package's version and every package depending on it. No shipped file changed.
## 0.1.4 - 2026-07-26
### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-caching-distributed-redis bumped.
- Dependency update: @dcsv-io/d2-caching-local-default bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-result bumped.
## 0.1.3 - 2026-07-22
### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-caching-distributed-redis bumped.
- Dependency update: @dcsv-io/d2-caching-local-default bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-result bumped.
## 0.1.2 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-caching-distributed-redis bumped.
- Dependency update: @dcsv-io/d2-caching-local-default bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-result bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-caching-abstractions bumped.
- Dependency update: @dcsv-io/d2-caching-distributed-redis bumped.
- Dependency update: @dcsv-io/d2-caching-local-default bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-result bumped.
