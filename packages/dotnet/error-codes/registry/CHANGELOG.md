# Changelog - DcsvIo.D2.ErrorCodes.Registry

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.4.4 - 2026-10-20
### Changed

- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.4. This package's own surface is unchanged.
## 0.4.3 - 2026-10-19
### Changed

- Code comments and the `ErrorCodeInfo` `Doc` text, regenerated from the spec, rewritten to the repository writing
  standard; no code or public API shape changed.
- Dependency update: `DcsvIo.D2.ErrorCodes.Category` bumped to 0.1.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.3. This package's own surface is unchanged.
## 0.4.2 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.ErrorCodes.Category` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.2. This package's own surface is unchanged.
## 0.4.1 - 2026-10-17
### Changed

- The packaged license file no longer starts with a byte-order mark; the license text is unchanged.
- Dependency update: `DcsvIo.D2.ErrorCodes.Category` bumped to 0.1.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.1. This package's own surface is unchanged.
## 0.4.0 - 2026-09-27
### Changed

- The README's prose lines over 120 characters are re-wrapped at that width; no wording changed. No code changed.
- The registry generator refuses an error-code catalog whose filename is in no catalog identity map with the new build error `D2ERC008`, and emits no registry. It no longer derives an owner, domain and prefix from the filename of an unregistered catalog. Every catalog this package registers is in its map, so the emitted registry is unchanged.
## 0.3.6 - 2026-09-26
### Changed

- The README states that the registry contains GEN and LIB codes only, drops the category-count and row-count figures,
  and describes `RegistryEmitConfig` and the source fence test by what they do. The 0.1.2 release note describes the
  merge-scope change by code owner. No code changed.
### Fixed

- `RegistryCollisionChecker`'s reserved-namespace guard compared each GEN code against every non-GEN catalog's prefix, including one that resolved to `GEN_` itself. A spec file the identity map does not name resolves to a `GEN_` prefix under a `lib` owner, and every legitimate GEN code was then reported as carrying a foreign prefix. The guard now skips a prefix equal to the entry's own.
## 0.3.5 - 2026-09-10
### Changed

- The README tables are padded to their own column widths. No code changed.
## 0.3.4 - 2026-09-08
### Added

- The registry generator has a README listing its build-time diagnostics, its emitted shape and its validation ledger.
### Changed

- The `Domain` documentation the registry emits uses a portable example set rather than a D2 service, and states the
  derivation rule - the token is read from the catalog's declared identity rather than the filename stem - without
  referring to a product catalog. The generated `ErrorCodeInfo` xmldoc changes with it; no code, value or lookup
  behavior changed.
## 0.3.3 - 2026-09-06
### Added

- The `GEN_FORGERY_REFUSED` entry, from the new code in `contracts/error-codes/error-codes.spec.json`. It resolves with
  domain `common`, owner `gen`, HTTP status 403, category `policy_denied` and factory name `ForgeryRefused`.
## 0.3.2 - 2026-08-26
### Changed

- The generated `ErrorCodeRegistry` xmldoc no longer counts the populated registry-row fields;
  the sentence states it without a count (`All fields are populated`). Emitted by the corrected
  `RegistryEmitter`; no field, shape or value changes.
## 0.3.1 - 2026-08-20
### Added

- `LIB_MESSAGING_PUBLISH_UNROUTABLE` resolves through the registry, from the new
  `contracts/messaging-error-codes/messaging-error-codes.spec.json` catalog (owner `lib`,
  domain `messaging`). A publish that reached no queue returns it instead of the generic
  `GEN_SERVICE_UNAVAILABLE`.
## 0.3.0 - 2026-08-06

### Wire-breaking

- The registry entry for `GEN_PARTIAL_SUCCESS` has `httpStatus` `503`
  instead of `207`, and documentation stating that the outcome reports failure.
  A consumer resolving that code for its transport status gets the new value.

## 0.2.2 - 2026-08-04
### Added

- The merged registry now includes the pagination catalog, so every
  `LIB_PAGINATION_*` code resolves to its status, category and message key
  without importing the producing package.
### Changed

- Dependency update: DcsvIo.D2.I18n.Keys bumped.
## 0.2.1 - 2026-07-26
### Changed

- NuGet `<Description>` and csproj comment punctuation normalized to keyboard ASCII (em dash, bullet and arrow glyphs replaced by `-` and `->`). Published metadata text only: no dependency, target framework or build setting changed.
- Keyboard-ASCII punctuation normalization in generated xmldoc comments (em dash, section sign and arrow glyphs replaced by `-`, `section` and `->`). Comment text only: no type, member, constant value or wire format changed.
- One generated registry entry's `Doc` text uses `-` instead of an em dash. Error codes, HTTP statuses and categories are unchanged.
- Dependency update: DcsvIo.D2.ErrorCodes.Category bumped.
- Dependency update: DcsvIo.D2.I18n.Keys bumped.
## 0.2.0 - 2026-07-22

### Changed

- Package release 0.2.0 (dependency cascade and baseline reseed; no change to the public API surface).
## 0.1.2 - 2026-07-21
### Wire-breaking

- Wire codes use owner prefixes: generic catalog is `GEN_*` (was bare `NOT_FOUND` etc.); public auth catalog is `LIB_AUTH_*` (was bare `AUTH_*`).
- `ErrorCodeInfo` expands to **10 fields** - adds `Owner` (9th, closed `gen`/`lib`/`dom`) and `IsDeprecated` (10th).
- The registry aggregates only the GEN and LIB catalogs; codes from `dom`-owned catalogs no longer resolve through it.

### Changed

- Registry SourceGen uses owner-aware DomainPrefix collision (`D2ERC005`) and dual-registry fence.

## 0.1.1 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.ErrorCodes.Category bumped.
- Dependency update: DcsvIo.D2.I18n.Keys bumped.
