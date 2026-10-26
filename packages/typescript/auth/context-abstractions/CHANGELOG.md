<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - @dcsv-io/d2-auth-context-abstractions

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.2.7 - 2026-10-25
### Changed

- `package.json` has a `repository` field with the URL https://github.com/DCSV-io/D2-Public and this package's folder in it.
## 0.2.6 - 2026-10-21
### Changed

- The generated doc of `authMethod` says that the `amr` claim is a JSON array and that this value is its elements joined
  with one space. No value changed.
## 0.2.5 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
## 0.2.4 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
## 0.2.3 - 2026-10-17
### Changed

- Punctuation in comments and documentation only.
## 0.2.2 - 2026-09-10
### Changed

- The README codegen note no longer gives a regeneration command. It states that the generated files ship committed.
## 0.2.1 - 2026-08-05

### Changed

- Regenerated from `IAuthContext.spec.json` so the `accessibleOrgIds` JSDoc states that
  every element of the claim reaches the property unchanged, including one that is not a
  well-formed identifier, and that the conversion rejects the whole set rather than
  accepting a silently smaller one. The .NET equivalent already had this text. Comment
  text only: no exported symbol, type, value or wire format changed.

## 0.2.0 - 2026-08-05

### API-breaking

- `IAuthContext` gains `rootOrgId?: string` and `accessibleOrgIds: ReadonlySet<string>`,
  so an implementation outside this package must supply them. `accessibleOrgIds` is
  required rather than optional because the parser always populates it - an empty set
  when the claim is absent.

## 0.1.4 - 2026-08-03
### Fixed

- Release fingerprint recomputed: `vitest.config.ts` is no longer hashed as shipped package content. It is test-runner configuration and is not published, so a coverage-threshold change was bumping this package's version and every package depending on it. No shipped file changed.
## 0.1.3 - 2026-07-26
### Changed

- Keyboard-ASCII punctuation normalization in generated JSDoc comments (em dash, section sign and arrow glyphs replaced by `-`, `section` and `->`). Comment text only: no exported symbol, value or wire format changed.
## 0.1.2 - 2026-07-17
## 0.1.1 - 2026-07-17
