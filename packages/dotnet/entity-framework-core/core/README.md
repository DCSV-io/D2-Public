<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.EntityFrameworkCore

> **Audience**: backend .NET service engineers writing EF Core migrations that need to declare indexes on
> `ComplexProperty` member columns.

Generic, VO-agnostic EF Core helpers that need no database driver. It ships
`CreateD2Index<TEntity>`, a `MigrationBuilder` extension for declaring indexes on
`ComplexProperty` member columns that works around the EF Core 10 complex-member-index
limitation, plus [`PgClassedAdvisoryLock`](#pgclassedadvisorylock) and
[`PgRowLock`](#pgrowlock).

Ships no `DbContext`, no migrations, no DI engine, and no VO-specific mapping. The host
owns all of those.

---

## Install

```bash
dotnet add package DcsvIo.D2.EntityFrameworkCore
```

## `CreateD2Index<TEntity>` - complex-member index helper

Call from a migration class's `Up()` method when you need to index a `ComplexProperty`
member column. The helper derives the `{ComplexProp}_{Member}` column name from a typed
member-selector expression and emits a raw `CreateIndexOperation`.

```csharp
// In your Up() migration method:
migrationBuilder.CreateD2Index<Person>(
    table: "Persons",
    member: u => u.HomeLocation.City,
    unique: false);
// Emits: CREATE INDEX IX_Persons_HomeLocation_City ON "Persons" ("HomeLocation_City")

// Custom name + unique:
migrationBuilder.CreateD2Index<Person>(
    table: "Persons",
    member: u => u.HomeLocation.City,
    name: "UX_Persons_HomeLocation_City",
    unique: true);
```

The helper is model-unaware (the migration context has no access to the EF model at
migration-apply time). It derives the column name purely from the expression member chain
using EF Core 10 default complex column naming. If you override complex column prefixes via
`HasColumnName` in your entity configuration, you are responsible for the index column name too - but the
D2 toolkit mapping helpers never call `HasColumnName`.

**Null and empty guards.** `ArgumentNullException` is thrown for a null `table` or null
`member` expression. `ArgumentException` is thrown for an empty or whitespace-only `table`.

---

## ⚠️ EF Core 10 limitation - indexing complex-type members

### The limitation

A `ComplexProperty` member column **cannot be indexed model-aware in EF Core 10**.

Every attempted path fails:

- Fluent `HasIndex(u => u.Vo.Member)` - throws `InvalidOperationException`: "not a valid
  member access expression"
- `HasIndex("Vo_Member")` - throws: shadow property needs a type
- `AddIndex([complexMemberProp])` via metadata + finalizing convention - **silently
  discarded** at finalization: `"index properties ... not declared on the entity type"` ->
  `IMigrationsModelDiffer` emits **zero** `CreateIndexOperation`

### Proof summary

Spike (EF Core 10.0.7, Npgsql 10.0.1, real Postgres round-trip): the fluent/metadata
paths were exercised. The model-differ produced zero `CreateIndexOperation`s for every
attempted complex-member index path. The EF team has acknowledged this as a gap in issue
#31246.

### The `CreateD2Index` workaround

Use `CreateD2Index` in the host's migration class (see the `CreateD2Index<TEntity>` section). The column-name
derivation uses the expression member chain under EF Core 10 default complex column naming
(`{ComplexProp}_{Member}`, joined with `_`).

### EF Core 11 native path

EF Core 11 (issue #31246 closed, milestone 11.0.0; PR
#38192 merged 2026-05-19) makes
`HasIndex(u => u.Vo.Member)` native inside `IEntityTypeConfiguration`. When the host
upgrades to EF Core 11, move the index declaration to the configuration class and remove
the `CreateD2Index` call from the migration.

### Value-converter and query have no limitation

Value-converted single-value VO columns (`EmailAddress`, `PhoneNumber`) can be indexed or
made unique via `HasIndex(u => u.Email).IsUnique()` in the entity configuration (no
workaround). Complex-member **queries** (`WHERE HomeLocation_City = 'London'`) also work, and
produce SQL with no joins - only the model-aware **index declaration** on a complex member is
limited.

---

## `PgClassedAdvisoryLock`

Two-integer advisory locks (`pg_advisory_lock(class, key)`) taken on the `DbContext`'s own
connection, for serializing work on the state of one row. PostgreSQL tags a two-integer lock
differently from a single-bigint one, so this keyspace never collides with the single-bigint one
`DcsvIo.D2.EntityFrameworkCore.Postgres.PgAdvisoryLock` uses. Register the class in the
advisory-locks catalog and pass the generated constant.

```csharp
// Waiting form: the lock lives on the session, so it outlives the transactions
// the work opens and the context's connection stays yours until the work returns.
await PgClassedAdvisoryLock.ExecuteLockedAsync(
    db, lockClass, accountId, ct => DoWorkAsync(ct), ct);

// Budgeted form: the lock lives on the transaction this opens, the wait is
// bounded by lock_timeout, and false means the work did not run.
var ran = await PgClassedAdvisoryLock.TryExecuteLockedAsync(
    db, lockClass, accountId, ct => DoWorkAsync(ct), TimeSpan.FromMilliseconds(250), ct);
```

The waiting form's wait is one statement, so the context's command timeout bounds it as well as the
token. A wait that reaches the command timeout throws the provider's timeout - with Npgsql, an
`NpgsqlException` whose inner exception is a `TimeoutException` - and the work does not run. When
the release fails on a live connection, for example because the work left a failed transaction
open, the helper ends that transaction and releases again, and ends the session if that fails
too, so a pooled connection never goes back to its pool holding the lock.

A budget of zero or less asks the engine to refuse rather than queue at all, and bounds only that
attempt on the lock: no `lock_timeout` is set, so a wait the work takes inside the transaction is
not bounded by the budget. A non-relational provider runs the work directly, so unit suites on the
in-memory provider are unaffected.

A `false` for a wait that reached the budget also clears the context's change tracker, including
entities tracked before the call. The rollback undid every save the work made, so a later
`SaveChangesAsync` on the same context writes none of the refused work. The same holds for
`PgRowLock.TryExecuteLockedAsync` on the branch where it opened the transaction. A fault the work
or the commit throws rolls the transaction back and clears the tracker the same way before it
reaches the caller, in this form and in both `PgRowLock` forms on the branch where they opened the
transaction. The `false` returns that involve no refused wait - this form's zero budget finding the
lock held, and `PgRowLock`'s absent row - come before the work runs and leave the tracker as it
was, and so does a fault raised before the work started.

---

## `PgRowLock`

`SELECT ... FOR UPDATE` on one row, with the table and the key column resolved from the EF model
and delimited through the provider's `ISqlGenerationHelper`. SQL has no parameter form for an
identifier, and PostgreSQL folds an unquoted one to lower case, so a mixed-case column needs the
provider's own delimiting.

```csharp
// Budgeted form: false means the work did not run - either no row carries that
// key, or a wait inside the locked region reached the budget.
var ran = await PgRowLock.TryExecuteLockedAsync<UserRow>(
    db, u => u.Id, userId, ct => DoWorkAsync(ct), TimeSpan.FromMilliseconds(250), ct);

// Waiting form: no lock_timeout is set, so the wait is bounded by the token and
// the context's command timeout, and a budget the caller's own transaction
// already carries is left alone. The work runs either way; the answer says
// whether it ran serialized.
var held = await PgRowLock.ExecuteLockedAsync<UserRow>(
    db, u => u.Id, userId, ct => DoWorkAsync(ct), ct);
```

**A row that does not exist locks nothing.** `SELECT ... FOR UPDATE` can only hold a row that is
there, so two callers locking a row that neither has created yet proceed with no lock between
them. The budgeted form reads whether its statement matched: no row means `false` and the work
does not run. The waiting form runs the work either way and returns `false` when no row was held,
so the caller can tell a serialized run from an unserialized one. The caller must establish the
row inside the same transaction, or hold `PgClassedAdvisoryLock` on the identifier, which exists
whether or not its row does.

When the context already has a transaction the helper enlists in it rather than opening a second
one, and a refused row lock aborts that transaction, so the helper throws the refusal and it
reaches the code that opened the transaction. Only the branch where the helper opened the
transaction itself turns a refusal into `false`.

Where the helper opened the transaction, a positive budget is `lock_timeout` for the rest of it, so
a `false` from the budgeted form can come from any wait inside the locked region - this row's, or one
the work took under the same bound. A budget of zero or less takes the row with `NOWAIT` and sets
no `lock_timeout`, so it refuses only the wait for this row. The engine reports both with one
SQLSTATE, so the helper does not report which resource was contended. On the enlisting branch
the helper puts the caller's previous `lock_timeout` back as soon as the row is held, so the
budget bounds the row wait alone and a `false` there means no row carries the key.

A positive wait budget is applied as `lock_timeout`, which PostgreSQL keeps for the rest of the
transaction. On the enlisting branch the helper reads the value in force before it changes it and
puts that value back once the row is held, so a caller that set its own budget keeps it for the
waits it takes afterwards. The waiting form sets no budget and therefore restores none.

**Both lock helpers send PostgreSQL statements and ship in this provider-neutral package.** Neither needs
a driver type: the SQL crosses as text on the EF context, the identifiers come from the provider's
`ISqlGenerationHelper`, and a refusal is read from `DbException.SqlState` wherever that exception
sits in the inner chain a save or a query wraps it in. The callers that take these locks are
application-layer types, which the layering gate forbids from reaching the Npgsql dependencies of
`DcsvIo.D2.EntityFrameworkCore.Postgres`.

---

## Telemetry

No telemetry surface - migration helpers run at migration-apply time with no runtime span or metric emission.

## Edge cases / gotchas

- **Model-unaware column name derivation** - `CreateD2Index` derives the column name purely from the expression member
  chain under EF Core 10 default complex column naming (`{ComplexProp}_{Member}`, joined with `_`). If the host
  overrides complex column prefixes via `HasColumnName` in entity configuration, the host must supply the correct
  `name:` override argument; the toolkit mapping helpers (`MapPersonal`, `MapStreetAddress`, etc.) never call
  `HasColumnName`, so the default derivation is always correct when using those helpers.
- **EF 11 supersedes this workaround** - EF Core 11 (issue #31246, milestone 11.0.0) makes `HasIndex(u => u.Vo.Member)`
  native. When the host upgrades, move the index declaration to the entity configuration class and remove the
  `CreateD2Index` migration call. See [EF Core 11 native path](#ef-core-11-native-path).

## Configuration

No configuration - the helper has no tunable behavior.

## Dependencies

- `Microsoft.EntityFrameworkCore.Relational` - `MigrationBuilder`, `CreateIndexOperation`,
  `OperationBuilder<CreateIndexOperation>`
- `DcsvIo.D2.Utilities` - `ThrowIfFalsey` for a combined null/empty/whitespace guard on
  required string parameters (cycle-free - Utilities does not reference EF or this lib)
