<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - @dcsv-io/d2-encryption-abstractions

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.1.11 - 2026-10-25
### Changed

- `package.json` has a `repository` field with the URL https://github.com/DCSV-io/D2-Public and this package's folder in it.
## 0.1.10 - 2026-10-21
### Changed

- The generated doc of `CONSTRAINT_MIN_FRAME_SIZE` says that it is the floor of the fixed fields and excludes the key
  id, whose length is checked separately. No value changed.
## 0.1.9 - 2026-10-20
### Changed

- The README now describes the encryption-domains spec as the `plaintext` sentinel and the `payload-fixture-sealed`
  fixture domain, and no longer lists the product domain identifiers. No code changed.
## 0.1.8 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
## 0.1.7 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
## 0.1.6 - 2026-10-17
### Changed

- Punctuation in comments and documentation only.
## 0.1.5 - 2026-09-10
### Changed

- Corrected documentation text: the README's frame-field description and a comment in the smoke test. The published
  surface is unchanged.
## 0.1.4 - 2026-08-03
### Fixed

- Release fingerprint recomputed: `vitest.config.ts` is no longer hashed as shipped package content. It is test-runner configuration and is not published, so a coverage-threshold change was bumping this package's version and every package depending on it. No shipped file changed.
## 0.1.3 - 2026-07-26
### Changed

- Keyboard-ASCII punctuation normalization in generated JSDoc comments (em dash, section sign and arrow glyphs replaced by `-`, `section` and `->`). Comment text only: no exported symbol, value or wire format changed.
## 0.1.2 - 2026-07-17
## 0.1.1 - 2026-07-17
