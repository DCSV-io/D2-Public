<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Utilities

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.3.10 - 2026-10-21
### Changed

- The email pattern rejects a domain with an empty label, such as `a@b..c` or `a@b.`, and its matching time is linear in
  the input length.
## 0.3.9 - 2026-10-20
### Changed

- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.8. This package's own surface is unchanged.
## 0.3.8 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.7. This package's own surface is unchanged.
## 0.3.7 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.6. This package's own surface is unchanged.
## 0.3.6 - 2026-10-17
### Changed

- Punctuation and wording in comments and documentation only; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.5. This package's own surface is unchanged.
## 0.3.5 - 2026-09-27
### Changed

- The README's prose lines over 120 characters are re-wrapped at that width; no wording changed. No code changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.4. This package's own surface is unchanged.
## 0.3.4 - 2026-09-26
### Changed

- The README opener lists the helper categories. The `Extensions` README writes its non-ASCII examples as C#
  code-point escapes. No code changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.3. This package's own surface is unchanged.
## 0.3.3 - 2026-09-10
### Changed

- The configuration README table is padded to its own column widths. No code changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.2. This package's own surface is unchanged.
## 0.3.2 - 2026-09-08
### Changed

- The README prose is re-wrapped at 120 characters. No code changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.1. This package's own surface is unchanged.
## 0.3.1 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.0. This package's own surface is unchanged.
## 0.3.0 - 2026-09-03
### API-breaking

- `Configuration/D2Env` moved out of this package into `DcsvIo.D2.EnvFiles`, at that package's 0.1.0. `D2Env.Load()` keeps its name, its signature and its behavior; a caller adds the `DcsvIo.D2.EnvFiles` package reference and changes the using from `DcsvIo.D2.Utilities.Configuration` to `DcsvIo.D2.EnvFiles`. The move takes the `dotenv.net` reference with it, so this package now declares no runtime NuGet dependency and no consumer of `Falsey()`, `Truthy()` or the parse extensions pulls an env-file parser into its closure.

## 0.2.0 - 2026-08-19
### Added

- `Configuration/TimeSpanRangeBounds.NO_CEILING` - the string `[Range(typeof(TimeSpan), ...)]` takes as its upper bound
  when a property's real constraint is only a floor. `RangeAttribute` parses both bounds from compile-time strings and
  offers no minimum-only form, so every such annotation had been repeating `TimeSpan.MaxValue` written out as an
  unexplained literal. The constant states the value and the explanation in one place, and a test asserts it by
  parsing it back to `TimeSpan.MaxValue`.
## 0.1.6 - 2026-08-09
### Changed

- `Extensions/README.md` no longer instructs callers to annotate each BCL `ThrowIfNull` carve-out site with a comment citing a retired rule catalog. Documentation only; no API or behavior change.

## 0.1.5 - 2026-08-06
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
## 0.1.4 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.I18n.Keys bumped.
- Dependency update: DcsvIo.D2.Result bumped.
## 0.1.3 - 2026-07-26
### Changed

- NuGet `<Description>` and csproj comment punctuation normalized to keyboard ASCII (em dash, bullet and arrow glyphs replaced by `-` and `->`). Published metadata text only: no dependency, target framework or build setting changed.
- Dependency update: DcsvIo.D2.I18n.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Keys bumped.
- Dependency update: DcsvIo.D2.Result bumped.
## 0.1.2 - 2026-07-22
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.I18n.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Keys bumped.
- Dependency update: DcsvIo.D2.Result bumped.
