<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.Caching.Distributed.Redis

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.4.16 - 2026-10-21
### Changed

- `RedisCacheInvalidationBackplane.PublishInvalidationAsync` and `PublishInvalidationManyAsync` return
  ServiceUnavailable after the backplane is disposed.
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

- Token-bucket clock rollback preserves the accrual timestamp and remainder, preventing duplicate refill on recovery.
## 0.4.8 - 2026-09-11
### Changed

- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.5. This package's own surface is unchanged.
## 0.4.7 - 2026-09-10
### Changed

- Dependency update: `DcsvIo.D2.Caching.Abstractions` bumped to 0.5.4. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Result` bumped to 0.5.2. This package's own surface is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
### Fixed

- A positive expiration under one millisecond no longer writes an entry Redis never expires. `SetIfEqualAsync`,
  `IncrementAsync` and `SetAddAsync` pass a millisecond count to their scripts, which read zero as no expiration, so
  a sub-millisecond lifetime truncated into a permanent entry in a store that persists to disk and to backups. A
  positive lifetime now floors at one millisecond, which expires at once, the way the in-process flavor treats the
  same input.
## 0.4.6 - 2026-09-08
### Changed

- Formatting only: the brace-less guards in `SetIfEqualAsync` and `RemoveIfEqualAsync` have the blank line the brace
  policy requires. No behavior change.
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
## 0.4.2 - 2026-09-03
### Changed

- Depends on the caching abstractions at the advanced version; no content change in this package.
## 0.4.1 - 2026-09-02
### Fixed

- `CONSUME_TOKEN` keeps the accrual a refill could not spend on a whole
  thousandth of a token, in a third hash field, so a bucket taking arrivals more
  often than one thousandth accrues still refills at the configured rate. The
  stored instant used to advance to now on every call while the floored
  remainder was dropped, which reduced the refill by the arrival rate and, at a
  high enough rate on one key, stopped it entirely.
- `ConsumeTokenAsync` returns `Conflict` for a key that already holds something
  other than a bucket hash. The `WRONGTYPE` the server raises used to arrive as
  a store outage, which the caller treats as a fault, not as the type
  collision the in-process flavors return.

## 0.4.0 - 2026-09-02
### Added

- `ConsumeTokenAsync` - the token-bucket primitive through the new
  `CONSUME_TOKEN` Lua script, which refills and consumes in one server-side
  step. The instant comes from the server's own `TIME`, so two nodes whose
  clocks disagree still charge one bucket consistently, and a stored instant
  later than that now refills nothing. The bucket key takes a lifetime of one
  refill from empty plus one interval.

## 0.3.0 - 2026-09-02
### Added

- `RemoveIfEqualAsync<T>` - compare-and-delete through the new
  `REMOVE_IF_EQUAL` Lua script, which compares the stored bytes against the
  re-serialized expected value and `DEL`s the key in the same evaluation. An
  absent key returns `Ok(false)`, because Lua's `GET` returns false for a
  missing key and false never equals a serialized payload.

## 0.2.3 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.2.2 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
## 0.2.1 - 2026-08-17
### Changed

- Documented the `SET_IF_EQUAL` Lua script in the README, and stopped the script list
  stating a tally of itself.
- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
## 0.2.0 - 2026-08-17
### Added

- `SetIfEqualAsync` over a new `SET_IF_EQUAL` Lua script, in the shape
  `RELEASE_LOCK_IF_OWNER` already uses with a `SET` in place of the `DEL`. The
  comparison is against the bytes this store holds, so `expected` is
  re-serialized with the same serializer that wrote them.

## 0.1.7 - 2026-08-09
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.6 - 2026-08-06
### Changed

- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.5 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Keys bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.4 - 2026-07-26
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Keys bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.3 - 2026-07-22
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.2 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.Caching.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Abstractions bumped.
- Dependency update: DcsvIo.D2.I18n.Keys bumped.
- Dependency update: DcsvIo.D2.Result bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.1 - 2026-07-17

### Changed

- Package release 0.1.1 (dependency cascade / baseline reseed; no public API surface change in this slot).
