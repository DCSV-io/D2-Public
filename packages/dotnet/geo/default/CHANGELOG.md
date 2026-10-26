<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Geo.Default

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.2.0 - 2026-10-21
### Added

- `Timezones.America.Port_au_Prince` and `Timezones.Asia.Ust_Nera`. The geo source generator writes a hyphen in an
  IANA segment as an underscore; before, it left `America/Port-au-Prince` and `Asia/Ust-Nera` out of `Timezones`.
  `TimezoneLookup` already contained them.
### Changed

- `CountryLookup` gives a primary language to these countries, which had none: `en` for MP, NU, PW, TK and TV, `zh`
  for SG, `pt` for TL and `fr` for WF, the language of each country's primary locale. Their geo data named an
  ISO 639-3 code that the ISO 639-1 language catalog has no entry for, and the countries overlay now sets the code.
- The geo source generator reports `D2GEO002` and `D2GEO004` at Info, so neither fails a build that treats warnings
  as errors.
## 0.1.28 - 2026-10-20
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.20. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Geo.Abstractions` bumped to 0.1.28. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.1.27 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.19. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Geo.Abstractions` bumped to 0.1.27. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.1.26 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.18. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Geo.Abstractions` bumped to 0.1.26. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.1.25 - 2026-10-17
### Changed

- Punctuation and wording in comments and documentation only; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.17. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Geo.Abstractions` bumped to 0.1.25. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.1.24 - 2026-10-03
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.16. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Geo.Abstractions` bumped to 0.1.24. This package's own surface is unchanged.
## 0.1.23 - 2026-09-27
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.15. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Geo.Abstractions` bumped to 0.1.23. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.1.22 - 2026-09-26
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.14. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Geo.Abstractions` bumped to 0.1.22. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.1.21 - 2026-09-11
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.13. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Geo.Abstractions` bumped to 0.1.21. This package's own surface is unchanged.
## 0.1.20 - 2026-09-10
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.12. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Geo.Abstractions` bumped to 0.1.20. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
## 0.1.19 - 2026-09-09
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Geo.Abstractions` bumped to 0.1.19. This package's own surface is unchanged.
## 0.1.18 - 2026-09-08
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Geo.Abstractions` bumped to 0.1.18. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
## 0.1.17 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Geo.Abstractions` bumped to 0.1.17. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.0. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.1. This package's own surface is unchanged.
## 0.1.16 - 2026-09-04
### Changed

- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Geo.Abstractions` bumped to 0.1.16. This package's own surface is unchanged.
## 0.1.15 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.0. That release is API-breaking - `D2Env` moved to
  `DcsvIo.D2.EnvFiles`, which now carries the `dotenv.net` runtime package. This package's own surface is unchanged; a
  consumer that reached `D2Env` through this package's closure adds `DcsvIo.D2.EnvFiles` to get it back.
- Dependency update: `DcsvIo.D2.Context.Abstractions` bumped to 0.3.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Geo.Abstractions` bumped to 0.1.15. This package's own surface is unchanged.
## 0.1.14 - 2026-08-22
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Geo.Abstractions bumped.
## 0.1.13 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Geo.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.12 - 2026-08-17
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Geo.Abstractions bumped.
## 0.1.11 - 2026-08-12
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Geo.Abstractions bumped.
## 0.1.10 - 2026-08-09
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Geo.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.9 - 2026-08-06
### Changed

- Dependency update cascade: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Geo.Abstractions bumped.
## 0.1.8 - 2026-08-05
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Geo.Abstractions bumped.
## 0.1.7 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Geo.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Keys bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.6 - 2026-07-28
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Geo.Abstractions bumped.
## 0.1.5 - 2026-07-26
### Changed

- NuGet `<Description>` and csproj comment punctuation normalized to keyboard ASCII (em dash, bullet and arrow glyphs
  replaced by `-` and `->`). Published metadata text only: no dependency, target framework or build setting changed.
- Keyboard-ASCII punctuation normalization in generated xmldoc comments (em dash, section sign and arrow glyphs replaced
  by `-`, `section` and `->`). Comment text only: no type, member, constant value or wire format changed.
- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Geo.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Keys bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.4 - 2026-07-23
### Changed

- Dependency update: DcsvIo.D2.Geo.Abstractions bumped.
## 0.1.3 - 2026-07-22
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Geo.Abstractions bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.2 - 2026-07-20
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Geo.Abstractions bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.Context.Abstractions bumped.
- Dependency update: DcsvIo.D2.Geo.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Keys bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
