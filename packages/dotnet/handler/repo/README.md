<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Handler.Repo

`BaseRepoHandler<TSelf, TInput, TOutput>` is the Entity Framework Core subclass of `BaseHandler` from
`DcsvIo.D2.Handler`. Its `HandleAsync` override converts any database exception captured during `ExecuteAsync` into a
typed `D2Result` failure (concurrency conflict, unique violation, deadlock, connection failure, etc.), so callers can
branch on what went wrong instead of receiving a generic 500.

The handler is provider-agnostic: it catches the BCL-typed `DbUpdateConcurrencyException` directly and routes every
other exception through an injected `IDbExceptionClassifier`. Provider-specific code is in sibling packages, such as
`DcsvIo.D2.Handler.Repo.Postgres`.

## Install

```bash
dotnet add package DcsvIo.D2.Handler.Repo
```

---

## Public API

| Type                                                           | Role                                                                                                                                                                                                                                                  |
| -------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `BaseRepoHandler<TSelf, TInput, TOutput>` (`TInput : notnull`) | Abstract subclass of `BaseHandler`. The constructor takes an injected `IDbExceptionClassifier`. The `HandleAsync` override calls `RunCorePipelineAsync`, then dispatches the captured exception through the classifier to a typed `D2Result` factory. |
| `MapDbException` (virtual)                                     | Per-handler refinement hook: attach a domain-specific `TKMessage` and `InputError`, or return `null` to use the default factory.                                                                                                                      |

This package has no provider dependencies (no Npgsql). It references `DcsvIo.D2.Handler`,
`DcsvIo.D2.Handler.Abstractions`, `DcsvIo.D2.Handler.Repo.Abstractions`, `DcsvIo.D2.Result`, and
`Microsoft.EntityFrameworkCore`.

## Host requirement

`HandleAsync` awaits without `ConfigureAwait(false)`, so the package is supported only in a host with no
`SynchronizationContext` - which is what an ASP.NET Core host runs under. In a host that installs one, every
continuation marshals back to it and a caller that blocks on `HandleAsync` deadlocks.

---

## Mapping

| Captured exception                                              | Classified as                                  | Default `D2Result`                                             |
| --------------------------------------------------------------- | ---------------------------------------------- | -------------------------------------------------------------- |
| `DbUpdateConcurrencyException`                                  | `ConcurrencyConflict` (BCL - handled directly) | `D2Result.ConcurrencyConflict()`                               |
| `IDbExceptionClassifier.Classify(ex)` returns `UniqueViolation` | `UniqueViolation`                              | `D2Result.UniqueViolation()`                                   |
| Returns `ForeignKeyViolation`                                   | `ForeignKeyViolation`                          | `D2Result.ForeignKeyViolation()`                               |
| Returns `NotNullViolation`                                      | `NotNullViolation`                             | `D2Result.NotNullViolation()`                                  |
| Returns `CheckViolation`                                        | `CheckViolation`                               | `D2Result.CheckViolation()`                                    |
| Returns `Timeout`                                               | `Timeout`                                      | `D2Result.DbTimeout()`                                         |
| Returns `Deadlock`                                              | `Deadlock`                                     | `D2Result.DbDeadlock()`                                        |
| Returns `ConnectionFailure`                                     | `ConnectionFailure`                            | `D2Result.DbConnectionFailure()`                               |
| Classifier returns `null`                                       | unknown                                        | Falls through - `BaseHandler`'s `UnhandledException` preserved |

This package intentionally does not remap `OperationCanceledException`; `BaseHandler.RunCorePipelineAsync` already
handles it (returns `D2Result.Canceled` for caller-initiated cancellation, `D2Result.ServiceUnavailable` for downstream
timeouts not tied to the request token).

---

## Per-handler refinement

The default factory dispatch produces a generic message ("This value is already in use") with no field-level
information. That is enough for diagnostics and programmatic discrimination, but weak for form-driven flows.

Handlers that know their constraint identity should override `MapDbException` to attach a domain-specific `TKMessage`
and `InputError`:

```csharp
public sealed class CreateUser(
    HandlerContext<CreateUser> context,
    IDbExceptionClassifier classifier,
    IAppDbContext db)
    : BaseRepoHandler<CreateUser, CreateUserInput, UserDto>(context, classifier), ICreateUser
{
    protected override async ValueTask<D2Result<UserDto?>> ExecuteAsync(
        CreateUserInput input, CancellationToken ct)
    {
        var user = User.Create(input);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return D2Result<UserDto?>.Created(user.ToDto());
    }

    protected override D2Result<UserDto?>? MapDbException(Exception ex, DbFailureKind kind)
    {
        // The DB-side unique index `users_email_key` covers the email column.
        if (kind == DbFailureKind.UniqueViolation && IsEmailIndex(ex))
        {
            return D2Result<UserDto?>.UniqueViolation(
                messages: [TK.Auth.Errors.EMAIL_ALREADY_TAKEN],
                inputErrors: [new InputError("email", "EMAIL_ALREADY_TAKEN")]);
        }

        return null; // fall back to the generic factory
    }
}
```

Returning `null` from the override selects the default result, so a handler customizes only the cases it needs.

---

## Caller-side discrimination

Callers branch on the typed booleans from `DcsvIo.D2.Handler.Repo.Abstractions`:

```csharp
var result = await createUser.HandleAsync(input);

if (result.IsUniqueViolation)        return Conflict(result);                  // 409, surface to user
if (result.IsConcurrencyConflict)    return await ReloadAndMergeAsync(input);  // optimistic-concurrency retry
if (result.IsTransientDbFailure)     return await retry.RetryAsync(...);       // deadlock / timeout / connection
if (result.IsForeignKeyViolation)    return BadRequest(result);                // referenced item missing
```

The roll-up `IsTransientDbFailure` covers deadlock, timeout and connection failure (a caller may safely retry).
Concurrency conflicts are intentionally excluded from the roll-up: they need reload-then-merge, not a blind retry.

`IsTransientDbFailure` is separate from the built-in `IsTransientRetryable` (`IsServiceUnavailable || IsRateLimited`) on
`D2Result` from the result package. A generic retry policy that must catch both the HTTP-related and the
database-related transient sets should check the union:
`result.IsTransientRetryable || result.IsTransientDbFailure`.

---

## DI registration

`BaseRepoHandler` requires an `IDbExceptionClassifier` from DI. The composition root registers a provider-specific
implementation:

```csharp
services.AddD2Handler();
services.AddD2Postgres();   // registers PostgresDbExceptionClassifier as IDbExceptionClassifier
services.AddDbContext<AppDbContext>(o => o.UseNpgsql(...));
services.AddTransient<ICreateUser, CreateUser>();
```

Without a registered classifier, resolving any `BaseRepoHandler` subclass fails fast at the container.

---

## Dependencies

- `DcsvIo.D2.Handler` - the base class and `HandlerContext<T>`
- `DcsvIo.D2.Handler.Abstractions` - `IHandler`, `HandlerOptions`
- `DcsvIo.D2.Handler.Repo.Abstractions` - `IDbExceptionClassifier`, `DbFailureKind`, `D2Result` extension factories
- `DcsvIo.D2.Result` - base `D2Result`
- `Microsoft.EntityFrameworkCore` - `DbUpdateConcurrencyException`

No Npgsql and no other provider-specific dependencies.

---

## Related packages

- `DcsvIo.D2.Handler.Repo.Abstractions` - `IDbExceptionClassifier`, failure kinds, extension factories and booleans
- `DcsvIo.D2.Handler.Repo.Postgres` - PostgreSQL classifier impl
- `DcsvIo.D2.Handler` - base handler
