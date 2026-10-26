# Changelog - DcsvIo.D2.EnvFiles

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.1.5 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
## 0.1.4 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
## 0.1.3 - 2026-10-17
### Changed

- The packaged license file no longer starts with a byte-order mark; the license text is unchanged.
## 0.1.2 - 2026-09-27
### Changed

- The README's prose lines over 120 characters are re-wrapped at that width; no wording changed. No code changed.
## 0.1.1 - 2026-09-08
### Changed

- The README prose is re-wrapped at 120 characters. No code changed.
## 0.1.0 - 2026-09-03
### Added

- `D2Env.Load(params string[])` - the `.env*` file reader, moved here unchanged from `DcsvIo.D2.Utilities` at version
  0.2.0. It comes with the `dotenv.net` reference it needs, so `DcsvIo.D2.Utilities` has no runtime package dependency
  and no consumer of `Falsey()` or the parse extensions gets a parser in its dependencies.
