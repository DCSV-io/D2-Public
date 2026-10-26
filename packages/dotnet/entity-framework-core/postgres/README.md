<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.EntityFrameworkCore.Postgres

> **Audience**: backend .NET service engineers wiring a PostgreSQL-backed EF Core
> DbContext with advisory-lock-guarded migrations and startup validation.

PostgreSQL-specific EF Core startup machinery for .NET hosts that use Npgsql.
Each host that owns a per-domain database registers
`AdvisoryLockMigrator<TContext>` to safely bootstrap migrations across multiple
replicas, and calls `ApplyD2NpgsqlDefaults` from both the DI registration and the
design-time factory so the runtime and design-time paths cannot differ.

**Mechanism only.** Domain advisory-lock key catalogs do **not** ship from this
package. Callers pass domain-owned generated constants (e.g.
`AdvisoryLocks.SampleDomain.MIGRATOR` from the owning host/module assembly) into
`PgAdvisoryLock` / `AdvisoryLockMigrator`. Catalogs are host-supplied
(`advisory-locks.spec.json` AdditionalFiles); `DcsvIo.D2.AdvisoryLocks.SourceGen`
emits into the owning-module assembly via MSBuild emit props.

---

## Install

```bash
dotnet add package DcsvIo.D2.EntityFrameworkCore.Postgres
```

## `PgAdvisoryLock`

Session-scoped PostgreSQL advisory lock helper. Opens a **dedicated**
`NpgsqlConnection`; each acquired lock scope manages the lifecycle of its own connection.

```csharp
// Try-acquire (non-blocking - skip if held):
// Pass a domain-owned generated constant from the owning host/module assembly.
await using var rotLock = await PgAdvisoryLock.TryAcquireSessionAsync(
    connStr, migratorLockKey /* e.g. AdvisoryLocks.MyService.ROTATION */, ct);
if (!rotLock.IsHeld)
    return; // another instance is rotating - skip this tick

// Blocking acquire (migrator):
await using var migLock = await PgAdvisoryLock.AcquireSessionBlockingAsync(
    connStr, migratorLockKey /* e.g. AdvisoryLocks.MyService.MIGRATOR */, ct);
// migLock.IsHeld is always true after this returns
```

`PgAdvisoryLock` is `[MustDisposeResource]` - always use `await using`. `DisposeAsync`
sends an explicit `pg_advisory_unlock` and closes the connection.

**No `EnableRetryOnFailure`**: an execution-strategy reconnect silently drops a session
lock. Services using advisory locks must handle transient failures at the application
level.

---

## `PgClassedAdvisoryLock` and `PgRowLock`

Both types ship in `DcsvIo.D2.EntityFrameworkCore`, the provider-neutral package, and are
documented in its README. Their statements are PostgreSQL; neither needs a driver type, and
the application-layer callers that take these locks may not reference the Npgsql dependencies of
this package. Their namespace is `DcsvIo.D2.EntityFrameworkCore`, and a project references that
package to use them.

---

## `AdvisoryLockMigrator<TContext>`

`IHostedService` that runs at host startup: ensure-database -> blocking lock -> migrate
-> release.

Register it before any hosted services that require the schema:

```csharp
services.AddSingleton<AdvisoryLockMigrator<MyDbContext>>(sp =>
    new AdvisoryLockMigrator<MyDbContext>(
        sp.GetRequiredService<IServiceScopeFactory>(),
        connectionString,
        migratorLockKey, // domain-owned generated AdvisoryLocks.{Db}.MIGRATOR
        sp.GetRequiredService<ILogger<AdvisoryLockMigrator<MyDbContext>>>()));
services.AddHostedService<AdvisoryLockMigrator<MyDbContext>>();
```

The migrator is **fail-fast**: a bad migration throws, crash-looping the host so the
problem shows immediately instead of the host starting with a corrupt schema.

---

## `DesignTimeDbContextFactoryBase<TContext>`

Abstract base for EF Core design-time factories in module-within-host services (no
`Sdk.Web` startup project for `dotnet ef`). Subclass in the project that owns the context and its migrations:

```csharp
public sealed class MyDbContextFactory
    : DesignTimeDbContextFactoryBase<MyDbContext>
{
    protected override string ConnectionStringEnvVar => "MY_DATABASE_URL";
    protected override string MigrationsAssemblyName =>
        typeof(MyDbContextFactory).Assembly.GetName().Name!;
    protected override MyDbContext CreateContext(DbContextOptions<MyDbContext> opts)
        => new(opts);
}
```

Set `MY_DATABASE_URL` before running `dotnet ef migrations add <Name>`.

`dotnet ef` calls `CreateDbContext(args)`, which reads the variable that `ConnectionStringEnvVar`
specifies, throws `InvalidOperationException` when it is unset or blank, and passes the connection
string, `CommandTimeoutSeconds` and `MigrationsAssemblyName` to `ApplyD2NpgsqlDefaults` before
handing the options to `CreateContext`. Override `CommandTimeoutSeconds` to change the command
timeout from its default of 30 seconds.

---

## `NpgsqlContextDefaults.ApplyD2NpgsqlDefaults`

The `DbContextOptionsBuilder` extension that applies `UseNpgsql` with
`AddD2NodaTime()`, `CommandTimeout`, and `MigrationsAssembly`. Call from BOTH the
runtime DI lambda AND the design-time factory so neither path can differ from the other.

`EnableRetryOnFailure` is absent on purpose - see [`PgAdvisoryLock`](#pgadvisorylock).

---

## `NodaTimeValueConverterExtensions.AddD2NodaTime`

`NpgsqlDbContextOptionsBuilder` extension that turns on the Npgsql NodaTime
plugin, which maps `Instant` to `timestamptz`, `LocalDateTime` to `timestamp`,
`LocalDate` to `date` and the rest of the suite. Call it inside the `UseNpgsql`
configuration lambda; `ApplyD2NpgsqlDefaults` already does.

```csharp
services.AddDbContext<MyDbContext>(opts =>
    opts.UseNpgsql(connectionString, npgsql => npgsql.AddD2NodaTime()));
```

Idempotent - calling it twice on the same builder is safe. It lives here rather
than beside the NodaTime types themselves because the plugin it turns on is
PostgreSQL's, and a consumer of those types on another provider has no use for
it.

---

## Configuration

No configuration of its own. Consumers supply:
- `connectionString` - the Npgsql connection string (e.g. from `MY_DATABASE_URL`).
- `commandTimeoutSeconds` - per-command timeout (seconds).
- `migrationsAssemblyName` - assembly holding the EF Core migrations.
- `migratorLockKey` - advisory lock bigint key for the migrator (domain-owned
  generated `AdvisoryLocks.*` constant from the owning module assembly).

---

## Telemetry

`AdvisoryLockMigrator<TContext>` uses `[LoggerMessage]` (no `Exception` params):

| EventId | Name                    | Level       | Message shape                                            |
| ------- | ----------------------- | ----------- | -------------------------------------------------------- |
| 1001    | `Starting`              | Information | AdvisoryLockMigrator starting for context {ContextName}. |
| 1002    | `LockAcquired`          | Debug       | Migration advisory lock {LockKey} acquired.              |
| 1003    | `NoPendingMigrations`   | Information | No pending migrations for {ContextName}.                 |
| 1004    | `ApplyingMigrations`    | Information | Applying pending migrations for {ContextName}.           |
| 1005    | `MigrationsApplied`     | Information | Migrations applied for {ContextName}.                    |
| 1006    | `CreatingDatabase`      | Information | Creating database '{DatabaseName}'.                      |
| 1007    | `DatabaseCreated`       | Information | Database '{DatabaseName}' created.                       |
| 1008    | `DatabaseAlreadyExists` | Debug       | Database '{DatabaseName}' already exists.                |

`PgAdvisoryLock` and `ApplyD2NpgsqlDefaults` emit no package-owned telemetry.

## Dependencies

- `Microsoft.EntityFrameworkCore` - `DbContext`, `Database.MigrateAsync`, design-time
  `IDesignTimeDbContextFactory`.
- `Npgsql.EntityFrameworkCore.PostgreSQL` - `UseNpgsql`, `NpgsqlDbContextOptionsBuilder`.
- `Npgsql` - raw `NpgsqlConnection` for advisory locks and ensure-db maintenance.
- `Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime` - the `UseNodaTime()` plugin `AddD2NodaTime()` calls.
- `DcsvIo.D2.Utilities` - `ThrowIfFalsey` / `Falsey` guards.
- `JetBrains.Annotations` - `[MustDisposeResource]` on `PgAdvisoryLock`.
