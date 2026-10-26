<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Caching.Tiered

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.4.15 - 2026-10-21
### Changed

- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.12. This package's own surface is unchanged.
## 0.4.14 - 2026-10-20
### Changed

- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.8. This package's own surface is unchanged.
## 0.4.13 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.7. This package's own surface is unchanged.
## 0.4.12 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.6. This package's own surface is unchanged.
## 0.4.11 - 2026-10-17
### Changed

- Punctuation in comments and documentation only.
- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.5. This package's own surface is unchanged.
## 0.4.10 - 2026-09-27
### Changed

- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.4. This package's own surface is unchanged.
## 0.4.9 - 2026-09-26
### Changed

- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.3. This package's own surface is unchanged.
## 0.4.8 - 2026-09-11
### Changed

- `DefaultTieredCache` comments and xmldoc rewritten subject-first. No behavior changed.
- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.5. This package's own surface is unchanged.
## 0.4.7 - 2026-09-10
### Changed

- Two control-flow blocks after the L1 call are spaced. No behavior changed.
- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.2. This package's own surface is unchanged.
## 0.4.6 - 2026-09-08
### Changed

- `SetIfEqualAsync` and `RemoveIfEqualAsync` now publish the key on a registered `ICacheInvalidationBackplane` on the arm where the conditional write took effect, so the other instances drop the local copy of the value it replaced. The arm where the comparison lost publishes nothing, and a failed publish is logged rather than returned.
- New log delegate `InvalidationPublishFailed` records a publish the backplane refused.
- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.1. This package's own surface is unchanged.
## 0.4.5 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.0. This package's own surface is unchanged.
## 0.4.4 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.1. This package's own surface is unchanged.
## 0.4.3 - 2026-09-03
### Changed

- Depends on `DcsvIo.D2.Caching.Abstractions` 0.5.0, which now takes `DcsvIo.D2.Utilities` for its `Falsey()` key check, so that package enters this one's dependency closure.
## 0.4.2 - 2026-09-03
### Changed

- Depends on the caching abstractions at the advanced version; no content change in this package.
## 0.4.1 - 2026-09-02
### Changed

- Depends on `DcsvIo.D2.Caching.Abstractions` at 0.4.1 (the token-bucket refill
  keeps its accrual remainder, and the interval bound moved to one millisecond);
  no content change in this package.

## 0.4.0 - 2026-09-02
### Added

- `ConsumeTokenAsync` - the token-bucket primitive, pure delegation to L2 as
  the lock members are. The bucket state belongs to the shared store, and a
  local copy of it would describe one node, not the cluster.

## 0.3.0 - 2026-09-02
### Added

- `RemoveIfEqualAsync<T>` - compare-and-delete through L2, then the local tier
  is dropped on every outcome, in the order `SetIfEqualAsync` uses for its
  write. On the removed arm the local copy refers to an entry that is gone; on the
  other arms the node cannot know whether its copy still matches the shared
  value.

## 0.2.2 - 2026-08-18
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.

### Fixed

- The atomic-primitives heading now lists `SetIfEqualAsync` among the members it
  routes, and its L1 behavior moved into the per-member bullet list beside the
  others. It had been described in a paragraph outside the enumeration, so the
  list a reader scans for the atomic surface was one member short.

## 0.2.1 - 2026-08-17
### Changed

- Documented `SetIfEqualAsync` in the README, and why it invalidates L1 on every
  outcome rather than populating it as `SetNxAsync` does.
- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
## 0.2.0 - 2026-08-17
### Added

- `SetIfEqualAsync`, decided at L2 and dropping the local tier on EVERY outcome -
  written, comparison lost and store failure alike. Populating L1 the way
  `SetNxAsync` does would reopen the race the member exists to close: the
  backplane subscriber drops L1 on its own task, so an invalidation arriving
  between the L2 hop and the populate would be undone by the populate, and a read
  returns an L1 hit without consulting L2.
- `TieredCacheLog.L1DropFailed`, so the drop-on-every-outcome path logs a message
  that does not claim L2 succeeded.

## 0.1.6 - 2026-08-06
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
## 0.1.5 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
- Dependency update: DcsvIo.D2.Result bumped.
## 0.1.4 - 2026-07-26
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
- Dependency update: DcsvIo.D2.Result bumped.
## 0.1.3 - 2026-07-22
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
- Dependency update: DcsvIo.D2.Result bumped.
## 0.1.2 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
- Dependency update: DcsvIo.D2.Result bumped.
## 0.1.1 - 2026-07-17

### Changed

- Package release 0.1.1 (dependency cascade / baseline reseed; no public API surface change in this slot).
