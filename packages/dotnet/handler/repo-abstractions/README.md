<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Handler.Repo.Abstractions

Types for repo handlers: the types that application code uses to tell database failures apart (unique violation, FK
violation, deadlock, concurrency conflict, connection failure, etc.). This package contains only abstractions and has no
infrastructure dependencies. EF Core, Npgsql, and all provider-specific code are in sibling packages
(`DcsvIo.D2.Handler.Repo`, `DcsvIo.D2.Handler.Repo.Postgres`).

## Install

```bash
dotnet add package DcsvIo.D2.Handler.Repo.Abstractions
```

---

## Public API

| Type                     | Role                                                                                                                                                            |
| ------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `DbFailureKind`          | Enum - `ConcurrencyConflict` / `UniqueViolation` / `ForeignKeyViolation` / `NotNullViolation` / `CheckViolation` / `Timeout` / `Deadlock` / `ConnectionFailure` |
| `IDbExceptionClassifier` | Provider seam - `DbFailureKind? Classify(Exception)`                                                                                                            |
| `DbErrorCodes`           | String constants (`UNIQUE_VIOLATION`, `FOREIGN_KEY_VIOLATION`, etc.) used as `D2Result.ErrorCode` values                                                        |
| `D2Result` DB factories  | Static C# 14 extension factories - `D2Result.UniqueViolation()`, `.ConcurrencyConflict()`, `.DbDeadlock()`, etc. (non-generic + generic)                        |
| DB booleans              | C# 14 instance extension properties - `result.IsUniqueViolation`, `result.IsConcurrencyConflict`, `result.IsTransientDbFailure`, etc.                           |

---

## Emitting and discriminating DB failures

Handler code returns typed DB failures via the extension factories:

```csharp
return D2Result<User>.UniqueViolation(
    messages: [TK.Auth.Errors.EMAIL_ALREADY_TAKEN],
    inputErrors: [new InputError("email", "EMAIL_ALREADY_TAKEN")]);
```

Caller code tells the failures apart with the boolean properties:

```csharp
var result = await createUser.HandleAsync(input);
if (result.IsUniqueViolation)
    return Conflict(result.Messages);
if (result.IsTransientDbFailure)
    return await retryPolicy.RetryAsync(() => createUser.HandleAsync(input));
if (result.IsConcurrencyConflict)
    return await ReloadAndMergeAsync(input);
```

The `IsTransientDbFailure` predicate is a roll-up of `IsDbDeadlock || IsDbTimeout || IsDbConnectionFailure`.
`IsConcurrencyConflict` is intentionally excluded: concurrency conflicts need reload-then-merge logic, not a blind
retry.

`IsTransientDbFailure` is separate from the built-in `IsTransientRetryable` (`IsServiceUnavailable || IsRateLimited`) on
`D2Result` in the result package. They are in separate packages because not every consumer of `D2Result` uses a
database; the database roll-up is available only when the consumer references this package. A generic retry policy
that must catch both HTTP-related and database-related transient failures should check the union:
`result.IsTransientRetryable || result.IsTransientDbFailure`.

---

## Default messages

Each factory accepts an optional `messages` parameter; when omitted, the generic fallback from `TK.Common.Errors.*` is
used:

| Factory                 | Default `TKMessage`                      |
| ----------------------- | ---------------------------------------- |
| `ConcurrencyConflict()` | `TK.Common.Errors.CONCURRENCY_CONFLICT`  |
| `UniqueViolation()`     | `TK.Common.Errors.UNIQUE_VIOLATION`      |
| `ForeignKeyViolation()` | `TK.Common.Errors.FOREIGN_KEY_VIOLATION` |
| `NotNullViolation()`    | `TK.Common.Errors.NOT_NULL_VIOLATION`    |
| `CheckViolation()`      | `TK.Common.Errors.CHECK_VIOLATION`       |
| `DbTimeout()`           | `TK.Common.Errors.DB_TIMEOUT`            |
| `DbDeadlock()`          | `TK.Common.Errors.DB_DEADLOCK`           |
| `DbConnectionFailure()` | `TK.Common.Errors.DB_CONNECTION_FAILURE` |

These defaults are deliberately generic ("This value is already in use"). Handlers that know the constraint identity
should override the message, for example with `[TK.Auth.Errors.EMAIL_ALREADY_TAKEN]`, and attach an `InputError` so the
form UI can highlight the offending field.

---

## Status code mapping

| Factory                                                                          | HTTP status             |
| -------------------------------------------------------------------------------- | ----------------------- |
| `ConcurrencyConflict` / `UniqueViolation` / `ForeignKeyViolation` / `DbDeadlock` | 409 Conflict            |
| `NotNullViolation` / `CheckViolation`                                            | 400 Bad Request         |
| `DbTimeout` / `DbConnectionFailure`                                              | 503 Service Unavailable |

---

## Dependencies

- `DcsvIo.D2.Result` - base `D2Result` type that the factories extend
- `DcsvIo.D2.I18n` - `TKMessage` + `TK` codegen entry point

No external NuGet packages: no EF Core and no Npgsql.

---

## Related packages

- `DcsvIo.D2.Handler.Repo` - `BaseRepoHandler` uses the classifier interface
- `DcsvIo.D2.Handler.Repo.Postgres` - PostgreSQL classifier implementation
