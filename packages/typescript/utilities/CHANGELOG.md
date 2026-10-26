<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - @dcsv-io/d2-utilities

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.1.11 - 2026-10-25
### Changed

- `package.json` has a `repository` field with the URL https://github.com/DCSV-io/D2-Public and this package's folder in it.
## 0.1.10 - 2026-10-21
### Changed

- `tryParseTruthyUndefInt` returns `undefined` for a number that is not a safe integer; `EMAIL_RE` rejects a domain with
  an empty label, such as `a@b..c` or `a@b.`, and runs in linear time; the `UUID_RE` comment says it accepts any
  8-4-4-4-12 hex string.
## 0.1.9 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
## 0.1.8 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
## 0.1.7 - 2026-10-17
### Changed

- Punctuation in comments and documentation only.
## 0.1.6 - 2026-09-10
### Changed

- README table columns re-padded to line up character-for-character. Documentation only.
## 0.1.5 - 2026-09-09
### Changed

- The `parseEnvArray` README example reads an environment-variable-shaped key and states the indexed keys it reads. Documentation only.
## 0.1.4 - 2026-08-03
### Fixed

- Release fingerprint recomputed: `vitest.config.ts` is no longer hashed as shipped package content. It is test-runner configuration and is not published, so a coverage-threshold change was bumping this package's version and every package depending on it. No shipped file changed.
## 0.1.3 - 2026-07-22
## 0.1.2 - 2026-07-17
## 0.1.1 - 2026-07-17
