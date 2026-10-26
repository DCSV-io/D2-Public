<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - @dcsv-io/d2-request-context-abstractions

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.2.11 - 2026-10-25
### Changed

- `package.json` has a `repository` field with the URL https://github.com/DCSV-io/D2-Public and this package's folder in it.
- Dependency update: `@dcsv-io/d2-auth-context-abstractions` bumped to 0.2.7. This package's own surface is unchanged.
## 0.2.10 - 2026-10-21
### Changed

- Dependency update: `@dcsv-io/d2-auth-context-abstractions` bumped to 0.2.6. This package's own surface is unchanged.
## 0.2.9 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-auth-context-abstractions` bumped to 0.2.5. This package's own surface is unchanged.
## 0.2.8 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-auth-context-abstractions` bumped to 0.2.4. This package's own surface is unchanged.
## 0.2.7 - 2026-10-17
### Changed

- Punctuation and wording in comments and documentation only; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-auth-context-abstractions` bumped to 0.2.3. This package's own surface is unchanged.
## 0.2.6 - 2026-09-10
### Changed

- Dependency update: `@dcsv-io/d2-auth-context-abstractions` bumped to 0.2.2. This package's own surface is unchanged.
## 0.2.5 - 2026-08-05
### Changed

- Dependency update: @dcsv-io/d2-auth-context-abstractions bumped.

## 0.2.4 - 2026-08-05
### Changed

- Dependency update: @dcsv-io/d2-auth-context-abstractions bumped.

## 0.2.3 - 2026-08-03
### Changed

- Dependency update: @dcsv-io/d2-auth-context-abstractions bumped.
### Fixed

- Release fingerprint recomputed: `vitest.config.ts` is no longer hashed as shipped package content. It is test-runner configuration and is not published, so a coverage-threshold change was bumping this package's version and every package depending on it. No shipped file changed.
## 0.2.2 - 2026-07-26
### Changed

- Keyboard-ASCII punctuation normalization in generated JSDoc comments (em dash, section sign and arrow glyphs replaced by `-`, `section` and `->`). Comment text only: no exported symbol, value or wire format changed.
- Dependency update: @dcsv-io/d2-auth-context-abstractions bumped.
## 0.2.1 - 2026-07-22
## 0.2.0 - 2026-07-20

### Added

- `deviceKey` (string | null, redacted, propagate, maxLength 128) on fingerprints.
- `deviceConfidence` (string | null, maxLength 32, closed wire: High | Common | LowHostile) on fingerprints.

## 0.1.2 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-auth-context-abstractions bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-auth-context-abstractions bumped.
