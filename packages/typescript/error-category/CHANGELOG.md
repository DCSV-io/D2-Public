<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - @dcsv-io/d2-error-category

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.1.11 - 2026-10-25
### Changed

- `package.json` has a `repository` field with the URL https://github.com/DCSV-io/D2-Public and this package's folder in it.
## 0.1.10 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
## 0.1.9 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
## 0.1.8 - 2026-10-17
### Changed

- Punctuation in comments and documentation only.
## 0.1.7 - 2026-09-11
### Changed

- README text: the sentences stating how many members the union holds now refer to the closed set instead, the parity
  sentence spells out that the .NET enum wire set, the TypeScript union and the spec are the same set where it used a
  mathematical symbol, and the public-API table columns line up. The row for this package in
  `packages/typescript/README.md` drops the same count. The published surface is unchanged.
## 0.1.6 - 2026-09-10
### Changed

- Corrected documentation text: the README sentence about what codegen emits and a comment in the smoke test. The
  published surface is unchanged.
## 0.1.5 - 2026-08-06

### Changed

- The `partial_success` category documentation states that both codes it
  classifies are failures. It previously said the category was not a failure,
  which was already untrue of `GEN_SOME_FOUND` and became untrue of
  `GEN_PARTIAL_SUCCESS`. Documentation text only: the wire value, the union
  member and the mapping are unchanged.

## 0.1.4 - 2026-08-03
### Fixed

- Release fingerprint recomputed: `vitest.config.ts` is no longer hashed as shipped package content. It is test-runner configuration and is not published, so a coverage-threshold change was bumping this package's version and every package depending on it. No shipped file changed.
## 0.1.3 - 2026-07-26
### Changed

- Keyboard-ASCII punctuation normalization in generated JSDoc comments (em dash, section sign and arrow glyphs replaced by `-`, `section` and `->`). Comment text only: no exported symbol, value or wire format changed.
## 0.1.2 - 2026-07-17
## 0.1.1 - 2026-07-17
