<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.DataGovernance.EntityFrameworkCore

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.2.11 - 2026-10-21
### Changed

- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped to 0.2.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.10. This package's own surface is unchanged.
## 0.2.10 - 2026-10-20
### Changed

- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped to 0.2.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.2.9 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped to 0.2.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.2.8 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped to 0.2.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.2.7 - 2026-10-17
### Changed

- Punctuation in the log message templates of `AnonymizationEngineLog` events 9403, 9407 and 9408: a dash is now a hyphen.
- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped to 0.2.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.2.6 - 2026-09-27
### Changed

- The README's prose lines over 120 characters are re-wrapped at that width; no wording changed. No code changed.
- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped to 0.2.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.2.5 - 2026-09-26
### Changed

- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped to 0.2.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.2.4 - 2026-09-10
### Changed

- The packed README contains no outbound link and refers to the decoration-paths document by name instead: the registry
  renderer cannot resolve a relative repository path.
- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped to 0.2.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
## 0.2.3 - 2026-09-08
### Changed

- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped to 0.2.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
## 0.2.2 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped to 0.2.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.1. This package's own surface is unchanged.
## 0.2.1 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.0. That release is API-breaking - `D2Env` moved to `DcsvIo.D2.EnvFiles`, which now carries the `dotenv.net` runtime package. This package's own surface is unchanged; a consumer that reached `D2Env` through this package's closure adds `DcsvIo.D2.EnvFiles` to get it back.
- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped to 0.2.1. This package's own surface is unchanged.
## 0.2.0 - 2026-08-28
### API-breaking

- `AnonymizationEngineOptions.SECTION_NAME` is now `AnonymizationEngineOptions.SECTION`,
  and its value changes from `DATA_GOVERNANCE` to `LIB_DATA_GOVERNANCE`. Configuration
  section names start with the owner prefix the error-code catalog uses: `LIB_` for a section
  a shared package owns. A host reads `LIB_DATA_GOVERNANCE__BATCHSIZE`,
  `LIB_DATA_GOVERNANCE__MAXCONCURRENCYRETRIES` and
  `LIB_DATA_GOVERNANCE__SKIPMODELVALIDATION`; the old keys bind nothing.
## 0.1.9 - 2026-08-24
### Changed

- Dependency update: `DcsvIo.D2.DataGovernance.Abstractions` bumped (the
  `KeepOnAnonymization` attribute lands there).
## 0.1.8 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.DataGovernance.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.7 - 2026-08-09
### Changed

- Dependency update: DcsvIo.D2.DataGovernance.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.6 - 2026-08-06
### Changed

- Dependency update: DcsvIo.D2.DataGovernance.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.5 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.DataGovernance.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.4 - 2026-07-26
### Changed

- NuGet `<Description>` and csproj comment punctuation normalized to keyboard ASCII (em dash, bullet and arrow glyphs replaced by `-` and `->`). Published metadata text only: no dependency, target framework or build setting changed.
- Dependency update: DcsvIo.D2.DataGovernance.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.3 - 2026-07-25

### Changed

- Package release 0.1.3 (dependency cascade and baseline reseed; no change to the public API surface).
## 0.1.2 - 2026-07-22
### Changed

- Dependency update: DcsvIo.D2.DataGovernance.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.DataGovernance.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
