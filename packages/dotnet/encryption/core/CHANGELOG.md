<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Encryption

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.2.1 - 2026-10-21
### Changed

- The generated doc of the minimum frame size says that it is the floor of the fixed fields and excludes the key id. No
  value changed.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.10. This package's own surface is unchanged.
## 0.2.0 - 2026-10-20
### API-breaking

- `EncryptionKeyringSource.KeyCustodian` is renamed `EncryptionKeyringSource.Managed`. Its numeric value stays 1.
  Source that names the old member must use the new name.
### Changed

- The log messages, exception messages and documentation comments of `EncryptionSourceStartupCheck` and
  `SealedEncryptionSourceStartupCheck` describe the accepted source as a managed, rotation-aware keyring and no longer
  name a specific key service. Event ids and message parameters are unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.1.20 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.1.19 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.1.18 - 2026-10-17
### Changed

- Punctuation in exception messages: `FrameMalformedException` from `EncryptionFrame` and `SealedFrame`,
  `ArgumentException` from `RecipientPrivateKeyring`, and `InvalidOperationException` from
  `SealedEncryptionStartupCheck`. A dash is now a hyphen, and the sign for greater than or equal is now `>=`.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.1.17 - 2026-09-27
### Changed

- The README's prose lines over 120 characters are re-wrapped at that width; no wording changed. No code changed.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.1.16 - 2026-09-26
### Changed

- The README type table is re-aligned and its "see below" references are anchor links. No code changed.
## 0.1.15 - 2026-09-26
### Changed

- The 0.1.13 release note for the `encryption/domains-source-gen` README correction no longer quotes host-tree wording. The `encryption/domains-source-gen` README states the assembly-name gate for `DcsvIo.D2.Encryption` alone. No code changed.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.1.14 - 2026-09-26
### Fixed

- `EncryptionDomainModeCatalog.RegisterSealedDomain` checked conflicts only against earlier registrations, so it accepted `plaintext` and a generated domain registered with another mode or another consumer. It now throws `InvalidOperationException` for all three. The `plaintext` and mode messages use the sentences of the TypeScript `resolveDomainModes`, with the .NET enum names `Symmetric` and `Sealed`.
- The README gave the version-1 frame overhead as about 14 bytes, leaving out the 16-byte tag. It now reads 30 bytes plus the kid length, the sum `EncryptionFrame` and `encryption-frame.spec.json` (`minFrameSize`) state.
## 0.1.13 - 2026-09-14
### Changed

- Generated LoggerMessage files omit trailing whitespace; log templates and behavior are unchanged.
- The `encryption/domains-source-gen` README contained an unreadable phrase left by an ASCII normalization of a
  set-union glyph. The phrase is corrected.
## 0.1.12 - 2026-09-10
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
## 0.1.11 - 2026-09-08
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
## 0.1.10 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.1. This package's own surface is unchanged.
## 0.1.9 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.0. That release is API-breaking - `D2Env` moved to `DcsvIo.D2.EnvFiles`, which now carries the `dotenv.net` runtime package. This package's own surface is unchanged; a consumer that reached `D2Env` through this package's closure adds `DcsvIo.D2.EnvFiles` to get it back.
## 0.1.8 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.7 - 2026-08-09
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.6 - 2026-08-06
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.5 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.4 - 2026-07-26
### Changed

- NuGet `<Description>` and csproj comment punctuation normalized to keyboard ASCII (em dash, bullet and arrow glyphs replaced by `-` and `->`). Published metadata text only: no dependency, target framework or build setting changed.
- Keyboard-ASCII punctuation normalization in generated xmldoc comments (em dash, section sign and arrow glyphs replaced by `-`, `section` and `->`). Comment text only: no type, member, constant value or wire format changed.
- Startup-check log message text now uses `-` and `->` instead of em dash and arrow glyphs (for example `seal -> open round-trip`). Affects the rendered message string only: EventIds, log levels, template placeholders and code paths are unchanged.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.3 - 2026-07-22
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.2 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.1 - 2026-07-17

### Changed

- Package release 0.1.1 (dependency cascade and baseline reseed; no change to the public API surface).
