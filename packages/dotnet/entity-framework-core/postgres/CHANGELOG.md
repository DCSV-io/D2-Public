<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog - DcsvIo.D2.EntityFrameworkCore.Postgres

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## 0.4.13 - 2026-10-21
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.10. This package's own surface is unchanged.
## 0.4.12 - 2026-10-20
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.9. This package's own surface is unchanged.
## 0.4.11 - 2026-10-19
### Changed

- Code comments rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.8. This package's own surface is unchanged.
## 0.4.10 - 2026-10-18
### Changed

- README prose rewritten to the repository writing standard; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.7. This package's own surface is unchanged.
## 0.4.9 - 2026-10-17
### Changed

- Punctuation and wording in comments and documentation only; no code, message or public API changed.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.6. This package's own surface is unchanged.
## 0.4.8 - 2026-09-27
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.5. This package's own surface is unchanged.
## 0.4.7 - 2026-09-26
### Changed

- The 0.4.3 release note for the `DesignTimeDbContextFactoryBase` recipe states the documentation change without comparing repository layouts.
- The `entity-framework-core/locks-source-gen` README states that the consuming project owns the advisory-lock catalog file and wires it through `<AdditionalFiles>`. No code changed.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.4. This package's own surface is unchanged.
## 0.4.6 - 2026-09-26
### Changed

- README: the `DesignTimeDbContextFactoryBase<TContext>` section describes `CreateDbContext`, which throws when the connection-string variable is unset or blank, and the `CommandTimeoutSeconds` override with its 30-second default. No code changed.
## 0.4.5 - 2026-09-14
### Changed

- Generated LoggerMessage files omit trailing whitespace; log templates and behavior are unchanged.
### Fixed

- `AdvisoryLocksGenerator` (in `entity-framework-core/locks-source-gen`, which this package's README documents) built its known-database set from the spec's `locks` array alone, so a database declared only in `classes` was reported as an unknown `D2AdvisoryLocksDatabases` token and no source was emitted. The set is now the union of both arrays, matching what `AdvisoryLocksEmitter` already emitted.
## 0.4.4 - 2026-09-10
### Changed

- The README tables are padded to their own column widths. No code changed.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.3. This package's own surface is unchanged.
## 0.4.3 - 2026-09-08
### Changed

- The `DesignTimeDbContextFactoryBase` recipe states the project that owns the context and its migrations as the
  place to subclass it.
- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.2. This package's own surface is unchanged.
## 0.4.2 - 2026-09-06
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.1. This package's own surface is unchanged.
## 0.4.1 - 2026-09-03
### Changed

- Dependency update: `DcsvIo.D2.Utilities` bumped to 0.3.0. That release is API-breaking - `D2Env` moved to `DcsvIo.D2.EnvFiles`, which now carries the `dotenv.net` runtime package. This package's own surface is unchanged; a consumer that reached `D2Env` through this package's closure adds `DcsvIo.D2.EnvFiles` to get it back.
## 0.4.0 - 2026-09-02
### API-breaking

- `PgClassedAdvisoryLock` and `PgRowLock` are removed from this package. They
  moved to `DcsvIo.D2.EntityFrameworkCore` unchanged apart from their
  namespace, so a caller updates its `using` and its package reference. Nothing
  else in this package referenced them.

## 0.3.2 - 2026-09-02
### Changed

- Project-file comments corrected: the advisory-lock catalog emit belongs in the owning module's App project; no code
  change.
## 0.3.1 - 2026-09-02
### Changed

- `PgRowLock` restores the caller's `lock_timeout` after the row is held on the enlisting branch, so the budget no
  longer bounds the caller's later waits.
## 0.3.0 - 2026-09-01

### Added

- `PgClassedAdvisoryLock` - two-integer PostgreSQL advisory locks taken on a
  `DbContext`'s own connection. A waiting session-scoped form that outlives the
  transactions the work opens, a budgeted transaction-scoped form that bounds
  the wait with `lock_timeout` and reports whether it got the lock, and
  `DeriveKey`, the masked exclusive-or fold from a `Guid` to the second integer.
  The class belongs in the advisory-locks catalog; pass the generated constant.
- `PgRowLock.TryExecuteLockedAsync<TEntity>` - `SELECT ... FOR UPDATE` on one
  row, with the table and the key column resolved from the EF model and
  delimited through the provider's `ISqlGenerationHelper`. It enlists in the
  caller's transaction when there is one and opens its own when there is not.
  The wait budget is applied as `lock_timeout`, which PostgreSQL keeps for the
  rest of the transaction; on the enlisting branch the helper reads the value in
  force first and puts it back once the row is held, so the caller's later waits
  in its own transaction keep the budget the caller set.

## 0.2.0 - 2026-08-22

### Added

- `NodaTimeValueConverterExtensions.AddD2NodaTime()` on
  `NpgsqlDbContextOptionsBuilder`, moved here from `DcsvIo.D2.Time` unchanged.
  It turns on the Npgsql NodaTime plugin, and the plugin is PostgreSQL's, so it
  belongs with the provider rather than with the NodaTime types it maps.
  `ApplyD2NpgsqlDefaults` already called it and continues to.
- `Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime` is now a declared dependency
  of this package, because the extension calls its `UseNodaTime()`.

### Changed

- The package no longer references `DcsvIo.D2.Time`. Nothing here refers to a type
  from it: the moved extension maps NodaTime's own types through the Npgsql
  plugin. A consumer that needs `IClock` or the temporal storage records
  references `DcsvIo.D2.Time` directly.

## 0.1.8 - 2026-08-19
### Changed

- Dependency update: DcsvIo.D2.Time bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.7 - 2026-08-09
### Changed

- Dependency update: DcsvIo.D2.Time bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.6 - 2026-08-06
### Changed

- Dependency update: DcsvIo.D2.Time bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.5 - 2026-08-04
### Changed

- Dependency update: DcsvIo.D2.Time bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.4 - 2026-07-26
### Changed

- NuGet `<Description>` and csproj comment punctuation normalized to keyboard ASCII (em dash, bullet and arrow glyphs replaced by `-` and `->`). Published metadata text only: no dependency, target framework or build setting changed.
- Dependency update: DcsvIo.D2.Time bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.3 - 2026-07-22
### Changed

- Dependency update: DcsvIo.D2.Time bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
## 0.1.2 - 2026-07-21
### Changed

- README: Telemetry table for AdvisoryLockMigrator LoggerMessage EventIds 1001-1008; portable host wording (no
  product-specific topology).
- Mechanism docs: domain lock catalogs remain host-supplied; no product DB ids in package prose.

## 0.1.1 - 2026-07-17
### Changed

- Dependency update: DcsvIo.D2.Time bumped.
- Dependency update: DcsvIo.D2.Utilities bumped.
