<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.PasswordHashing

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.2.10 - 2026-10-21
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.10. This package's own surface is unchanged.
## 0.2.9 - 2026-10-20
### Changed

- The comment on the Argon2id PHC regex in `Argon2idPhcPasswordHasher` describes it as having no backtracking, in place
  of an internal bucket label. No code changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.2.8 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.2.7 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.2.6 - 2026-10-17
### Changed

- The packaged license file no longer starts with a byte-order mark; the license text is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.2.5 - 2026-09-27
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.2.4 - 2026-09-26
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.2.3 - 2026-09-13
### Fixed

- The hasher snapshots validated options at construction, so later option changes cannot alter derivation, PHC encoding,
  verification, or rehash policy.
- `NeedsRehash` validates decoded salt and digest lengths against the same bounds as `Verify` and detects lengths below
  the configured policy. Valid shorter hashes still verify.
## 0.2.2 - 2026-09-11
### Fixed

- The `NoMatch` summary and the README stated that an unknown account costs what a known one costs, which was wrong.
  A real verify derives under the memory, iteration, parallelism and hash-length values read out of the stored PHC
  string, and `AreStoredCostsAccepted` admits any stored value inside the accepted range, so a row written before a
  cost increase verifies more cheaply until `NeedsRehash` has been acted on for it. Both now state that the costs are
  equal only where the stored parameters equal the configured ones.
## 0.2.1 - 2026-09-10
### Added

- `PhcPasswordHasherOptions.MIN_ACCEPTED_MEMORY_KIB_PER_LANE` publishes the Argon2id per-lane memory floor, which was
  previously an unnamed literal inside the verify path.
- Each of `MemoryKib`, `Iterations`, `Parallelism`, `SaltLength` and `HashLength` has a `[Range]` annotation stating
  the accepted bound, so a host binding the options through options validation gets the rejection at binding time.
### Changed

- The README publishes the accepted range as a table and states what `Verify` and `NeedsRehash` return for a stored
  string outside it.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
### Fixed

- `new Argon2idPhcPasswordHasher(options)` throws `ArgumentOutOfRangeException` for parameters outside the accepted
  range that `Verify` already enforced, including memory below the per-lane floor. Hash and Verify now share one
  range. A host that set `HashLength = 128`, `SaltLength = 4`, `Iterations = 32` or `MemoryKib = 2097152` previously
  got a successful `Hash` and a stored string for which this same hasher returned `Ok(false)` on every later attempt,
  which locked out every account hashed under it with no error anywhere; `MemoryKib = 0` or `Parallelism = 0` made
  `Hash` and the no-match derivation throw out of Argon2id, where `IPhcPasswordHasher.Hash` documents `Ok` or
  `ValidationFailed`.
## 0.2.0 - 2026-09-08
### Added

- `PhcPasswordHasherOptions` defines the bounds the verify path admits a stored PHC string within, as public
  constants: `MIN_ACCEPTED_MEMORY_KIB` / `MAX_ACCEPTED_MEMORY_KIB`, `MIN_ACCEPTED_ITERATIONS` /
  `MAX_ACCEPTED_ITERATIONS`, `MIN_ACCEPTED_PARALLELISM` / `MAX_ACCEPTED_PARALLELISM`, `MIN_ACCEPTED_SALT_LENGTH` /
  `MAX_ACCEPTED_SALT_LENGTH` and `MIN_ACCEPTED_HASH_LENGTH` / `MAX_ACCEPTED_HASH_LENGTH`.
### Changed

- The PHC pattern requires the tag and the `$` that separates it from the salt, and bounds each digit run. The
  previous pattern made both optional, which left two greedy runs over the same character class separated by an
  optional literal - the shape that has to be classified for backtracking rather than assumed. The pattern now has
  no backtracking, and the classification is stated in xmldoc on `PhcRegex`. A stored string with no tag at
  all now fails to match, which `Verify` already returned `false` for and `NeedsRehash` already returned `true` for on
  every other malformed shape.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
### Fixed

- `Argon2idPhcPasswordHasher.Verify` bounds the memory, iteration, parallelism, salt-length and tag-length values it
  reads out of a stored PHC string before deriving, and returns `Ok(false)` outside them. A row holding `m=0`, `t=0`
  or `p=0` previously threw `ArgumentOutOfRangeException` out of Argon2id, an absurd `m` was used as the memory cost
  whatever its size, and a one-to-three-byte tag threw as well - each of them an unhandled exception where
  `IPhcPasswordHasher.Verify` documents `Ok(false)`.
- `Verify` performs the same derivation on an absent, unparseable or out-of-bounds stored hash that it performs on a
  real mismatch. The call previously returned without deriving anything on those paths, so its response time told a
  caller whether an account existed unless every caller compensated with a dummy derivation.
- `NeedsRehash` returns `true` for a stored string whose cost parameters are outside the accepted range, rather than
  comparing an unusable row against the current policy.
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

- Removed source comments citing a retired rule catalog from this package's argument guards. No behavior change.

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
## 0.1.0 - 2026-07-21

### Added

- Initial portable Argon2id PHC hash/verify surface (`IPhcPasswordHasher`, `Argon2idPhcPasswordHasher`, `PhcPasswordHasherOptions`).
