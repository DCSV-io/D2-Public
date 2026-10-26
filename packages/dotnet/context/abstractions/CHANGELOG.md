<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Context.Abstractions

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.3.21 - 2026-10-21
### Changed

- The generated readers of `MutableRequestContext` join every non-empty string element of the `amr` claim array with one
  space into `AuthMethod`, from JSON and from claims, through the new `spaceJoined` claim form.
- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.16. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.AuthContext.Abstractions` bumped to 0.2.20. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.10. This package's own surface is unchanged.
## 0.3.20 - 2026-10-20
### Changed

- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.15. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.AuthContext.Abstractions` bumped to 0.2.19. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.3.19 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.14. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.AuthContext.Abstractions` bumped to 0.2.18. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.3.18 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.13. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.AuthContext.Abstractions` bumped to 0.2.17. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.3.17 - 2026-10-17
### Changed

- Punctuation in the `MalformedActorChainException` messages of `ActorChainParser` that cite RFC 8693: the section
  sign is now the word `section`.
- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.12. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.AuthContext.Abstractions` bumped to 0.2.16. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.3.16 - 2026-10-03
### Changed

- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.AuthContext.Abstractions` bumped to 0.2.15. This package's own surface is unchanged.
## 0.3.15 - 2026-09-27
### Changed

- The README's prose lines over 120 characters are re-wrapped at that width; no wording changed. No code changed.
- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.AuthContext.Abstractions` bumped to 0.2.14. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.3.14 - 2026-09-26
### Changed

- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.AuthContext.Abstractions` bumped to 0.2.13. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.3.13 - 2026-09-11
### Changed

- The `context/source-gen` README states which `[RedactData]` placement fires redaction and which one the parity gate
  reads, in plain order. No emitted output changed.
- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.AuthContext.Abstractions` bumped to 0.2.12. This package's own surface is unchanged.
## 0.3.12 - 2026-09-10
### Changed

- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.AuthContext.Abstractions` bumped to 0.2.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
### Fixed

- The generated JSON reading of a verbatim-set claim leaves the property unassigned when the claim is present but empty,
  which is the result the `ClaimsPrincipal` reading already returned: ASP.NET expands an array claim into one claim
  entry per element, so an empty array reaches that path as no claim at all. A consumer branching on absent versus
  present-and-empty got a different result depending on which reading built the context.
## 0.3.11 - 2026-09-09
### Changed

- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.AuthContext.Abstractions` bumped to 0.2.10. This package's own surface is unchanged.
## 0.3.10 - 2026-09-08
### Changed

- The README prose is re-wrapped at 120 characters, and the context generator README contains a validation ledger
  that lists what each emitter is validated against. No code changed.
- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.AuthContext.Abstractions` bumped to 0.2.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
## 0.3.9 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.AuthContext.Abstractions` bumped to 0.2.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.1. This package's own surface is unchanged.
## 0.3.8 - 2026-09-04
### Changed

- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.AuthContext.Abstractions` bumped to 0.2.7. This package's own surface is unchanged.
## 0.3.7 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.0. That release is API-breaking - `D2Env` moved to
  `DcsvIo.D2.EnvFiles`, which now carries the `dotenv.net` runtime package. This package's own surface is unchanged; a
  consumer that reached `D2Env` through this package's closure adds `DcsvIo.D2.EnvFiles` to get it back.
- Dependency update: `DcsvIo.D2.Auth.Abstractions` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.AuthContext.Abstractions` bumped to 0.2.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Time` bumped to 0.2.1. This package's own surface is unchanged.
## 0.3.6 - 2026-08-22
### Changed

- Dependency update: DcsvIo.D2.Time bumped.
## 0.3.5 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Auth.Abstractions bumped.
- Dependency update: DcsvIo.D2.AuthContext.Abstractions bumped.
- Dependency update: DcsvIo.D2.Time bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.3.4 - 2026-08-17
### Changed

- Dependency update: DcsvIo.D2.Auth.Abstractions bumped.
- Dependency update: DcsvIo.D2.AuthContext.Abstractions bumped.
- No source in this package changed; the release fingerprint folds in the declared version of each direct consumable
  dependency, so the bumps move it.
## 0.3.3 - 2026-08-12
### Changed

- Dependency update: DcsvIo.D2.Auth.Abstractions bumped.
- Dependency update: DcsvIo.D2.AuthContext.Abstractions bumped.
## 0.3.2 - 2026-08-09
### Changed

- Removed source comments citing a retired rule catalog from this package's argument guards. No behavior change.

## 0.3.1 - 2026-08-06

### Changed

- Dependency update: DcsvIo.D2.Auth.Abstractions bumped.

## 0.3.0 - 2026-08-05

### Added

- `MutableRequestContext.RootOrgId` and `MutableRequestContext.AccessibleOrgIds`, with
  their generated read paths from the `d2_root_org_id` and `d2_accessible_org_ids` claims
  on both the JWT-payload walk and the claims-principal walk. Neither is propagated
  across a hop: they are identity facts that rebuild from the token at every sync hop.

## 0.2.5 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.Auth.Abstractions bumped.
- Dependency update: DcsvIo.D2.AuthContext.Abstractions bumped.
- Dependency update: DcsvIo.D2.Time bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.2.4 - 2026-07-28
### Changed

- Dependency bump: `DcsvIo.D2.Auth.Abstractions` 0.1.5 -> 0.2.0 and `DcsvIo.D2.AuthContext.Abstractions` 0.1.5 -> 0.1.6.
  No API, behavior or wire change in this package - its own source is byte-identical; only the versions of the
  dependencies it declares moved.
## 0.2.3 - 2026-07-26
### Changed

- NuGet `<Description>` and csproj comment punctuation normalized to keyboard ASCII (em dash, bullet and arrow glyphs
  replaced by `-` and `->`). Published metadata text only: no dependency, target framework or build setting changed.
- Keyboard-ASCII punctuation normalization in generated xmldoc comments (em dash, section sign and arrow glyphs replaced
  by `-`, `section` and `->`). Comment text only: no type, member, constant value or wire format changed.
- `#region` labels in the generated context partials use `-` instead of an em dash.
- Gallery README punctuation normalized (`RFC 8693 section 2.1` / `RFC 6749 section 3.3` spelled out instead of the
  section glyph; `facade` instead of the cedilla spelling). Wording and structure otherwise unchanged.
- Dependency update: DcsvIo.D2.Auth.Abstractions bumped.
- Dependency update: DcsvIo.D2.AuthContext.Abstractions bumped.
- Dependency update: DcsvIo.D2.Time bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.2.2 - 2026-07-23
### Changed

- Dependency update: DcsvIo.D2.Auth.Abstractions bumped (fingerprint reseed).

## 0.2.1 - 2026-07-22
### Changed

- Dependency update: DcsvIo.D2.Auth.Abstractions bumped.
- Dependency update: DcsvIo.D2.AuthContext.Abstractions bumped.
- Dependency update: DcsvIo.D2.Time bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.2.0 - 2026-07-20
### Added

- `DeviceKey` (string?, redacted, propagate, maxLength 128) on Fingerprints.
- `DeviceConfidence` (string?, maxLength 32, closed wire: High | Common | LowHostile) on Fingerprints.

### Changed

- Dependency update: DcsvIo.D2.Auth.Abstractions bumped.
- Dependency update: DcsvIo.D2.AuthContext.Abstractions bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.Auth.Abstractions bumped.
- Dependency update: DcsvIo.D2.AuthContext.Abstractions bumped.
- Dependency update: DcsvIo.D2.Time bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
