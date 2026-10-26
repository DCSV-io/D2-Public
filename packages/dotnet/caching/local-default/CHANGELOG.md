<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Caching.Local.Default

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.4.16 - 2026-10-21
### Changed

- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.12. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.10. This package's own surface is unchanged.
## 0.4.15 - 2026-10-20
### Changed

- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.11. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.4.14 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.10. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.4.13 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.9. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.4.12 - 2026-10-17
### Changed

- Punctuation in comments and documentation only.
- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.8. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Abstractions` bumped to 0.1.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.I18n.Keys` bumped to 0.3.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.5. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.4.11 - 2026-09-27
### Changed

- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.7. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.4.10 - 2026-09-26
### Changed

- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.6. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.4.9 - 2026-09-14
### Fixed

- Conditional set and removal match a present null against an expected null without matching an absent key.
- Token-bucket clock rollback preserves the accrual timestamp and remainder, preventing duplicate refill on recovery.
## 0.4.8 - 2026-09-11
### Changed

- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.5. This package's own surface is unchanged.
## 0.4.7 - 2026-09-10
### Changed

- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
## 0.4.6 - 2026-09-08
### Changed

- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.3. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.1. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
## 0.4.5 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.0. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.1. This package's own surface is unchanged.
## 0.4.4 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.0. That release is API-breaking - `D2Env` moved to `DcsvIo.D2.EnvFiles`, which now carries the `dotenv.net` runtime package. This package's own surface is unchanged; a consumer that reached `D2Env` through this package's closure adds `DcsvIo.D2.EnvFiles` to get it back.
- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.1. This package's own surface is unchanged.
## 0.4.3 - 2026-09-03
### Changed

- Depends on `DcsvIo.D2.Caching.Abstractions` 0.5.0, which now takes `DcsvIo.D2.Utilities` for its `Falsey()` key check, so that package enters this one's dependency closure.
## 0.4.2 - 2026-09-02
### Fixed

- `ConsumeTokenAsync` reads the clock inside the critical section it then
  updates under. Two callers reading it before the lock could enter in the
  other order, and the one holding the earlier instant stored it after the one
  holding the later one; the next arrival measured its elapsed span from that
  earlier instant and accrued part of the run's time a second time. A bucket
  under contention admitted more than its rate, and by more the busier the key
  was.
- `ConsumeTokenAsync` rejects a refill interval that is not a whole number of
  milliseconds, through the shared argument checks in
  `DcsvIo.D2.Caching.Abstractions`.

## 0.4.1 - 2026-09-02
### Fixed

- `ConsumeTokenAsync` keeps the accrual a refill could not spend on a whole
  thousandth of a token, in a third stored value, so a bucket taking arrivals
  more often than one thousandth accrues still refills at the configured rate.
  The stored instant used to advance to now on every call while the floored
  remainder was dropped, which reduced the refill by the arrival rate and, at a
  high enough rate on one key, stopped it entirely.
- The `ConsumeTokenAsync` critical section allocates nothing: the state written
  back is built before the lock is taken, and the conflict result for a key
  holding something else is built after it is released.

## 0.4.0 - 2026-09-02
### Added

- `ConsumeTokenAsync` - the token-bucket primitive under the same per-cache
  lock the other read-modify-write members take, measured on the injected
  `TimeProvider`. A key holding something else returns `Conflict`, as an
  increment on a non-numeric key does.

## 0.3.0 - 2026-09-02
### Added

- `RemoveIfEqualAsync<T>` - compare-and-delete under the same per-cache lock
  `SetIfEqualAsync` takes, so the comparison and the removal cannot be split by
  another writer. An absent key and a stored value of another type both return
  `Ok(false)` and remove nothing. The removal drops the entry's recorded
  lifetime with it.

## 0.2.3 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.2.2 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
## 0.2.1 - 2026-08-17
### Changed

- Documented `SetIfEqualAsync` in the README, including that it compares with
  `EqualityComparer<T>.Default` under the same per-cache lock as its siblings.
- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
## 0.2.0 - 2026-08-17
### Added

- `SetIfEqualAsync`, comparing the stored object under the same per-cache lock
  `SetNxAsync` and `IncrementAsync` take.

## 0.1.6 - 2026-08-09
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.5 - 2026-08-06
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.4 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Keys bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.3 - 2026-07-26
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Keys bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.2 - 2026-07-22
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Keys bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
