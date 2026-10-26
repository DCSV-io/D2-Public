<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Handler.Repo.Postgres

PostgreSQL implementation of `IDbExceptionClassifier` from `DcsvIo.D2.Handler.Repo.Abstractions`. `BaseRepoHandler` uses
it through DI, and a composition root calls `services.AddD2Postgres()` once.

Provider-specific code is behind `IDbExceptionClassifier`. An implementation for another provider (where one exists)
has the same shape: one `IDbExceptionClassifier` implementation and one `services.AddD2X()` extension. `BaseRepoHandler`
itself stays provider-agnostic.

## Install

```bash
dotnet add package DcsvIo.D2.Handler.Repo.Postgres
```

---

## Public API

| Type                            | Role                                                                                                     |
| ------------------------------- | -------------------------------------------------------------------------------------------------------- |
| `PostgresDbExceptionClassifier` | Maps PostgreSQL / EF exceptions -> `DbFailureKind`                                                       |
| `PgErrorCodes`                  | SQLSTATE string constants + `TryGetPgException(Exception)` unwrap helper                                 |
| `AddD2Postgres`                 | DI extension - registers `PostgresDbExceptionClassifier` as `IDbExceptionClassifier` (`TryAddSingleton`) |

---

## SQLSTATE -> DbFailureKind matrix

| SQLSTATE | Name                               | `DbFailureKind`       |
| -------- | ---------------------------------- | --------------------- |
| `23505`  | unique_violation                   | `UniqueViolation`     |
| `23503`  | foreign_key_violation              | `ForeignKeyViolation` |
| `23502`  | not_null_violation                 | `NotNullViolation`    |
| `23514`  | check_violation                    | `CheckViolation`      |
| `40001`  | serialization_failure              | `Deadlock`            |
| `40P01`  | deadlock_detected                  | `Deadlock`            |
| `57014`  | query_canceled (statement_timeout) | `Timeout`             |
| `57P03`  | cannot_connect_now                 | `ConnectionFailure`   |
| `53300`  | too_many_connections               | `ConnectionFailure`   |
| `08***`  | connection_exception class         | `ConnectionFailure`   |

`DbUpdateConcurrencyException` (BCL-typed) is handled directly by `BaseRepoHandler` and never reaches the classifier.

Codes follow the PostgreSQL SQLSTATE / error-code appendix.

---

## Exception shapes handled uniformly

```csharp
public DbFailureKind? Classify(Exception exception)
```

The classifier accepts any `Exception` and handles these shapes:

1. `DbUpdateException` wrapping a `PostgresException`: the EF save-pipeline path. The unwrap helper finds the inner
   `PostgresException` and dispatches on its `SqlState`.
2. A raw `PostgresException`: thrown directly during a read, query or DDL statement when EF does not wrap it. Same
   `SqlState` switch.
3. A network-level failure: `SocketException` / `IOException` anywhere in the inner-exception chain (connection
   refused, socket reset, host unreachable, transport-stream fault). Returns `ConnectionFailure`. The walk descends up
   to 10 inner-exception levels so `AggregateException` / `TargetInvocationException` / EF wrappers do not hide the
   underlying network error.

Bare `NpgsqlException` instances with no recognizable inner cause (bad connection-string parse, SSL handshake failure,
concurrent-connection misuse, internal Npgsql state errors) return `null`, and `BaseRepoHandler` then preserves the
original `UnhandledException` result. These are programmer or configuration failures and should not be silently treated
as transient and retried.

Precedence: when shapes 1 or 2 and shape 3 both apply (for example a `PostgresException` whose inner exception chain
also contains a `SocketException`), pass 1 (SQLSTATE classification) wins. Pass 2 (network detection) runs only when
pass 1 yields `null`: either the `PostgresException` had no recognized SQLSTATE, the SQLSTATE was `null`, or no
`PostgresException` was found at all. Tests assert this ordering.

The classifier does not classify client-side `NpgsqlCommand.CommandTimeout` expirations: Npgsql surfaces them as
`OperationCanceledException`, which `BaseHandler.RunCorePipelineAsync` already handles before `BaseRepoHandler` sees the
captured exception. Server-side `statement_timeout` cancellations do reach this classifier, as a `PostgresException`
with SQLSTATE `57014` (pass 1) -> `DbFailureKind.Timeout`.

---

## DI registration

```csharp
// Composition root
services.AddD2Handler();
services.AddD2Postgres();           // <-> registers PostgresDbExceptionClassifier
services.AddDbContext<AppDbContext>(o => o.UseNpgsql(...));
```

`AddD2Postgres()` uses `TryAddSingleton`, so calling it multiple times is safe (it adds no duplicate registration). To
override the default classifier with a custom implementation, register the custom implementation before the call:

```csharp
services.AddSingleton<IDbExceptionClassifier, MyCustomClassifier>();
services.AddD2Postgres();   // no-op - TryAdd sees an existing registration
```

If a service needs more than one classifier (for example one connection to Postgres and one to an embedded SQLite for
tests), do not register two `IDbExceptionClassifier`s as the unkeyed service. DI resolves the last-registered
implementation, so the active implementation depends on call order and the other stays in the container as an unused
singleton. Use keyed registration and keyed resolution instead:

```csharp
services.AddKeyedSingleton<IDbExceptionClassifier, PostgresDbExceptionClassifier>("primary");
services.AddKeyedSingleton<IDbExceptionClassifier, SqliteDbExceptionClassifier>("scratch");

// Inject via [FromKeyedServices("primary")] IDbExceptionClassifier classifier
```

Keyed services are available from .NET 8, so the API is available in this codebase, which targets .NET 10.

---

## Dependencies

- `DcsvIo.D2.Handler.Repo.Abstractions` - `IDbExceptionClassifier` + `DbFailureKind`
- `Microsoft.EntityFrameworkCore` - `DbUpdateException` type
- `Npgsql` - `PostgresException` + `NpgsqlException` types
- `Microsoft.Extensions.DependencyInjection.Abstractions` - `IServiceCollection`

---

## Related packages

- `DcsvIo.D2.Handler.Repo.Abstractions` - interface + `DbFailureKind` enum + extension factories
- `DcsvIo.D2.Handler.Repo` - `BaseRepoHandler` uses the classifier
