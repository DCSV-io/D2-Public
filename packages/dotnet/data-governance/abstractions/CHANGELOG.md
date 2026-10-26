<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.DataGovernance.Abstractions

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.2.11 - 2026-10-21
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.10. This package's own surface is unchanged.
## 0.2.10 - 2026-10-20
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.2.9 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.2.8 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.2.7 - 2026-10-17
### Changed

- Punctuation and wording in comments and documentation only; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.2.6 - 2026-09-27
### Changed

- The README's prose lines over 120 characters are re-wrapped at that width; no wording changed. No code changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.2.5 - 2026-09-26
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.2.4 - 2026-09-10
### Changed

- The README tables are padded to their own column widths. No code changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
## 0.2.3 - 2026-09-08
### Changed

- The README lists `KeepOnAnonymizationAttribute` beside `AnonymizableAttribute`.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
## 0.2.2 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.0. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.1. This package's own surface is unchanged.
## 0.2.1 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.0. That release is API-breaking - `D2Env` moved to `DcsvIo.D2.EnvFiles`, which now carries the `dotenv.net` runtime package. This package's own surface is unchanged; a consumer that reached `D2Env` through this package's closure adds `DcsvIo.D2.EnvFiles` to get it back.
## 0.2.0 - 2026-08-24
### Added

- `KeepOnAnonymizationAttribute` - records that a property holding personal data
  is kept on purpose when its subject is erased, with the reason. The engine
  ignores it; it exists so a consumer's own completeness check can tell a column
  deliberately retained from one nobody declared.

## 0.1.7 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.6 - 2026-08-09
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.5 - 2026-08-06
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.4 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.3 - 2026-07-26
### Changed

- NuGet `<Description>` and csproj comment punctuation normalized to keyboard ASCII (em dash, bullet and arrow glyphs replaced by `-` and `->`). Published metadata text only: no dependency, target framework or build setting changed.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.2 - 2026-07-22
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
