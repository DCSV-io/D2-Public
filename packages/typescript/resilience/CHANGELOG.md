<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - @dcsv-io/d2-resilience

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.1.17 - 2026-10-25
### Changed

- `package.json` has a `repository` field with the URL https://github.com/DCSV-io/D2-Public and this package's folder in it.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.17. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.11. This package's own surface is unchanged.
## 0.1.16 - 2026-10-21
### Changed

- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.16. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.10. This package's own surface is unchanged.
## 0.1.15 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.15. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.9. This package's own surface is unchanged.
## 0.1.14 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.14. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.8. This package's own surface is unchanged.
## 0.1.13 - 2026-10-17

### Changed

- Punctuation in the `RangeError` messages for a `failureThreshold`, `cooldownMs` or `maxAttempts` below its
  minimum: the sign for greater than or equal is now `>=`.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.13. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.7. This package's own surface is unchanged.
## 0.1.12 - 2026-09-28
### Changed

- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.12. This package's own surface is unchanged.
## 0.1.11 - 2026-09-11
### Changed

- Centralized timeout completion and rate-limiter waiter cleanup while preserving cancellation precedence, FIFO admission and disposal before completion.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.11. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.3. This package's own surface is unchanged.
## 0.1.10 - 2026-09-10
### Changed

- Keyboard-ASCII punctuation normalization in test names and comments. No shipped file changed.
- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.10. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-utilities` bumped to 0.1.6. This package's own surface is unchanged.
## 0.1.9 - 2026-09-09
### Changed

- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.9. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.1. This package's own surface is unchanged.
## 0.1.8 - 2026-09-06
### Changed

- Dependency update: `@dcsv-io/d2-logging` bumped to 0.1.8. This package's own surface is unchanged.
- Dependency update: `@dcsv-io/d2-result` bumped to 0.5.0. This package's own surface is unchanged.
## 0.1.7 - 2026-08-06
### Changed

- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-logging bumped.
## 0.1.6 - 2026-08-04
### Changed

- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-result bumped.
## 0.1.5 - 2026-08-03
### Changed

- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
### Fixed

- Release fingerprint recomputed: `vitest.config.ts` is no longer hashed as shipped package content. It is test-runner configuration and is not published, so a coverage-threshold change was bumping this package's version and every package depending on it. No shipped file changed.
## 0.1.4 - 2026-07-26
### Changed

- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-result bumped.
## 0.1.3 - 2026-07-22
### Changed

- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
## 0.1.2 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-logging bumped.
- Dependency update: @dcsv-io/d2-result bumped.
- Dependency update: @dcsv-io/d2-utilities bumped.
