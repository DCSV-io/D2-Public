<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Usernames

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.1.17 - 2026-10-21
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.10. This package's own surface is unchanged.
## 0.1.16 - 2026-10-20
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.1.15 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.1.14 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.1.13 - 2026-10-17
### Changed

- The packaged license file no longer starts with a byte-order mark; the license text is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.1.12 - 2026-09-27
### Changed

- `DefaultUsernameAdjectives.All` spells the entry `Grayed` the American way, replacing its British spelling. Usernames generated from the built-in list after this release can carry `Grayed`; usernames generated earlier keep the spelling they were issued with. The list size is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.1.11 - 2026-09-26
### Changed

- The `FriendlyUsernameGenerator`, `FriendlyUsernameGeneratorOptions`, default word-list and README documentation describe the built-in lists and suffix range without list-size or combination counts, and without naming callers. No code changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.1.10 - 2026-09-11
### Changed

- `FriendlyUsernameGenerator.cs` gains a trailing newline; nothing else changed.
- `FriendlyUsernameGenerator` has the blank line after its multi-line statement that the convention requires. No
  behavior changed.
## 0.1.9 - 2026-09-10
### Changed

- The README tables are padded to their own column widths. No code changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
## 0.1.8 - 2026-09-08
### Changed

- `FriendlyUsernameGenerator` has a `System.Globalization` using directive instead of qualifying
  `CultureInfo.InvariantCulture` inline. No behavior changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
### Fixed

- `GenerateFriendlyUsername` returns `ValidationFailed` when `MaxSuffix` is `int.MaxValue`. The exclusive bound passed
  to `RandomNumberGenerator.GetInt32` is `MaxSuffix + 1`, which overflowed there and threw `ArgumentException` even
  though the method's contract says an invalid suffix bound returns `ValidationFailed`.
## 0.1.7 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.0. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.1. This package's own surface is unchanged.
## 0.1.6 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.0. That release is API-breaking - `D2Env` moved to `DcsvIo.D2.EnvFiles`, which now carries the `dotenv.net` runtime package. This package's own surface is unchanged; a consumer that reached `D2Env` through this package's closure adds `DcsvIo.D2.EnvFiles` to get it back.
## 0.1.5 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.4 - 2026-08-09
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.3 - 2026-08-06
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.2 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.1 - 2026-07-26
### Changed

- NuGet `<Description>` and csproj comment punctuation normalized to keyboard ASCII (em dash, bullet and arrow glyphs replaced by `-` and `->`). Published metadata text only: no dependency, target framework or build setting changed.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.0 - 2026-07-24

### Added

- Initial pure friendly-username surface (`FriendlyUsername`, `FriendlyUsernameGenerator`,
  `FriendlyUsernameGeneratorOptions`, built-in 4096-word adjective and noun lists).
