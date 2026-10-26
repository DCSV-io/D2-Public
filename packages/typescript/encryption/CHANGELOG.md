<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - @dcsv-io/d2-encryption

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.1.11 - 2026-10-25
### Changed

- `package.json` has a `repository` field with the URL https://github.com/DCSV-io/D2-Public and this package's folder in it.
- Dependency update: `@dcsv-io/d2-encryption-abstractions` bumped to 0.1.11. This package's own surface is unchanged.
## 0.1.10 - 2026-10-21
### Changed

- Dependency update: `@dcsv-io/d2-encryption-abstractions` bumped to 0.1.10. This package's own surface is unchanged.
## 0.1.9 - 2026-10-20
### Changed

- The recipient service id in the sealed-frame fixture script is now `orders` in place of its earlier value. No
  published code or public API changed.
- Dependency update: `@dcsv-io/d2-encryption-abstractions` bumped to 0.1.9. This package's own surface is unchanged.
## 0.1.8 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-encryption-abstractions` bumped to 0.1.8. This package's own surface is unchanged.
## 0.1.7 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `@dcsv-io/d2-encryption-abstractions` bumped to 0.1.7. This package's own surface is unchanged.
## 0.1.6 - 2026-10-17
### Changed

- Punctuation in the `RangeError` message of `RecipientPrivateKeyring` for an empty key map: a dash is now a hyphen.
- Dependency update: `@dcsv-io/d2-encryption-abstractions` bumped to 0.1.6. This package's own surface is unchanged.
## 0.1.5 - 2026-09-10
### Changed

- Corrected the doc comment on the frame and sealed-frame codecs that describes how the version byte separates the two
  formats. Code and the published surface are unchanged.
- Dependency update: `@dcsv-io/d2-encryption-abstractions` bumped to 0.1.5. This package's own surface is unchanged.
## 0.1.4 - 2026-08-03
### Changed

- Dependency update: @dcsv-io/d2-encryption-abstractions bumped.
### Fixed

- Release fingerprint recomputed: `vitest.config.ts` is no longer hashed as shipped package content. It is test-runner configuration and is not published, so a coverage-threshold change was bumping this package's version and every package depending on it. No shipped file changed.
## 0.1.3 - 2026-07-26
### Changed

- Dependency update: @dcsv-io/d2-encryption-abstractions bumped.
## 0.1.2 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-encryption-abstractions bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: @dcsv-io/d2-encryption-abstractions bumped.
