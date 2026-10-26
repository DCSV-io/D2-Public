<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# entity-framework-core cluster

> Parent: [`packages/dotnet/`](../README.md)

EF Core shared libraries, grouped by provider specificity:

| Package                                                                                 | Description                                                                                                                                                                                                                                                                                                                                                                                                                      |
| --------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [`core/`](core/README.md) - `DcsvIo.D2.EntityFrameworkCore`                             | Provider-agnostic EF Core migration helpers. Ships `CreateD2Index<TEntity>` - a `MigrationBuilder` extension for declaring indexes on `ComplexProperty` member columns - and the `PgClassedAdvisoryLock` and `PgRowLock` helpers.                                                                                                                                                                                                |
| [`postgres/`](postgres/README.md) - `DcsvIo.D2.EntityFrameworkCore.Postgres`            | PostgreSQL-specific EF Core startup mechanism: `PgAdvisoryLock`, `AdvisoryLockMigrator<TContext>`, `DesignTimeDbContextFactoryBase<TContext>`, and `NpgsqlContextDefaults.ApplyD2NpgsqlDefaults`. Does not contain domain lock-key catalogs.                                                                                                                                                                                     |
| [`locks-source-gen/`](locks-source-gen/README.md) - `DcsvIo.D2.AdvisoryLocks.SourceGen` | Roslyn `IIncrementalGenerator` that emits `AdvisoryLocks` into the owning host/module assembly. The owning module sets MSBuild props (`D2AdvisoryLocksEmitNamespace` / `D2AdvisoryLocksDatabases`) and supplies the fleet catalog as host `AdditionalFiles` (`advisory-locks.spec.json`). Enforces per-database key uniqueness at build time. The shared package owns the tooling; the owning module owns the emitted constants. |
