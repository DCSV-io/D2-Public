<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Caching.Abstractions

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.5.12 - 2026-10-21
### Changed

- The `ICacheInvalidationBackplane` doc says that a lost message leaves a subscriber's L1 copy in use until its local
  time to live ends. No code changed.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.10. This package's own surface is unchanged.
## 0.5.11 - 2026-10-20
### Changed

- The README example cache keys now use `Order:{orderId}` in place of the earlier user-session example key. No code changed.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.5.10 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.5.9 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.5.8 - 2026-10-17
### Changed

- Punctuation in comments and documentation only.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.5.7 - 2026-09-27
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.5.6 - 2026-09-26
### Changed

- The `SetIfEqualAsync` and `RemoveIfEqualAsync` parameter documentation describes the expected value as the value the comparison expects to find stored. No code changed.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.5.5 - 2026-09-11
### Changed

- README prose rewritten subject-first. No surface changed.
## 0.5.4 - 2026-09-10
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
## 0.5.3 - 2026-09-08
### Changed

- `ICacheAtomic.SetIfEqualAsync` and `RemoveIfEqualAsync` document the tiered flavor's backplane publish on the arm where the write took effect.
- The cache-key convention states that a key reaches log sinks verbatim through the tiered cache log delegates, so a key carries no personal value.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
## 0.5.2 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.0. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.1. This package's own surface is unchanged.
## 0.5.1 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.0. That release is API-breaking - `D2Env` moved to `DcsvIo.D2.EnvFiles`, which now carries the `dotenv.net` runtime package. This package's own surface is unchanged; a consumer that reached `D2Env` through this package's closure adds `DcsvIo.D2.EnvFiles` to get it back.
- The csproj comment and the README dependency note no longer state that `DcsvIo.D2.Utilities` carries `dotenv.net`. A consumer of these abstractions takes only `JetBrains.Annotations`, a compile-time attributes package, from that dependency. No public member changed.
## 0.5.0 - 2026-09-02
### Changed

- The package takes `DcsvIo.D2.Utilities` and checks the token-bucket key with
  `Falsey()` instead of a hand-rolled emptiness check. Consumers take
  `dotenv.net` and `JetBrains.Annotations` in their dependency closure as a
  result. No public member changed.

## 0.4.2 - 2026-09-02
### Fixed

- `InputFailures.TokenBucket` rejects a refill interval that is not a whole
  number of milliseconds. Every flavor casts the interval to whole
  milliseconds, so a fractional value such as 1.5 ms passed validation and then
  refilled the bucket at one and a half times the rate the caller asked for.
  The TypeScript implementation already rejected it, so one call was a validation
  failure on one runtime and a rescaled bucket on the other.

## 0.4.1 - 2026-09-02
### Fixed

- `ConsumeTokenAsync` keeps the accrual a refill could not spend on a whole thousandth of a token, so a bucket taking
  arrivals more often than one thousandth accrues still refills at the configured rate. The stored instant used to
  advance to now on every call while the floored remainder was dropped, which reduced the refill by the arrival rate
  and, at a high enough rate on one key, stopped it entirely. The wait that a refusal returns is now usable for the
  refusal it is returned with.
- `InputFailures.TokenBucket` rejects a refill interval under one millisecond.
  Every flavor measures the interval in whole milliseconds, so a positive
  sub-millisecond value passed validation and then divided by zero.
- `ConsumeTokenAsync` declares the `Conflict` outcome for a key that already
  holds something other than bucket state, which the in-process flavors already
  returned and the contract did not mention.

## 0.4.0 - 2026-09-02
### API-breaking

- Every implementation of `ICacheAtomic` gains `ConsumeTokenAsync`, so an
  implementer outside this repository has to supply it.

### Added

- `ICacheAtomic.ConsumeTokenAsync` - an atomic token-bucket refill-then-consume,
  with `TokenBucketDecision` for its result and `InputFailures.TokenBucket` for
  the argument checks every flavor shares. The bucket refills by the time
  elapsed since its last use, clamps to its capacity, and the cost is consumed
  in the same step, so there is no window boundary to spend an allowance twice
  across. The store's clock supplies the instant, not the caller. A refused
  request returns the wait until the bucket holds the cost.

## 0.3.0 - 2026-09-02
### API-breaking

- Every implementation of `ICacheAtomic` gains `RemoveIfEqualAsync<T>`, so an
  implementer outside this repository has to supply it.

### Added

- `ICacheAtomic.RemoveIfEqualAsync<T>` - atomic compare-and-delete, the
  removing sibling of `SetIfEqualAsync<T>`. It removes the key only while the
  stored value is still the one the caller states, and returns `Ok(false)` on a
  mismatch and on an absent key alike. The comparison is by value on the terms
  `SetIfEqualAsync` states, and the tiered flavor drops the local tier on every
  outcome. An owner-scoped release calls this member: it reads the entry, then
  removes it only while it is still the entry that was read.

## 0.2.2 - 2026-08-18
### Fixed

- The building-block summary list now names `SetIfEqual` among the `ICacheAtomic`
  members. It had kept the pre-compare-and-set member set while the per-member
  section further down the same README already described the new member, so the
  two halves of one document disagreed about what the interface exports.

## 0.2.1 - 2026-08-17
### Changed

- Documented `SetIfEqualAsync` in the README - its per-arm answers, the absent-key
  case, and the by-value requirement on `expected`.
## 0.2.0 - 2026-08-17
### Added

- `ICacheAtomic.SetIfEqualAsync<T>` - an atomic compare-and-set. `Ok(true)` when
  the value was written, `Ok(false)` when the stored value was not `expected`
  (which includes an absent key), and a failure on a backing-store error. A null
  expiration takes the store's default, following `SetNxAsync` rather than
  `IncrementAsync`, because this member replaces a value rather than
  accumulating onto one. The comparison is by value and the requirement on `T`
  is stated in the member's remarks rather than declared as a constraint, so the
  contract reads the same on both runtimes.

## 0.1.5 - 2026-08-06
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
- The project comment describing how tier failures are reported now says that
  `PartialSuccess`, `NotFound` and `SomeFound` all have `Success` false, so a
  caller that only checks `Failed` treats a write that reached one tier as not
  done. Comment only; no code and no public surface changed.
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
