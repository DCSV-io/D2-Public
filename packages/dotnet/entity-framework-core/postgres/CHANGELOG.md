# Changelog — DcsvIo.D2.EntityFrameworkCore.Postgres

All notable changes to this package are documented here. The format follows
Keep a Changelog, and this package adheres to Semantic Versioning.

## [Unreleased]

### Wire-breaking

### API-breaking

- **Removed** domain lock-key catalog (`AdvisoryLocks` / `AdvisoryLocks.D2Keycustodian.*`)
  from this package. Shared Postgres owns mechanism only (`PgAdvisoryLock`,
  `AdvisoryLockMigrator<TContext>`, design-time factory base, `NpgsqlContextDefaults`).
  Consumers must take domain constants from the owning host/module assembly
  (generated `AdvisoryLocks.<Domain>.{MIGRATOR,ROTATION,…}` members).
  Central fleet catalog SoT remains `contracts/advisory-locks/`;
  `DcsvIo.D2.AdvisoryLocks.SourceGen` emits into the owning-module assembly.
- Nested advisory-lock class names match the owning PostgreSQL database name
  (canonical `d2-{domain}` naming). Spec + PublicAPI updated in lockstep.

### Added

### Fixed
