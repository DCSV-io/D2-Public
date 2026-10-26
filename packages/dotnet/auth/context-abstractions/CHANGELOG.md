<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.AuthContext.Abstractions

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.2.20 - 2026-10-21
### Changed

- The generated doc of `AuthMethod` says that the `amr` claim is a JSON array and that this value is its elements joined
  with one space.
- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.16. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.10. This package's own surface is unchanged.
## 0.2.19 - 2026-10-20
### Changed

- The README `HasScope` and `HasAllScopes` examples now use `orders.order.cancel` and `Scopes.Self.Read` in place of the
  earlier example scope and its `Scopes.Auth.*` constants. No code changed.
- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.15. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.2.18 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.14. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.2.17 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.13. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.2.16 - 2026-10-17
### Changed

- Punctuation in comments and documentation only.
- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.12. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.2.15 - 2026-10-03
### Changed

- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.11. This package's own surface is unchanged.
## 0.2.14 - 2026-09-27
### Changed

- The README's prose lines over 120 characters are re-wrapped at that width; no wording changed. No code changed.
- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.2.13 - 2026-09-26
### Changed

- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.2.12 - 2026-09-11
### Changed

- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.8. This package's own surface is unchanged.
## 0.2.11 - 2026-09-10
### Changed

- The README tables are padded to their own column widths. No code changed.
- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
## 0.2.10 - 2026-09-09
### Changed

- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.6. This package's own surface is unchanged.
## 0.2.9 - 2026-09-08
### Changed

- The README prose is re-wrapped at 120 characters and its banned superscript punctuation is replaced with keyboard
  ASCII. No code changed.
- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
## 0.2.8 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.1. This package's own surface is unchanged.
## 0.2.7 - 2026-09-04
### Changed

- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.3. This package's own surface is unchanged.
## 0.2.6 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.0. That release is API-breaking - `D2Env` moved to
  `DcsvIo.D2.EnvFiles`, which now carries the `dotenv.net` runtime package. This package's own surface is unchanged; a
  consumer that reached `D2Env` through this package's closure adds `DcsvIo.D2.EnvFiles` to get it back.
- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.2. This package's own surface is unchanged.
## 0.2.5 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Auth.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.2.4 - 2026-08-17
### Changed

- Dependency update: DcsvIo.D2.Auth.Abstractions bumped. No source in this package changed; the release fingerprint
  folds in the declared version of each direct consumable dependency, so the bump moves it.
## 0.2.3 - 2026-08-12
### Changed

- Dependency update: DcsvIo.D2.Auth.Abstractions bumped.
## 0.2.2 - 2026-08-09
### Changed

- Dependency update: DcsvIo.D2.Auth.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.2.1 - 2026-08-06

### Changed

- Dependency update: DcsvIo.D2.Auth.Abstractions bumped.

## 0.2.0 - 2026-08-05

### API-breaking

- `IAuthContext` gains two members, so an implementation outside this package must supply
  them: `RootOrgId` (`Guid?`, claim `d2_root_org_id`) and `AccessibleOrgIds`
  (`IReadOnlySet<string>`, claim `d2_accessible_org_ids`).

### Added

- `RootOrgId` - the root organization of the tree the operating organization belongs to,
  minted onto the session, not derived per request.
- `AccessibleOrgIds` - the organizations the caller may reach: the seat's organization and every organization beneath
  it, downward only. The property holds strings because a claim is JSON. The authorization stage converts the set once
  into a type that admits only the organizations in the set, and an absent or unconvertible set gives a reach that
  admits nothing, not a partial set.

## 0.1.7 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.Auth.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.6 - 2026-07-28
### Changed

- Dependency bump: `DcsvIo.D2.Auth.Abstractions` 0.1.5 -> 0.2.0. No API, behavior or wire change in this package - its
  own source is byte-identical; only the version of the dependency it declares moved.
## 0.1.5 - 2026-07-26
### Changed

- NuGet `<Description>` and csproj comment punctuation normalized to keyboard ASCII (em dash, bullet and arrow glyphs
  replaced by `-` and `->`). Published metadata text only: no dependency, target framework or build setting changed.
- Keyboard-ASCII punctuation normalization in generated xmldoc comments (em dash, section sign and arrow glyphs replaced
  by `-`, `section` and `->`). Comment text only: no type, member, constant value or wire format changed.
- Dependency update: DcsvIo.D2.Auth.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.4 - 2026-07-23
### Changed

- Dependency update: DcsvIo.D2.Auth.Abstractions bumped.

## 0.1.3 - 2026-07-22
### Changed

- Dependency update: DcsvIo.D2.Auth.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.2 - 2026-07-20
### Changed

- Dependency update: DcsvIo.D2.Auth.Abstractions bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.Auth.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
