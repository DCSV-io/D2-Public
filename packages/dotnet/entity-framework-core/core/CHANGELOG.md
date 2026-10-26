<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.EntityFrameworkCore

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.4.11 - 2026-10-21
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.10. This package's own surface is unchanged.
## 0.4.10 - 2026-10-20
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.4.9 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.4.8 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.4.7 - 2026-10-17
### Changed

- The packaged license file no longer starts with a byte-order mark; the license text is unchanged.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.4.6 - 2026-09-28
### Changed

- The waiting forms, `PgClassedAdvisoryLock.ExecuteLockedAsync` and `PgRowLock.ExecuteLockedAsync`, document that the
  context's command timeout bounds the wait as well as the token, and state the exception a wait that reaches it ends
  in. The budgeted forms document that a budget of zero or less bounds only the attempt on the lock or the row, because
  no `lock_timeout` is set on that branch. The README says both.
### Fixed

- `PgClassedAdvisoryLock.ExecuteLockedAsync` no longer returns a pooled connection to its pool while the session still holds the lock. A release that failed on a live connection - a transaction the work left open and failed refuses every statement - was swallowed, and the pool keeps a session until the connection is next opened, so every waiter on that class and key blocked until then. The helper now ends the open transaction and releases again, and ends the session when the second release fails too.
- `PgClassedAdvisoryLock.TryExecuteLockedAsync` and both `PgRowLock` forms, on the branch where they opened the transaction, clear the context's change tracker when the work or the commit throws. The rollback undid what the work saved but left those entities tracked, so a caller that caught the fault and kept using the context worked from a tracker that no longer matched the database. A fault raised before the work started leaves the tracker as it was.
## 0.4.5 - 2026-09-27
### Changed

- The README's prose lines over 120 characters are re-wrapped at that width; no wording changed. No code changed.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.4.4 - 2026-09-26
### Fixed

- `PgClassedAdvisoryLock.TryExecuteLockedAsync` and `PgRowLock.TryExecuteLockedAsync` clear the context's change tracker
  when they return `false` for a wait that reached the `lock_timeout` budget. The rollback removed the refused work from
  the database but left what the work added, changed or saved tracked on the context, so a later `SaveChangesAsync` on
  the same context wrote the refused work outside the lock. Entities tracked before the call are cleared too; the
  `<returns>` text and the README say so. A `false` for a zero-budget lock found held, or for an absent row, leaves the
  tracker as it was.
## 0.4.3 - 2026-09-26
### Fixed

- `PgClassedAdvisoryLock.TryExecuteLockedAsync` and `PgRowLock.TryExecuteLockedAsync` return `false` when a wait the
  work takes reaches the `lock_timeout` budget, as their `<returns>` text states. The refusal was matched only as a bare
  `DbException`, so a row the work's `SaveChangesAsync` waited on was thrown as `DbUpdateException` and a query's wait
  was thrown wrapped in the execution strategy's `InvalidOperationException`, and both propagated as exceptions. The
  `55P03` SQLSTATE is now read wherever it sits in the exception's inner chain.
## 0.4.2 - 2026-09-26
### Changed

- The `PgClassedAdvisoryLock` remarks describe the waiting form's connection as held until the work returns. No code changed.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.4.1 - 2026-09-11
### Fixed

- `PgClassedAdvisoryLock.RollbackQuietlyAsync` rolled back on the caller's `CancellationToken`, so an already-canceled token threw `OperationCanceledException` out of the cleanup and replaced the original fault before `throw;` could rethrow it. It now rolls back on `CancellationToken.None`, the way `ReleaseQuietlyAsync` already did, and no longer takes a token parameter. `PgRowLock`'s three call sites follow.
## 0.4.0 - 2026-09-10
### API-breaking

- `PgRowLock.ExecuteLockedAsync<TEntity>` returns `Task<bool>` rather than `Task`. `true` means the row was held for the
  work, `false` means no row carries the key. The work runs either way, so a caller that does not care may ignore the
  return value, and `await` in statement position compiles unchanged. The helper already computed the fact and discarded
  it, which left a caller unable to tell a serialized run from an unserialized one - the failure a row lock is taken to
  prevent.
### Changed

- `PgRowLock.TryExecuteLockedAsync`'s `<returns>` text is split per branch. The budget bounds the whole region only
  where this helper opens the transaction; on the enlisting branch the helper puts the caller's previous `lock_timeout`
  back as soon as the row is held, so the budget bounds the row wait alone and `false` there means the absent row. The
  `<remarks>` already described the restore; the returns text now does too.
- `PgRowLock.TryExecuteLockedAsync` and `PgRowLock.ExecuteLockedAsync` document the `ArgumentException` `PropertyNameOf` throws for a key expression that is not a direct property access on the entity parameter.
- `PgRowLock` reads whether its `SELECT ... FOR UPDATE` matched a row through a private `RanAsync` helper; the name has
  the `Async` suffix its return type calls for.

## 0.3.0 - 2026-09-08
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
### Fixed

- `PgRowLock.TryExecuteLockedAsync` reads whether its `SELECT ... FOR UPDATE` matched a row. A key that matches no row
  locks nothing, so the work no longer runs and the helper returns `false`. `PgRowLock.ExecuteLockedAsync`'s contract
  says the caller establishes that the row exists, and identifies `PgClassedAdvisoryLock` as the lock that exists
  whether or not its row does; the return value of that member was added in 0.4.0.
- `PgRowLock` and `PgClassedAdvisoryLock` document that `false` can come from any wait inside the transaction that
  reached the `lock_timeout` budget, which is the lock itself or one the work took under the same bound. The engine
  reports both with one SQLSTATE.
## 0.2.3 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.1. This package's own surface is unchanged.
## 0.2.2 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.0. That release is API-breaking - `D2Env` moved to `DcsvIo.D2.EnvFiles`, which now carries the `dotenv.net` runtime package. This package's own surface is unchanged; a consumer that reached `D2Env` through this package's closure adds `DcsvIo.D2.EnvFiles` to get it back.
## 0.2.1 - 2026-09-02
### Changed

- `PgClassedAdvisoryLock.ExecuteLockedAsync` documents its type parameter as the
  value the work returns, and the README links to `PgClassedAdvisoryLock` and
  `PgRowLock` by anchor rather than by position. No behavior changed.

## 0.2.0 - 2026-09-02
### Added

- `PgClassedAdvisoryLock` and `PgRowLock` move here from
  `DcsvIo.D2.EntityFrameworkCore.Postgres`, unchanged apart from their
  namespace. Their statements are PostgreSQL and stay PostgreSQL; neither type
  needs a driver type, because the SQL crosses as text on the EF context, the
  identifiers come from the provider's `ISqlGenerationHelper`, and the refusal
  is read from `DbException.SqlState`. The callers are application-layer types
  that the layering gate forbids from reaching the Npgsql dependencies of the
  PostgreSQL package, and this package has none.
- `PgRowLock.ExecuteLockedAsync<TEntity>` - the waiting sibling of
  `TryExecuteLockedAsync<TEntity>`, for a caller whose outcome depends on
  getting the row rather than on how long it took. It sets no `lock_timeout`,
  so it neither bounds the wait nor disturbs a bound the caller's own
  transaction is already running under, and it enlists in that transaction
  where there is one.

## 0.1.8 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.7 - 2026-08-09
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.6 - 2026-08-06
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.5 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.4 - 2026-07-26
### Changed

- NuGet `<Description>` and csproj comment punctuation normalized to keyboard ASCII (em dash, bullet and arrow glyphs replaced by `-` and `->`). Published metadata text only: no dependency, target framework or build setting changed.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.3 - 2026-07-25

### Changed

- Package release 0.1.3 (dependency cascade and baseline reseed; no change to the public API surface).
## 0.1.2 - 2026-07-22
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.1 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.Utilities bumped.
