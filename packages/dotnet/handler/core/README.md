<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Handler

`BaseHandler<TSelf, TInput, TOutput>` is the abstract base that every handler inherits (CQRS handlers, repo handlers,
messaging consumers, scheduled jobs, anything handler-shaped). It provides a per-handler scope pre-check, an
OpenTelemetry activity, handler metrics, a log scope, a stopwatch and a try/catch that wraps the subclass's
`ExecuteAsync` and catches every exception. The sibling package `DcsvIo.D2.Handler.Repo` adds a mapping of EF and
PostgreSQL exceptions to `D2Result`.

Auth middleware (HTTP, gRPC, AMQP) validates JWT signature, expiry, audience and fingerprint binding at the transport
level, before the handler runs. When a handler executes, the host service has already validated the bearer token.

## Install

```bash
dotnet add package DcsvIo.D2.Handler
```

---

## PII safety: setup required before deploying any handler

`BaseHandler` logs handler input via Serilog destructuring (`{@Input}`) at `Debug` level, gated by
`HandlerOptions.LogInput` (default `true`). Without redaction wiring, any field on the input that contains PII (email,
phone, name, address, raw IP, etc.) appears verbatim in logs whenever Debug-level logging is enabled.

Every consuming service must register the `[RedactData]`-aware Serilog destructuring policy at startup. Hosts compose
this themselves with `DcsvIo.D2.Logging` and Serilog wiring; `BaseHandler` does not require a host-wide aggregator. If
you bootstrap logging manually, register the policy yourself.

If you cannot guarantee that the redaction policy is in place (for example in a one-off tool or an early bootstrap
path), set `LogInput = false` in `DefaultOptions` for every handler that touches PII. Prefer registering the redaction
policy once at host startup over relying on per-handler `LogInput = false`.

---

## Public API

| Type                                  | Role                                                                                                                                                                         |
| ------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `BaseHandler<TSelf, TInput, TOutput>` | Abstract base class. Virtual `HandleAsync` (entry point), non-virtual `RunCorePipelineAsync` (observability and try/catch) and abstract `ExecuteAsync` (subclass logic)      |
| `HandlerContext<T>`                   | Typed-logger context. Open-generic registration via `AddD2Handler`                                                                                                           |
| `HandlerTelemetry`                    | Static OpenTelemetry primitives: an `ActivitySource`, a `Meter` and instruments (counters `d2.handler.invoked` / `succeeded` / `failed` and histogram `d2.handler.duration`) |
| `AddD2Handler`                        | DI extension that registers the open-generic `HandlerContext<T>`                                                                                                             |

A null handler `input` returns `D2Result.ValidationFailed` before the scope checks, `LogInput` and `ExecuteAsync`, and
does not throw `ArgumentNullException`. `TInput : notnull` states the same rule in the type system.

---

## BaseHandler shape

```csharp
public abstract class BaseHandler<TSelf, TInput, TOutput> : IHandler<TInput, TOutput>
    where TSelf : BaseHandler<TSelf, TInput, TOutput>
    where TInput : notnull
{
    protected BaseHandler(HandlerContext<TSelf> context);

    protected IHandlerContext Context { get; }
    protected virtual HandlerOptions DefaultOptions { get; }

    public virtual ValueTask<D2Result<TOutput?>> HandleAsync(
        TInput input, CancellationToken ct = default, HandlerOptions? options = null);

    protected ValueTask<(D2Result<TOutput?> Result, Exception? CapturedException)> RunCorePipelineAsync(
        TInput input, CancellationToken ct, HandlerOptions? options);

    protected abstract ValueTask<D2Result<TOutput?>> ExecuteAsync(TInput input, CancellationToken ct);
}
```

`TInput : notnull` matches `IHandler`: optional or absent values are members of the input type, never a nullable
whole-input type argument.

`RunCorePipelineAsync` returns the captured exception alongside the result so subclasses for EF Core (`BaseRepoHandler`)
can remap typed exceptions to `D2Result` failure codes from their own overridden `HandleAsync`.

---

## Host requirement

`HandleAsync` awaits without `ConfigureAwait(false)`, so the package is supported only in a host with no
`SynchronizationContext` - which is what an ASP.NET Core host runs under. In a host that installs one, every
continuation marshals back to it and a caller that blocks on `HandleAsync` deadlocks.

## HandleAsync flow

1. Resolve options: per-call -> `DefaultOptions` -> platform defaults
2. Null input gate: `input is null` -> increments `HandlerTelemetry.SR_Invoked` + `SR_Failed`, logs at
   `Warning` (event id 8), then returns `D2Result.ValidationFailed` (before scope, LogInput, `ExecuteAsync`)
3. Scope pre-check (Require): if `ScopeRequirement` is non-null with a non-empty `Scopes` set, checks
   `Context.Request.Scopes` using the declared `HandlerScopeMatch` - `Any` (at least one overlap) or `All` (every scope
   present); skip when `ScopeRequirement` is null or `Scopes` is empty; mismatch -> increments
   `HandlerTelemetry.SR_Invoked` + `SR_Failed`, skips activity span and duration recording, returns `D2Result.Forbidden`
4. Composition elevate (Elevate): a non-empty `ElevateScopes` requires a non-empty `ScopeRequirement`; the pairing is
   refused with `D2Result.Forbidden` ahead of the scope check when the requirement is null or has no scopes, because
   elevation without one would grant the elevated scopes to whatever caller reached the handler. Otherwise the ambient
   request is cast to `MutableRequestContext` (refused with `D2Result.Forbidden`, failing closed, when it is not
   mutable) and the scopes are pushed through the internal `ScopeElevation.Push` lease for the Execute window.
   Null/empty `ElevateScopes` is a no-op. Nested child handlers see the elevated ambient set for Require. Both refusals
   are handler declaration or composition faults and not caller denials, so each logs at `Error` (event ids 9 and 10)
   with the handler name; an ordinary scope denial at step 3 logs neither.
5. Activity start: `HandlerTelemetry.SR_ActivitySource.StartActivity(handlerName)`. Tags emitted (when present):
   - Always: `d2.handler.name`
   - When user identity present: `d2.user_id`, `d2.org_id`, `d2.org_type`, `d2.org_role`
   - When impersonating: `d2.impersonating`, `d2.impersonation_kind`, `d2.impersonator_id`, `d2.impersonator_org_id`,
     `d2.impersonator_org_type`, `d2.impersonator_org_role`
6. Counter: `HandlerTelemetry.SR_Invoked.Add(1, handlerNameTag)`
7. `ExecuteAsync` inside try/catch (under elevate lease when set)
8. Stopwatch + threshold logging: warn at `SlowThreshold`, error at `CriticalThreshold`
9. Result handling:
   - Success -> `Succeeded` counter + debug log
   - Failure -> `Failed` counter + debug log
   - `OperationCanceledException` with the `ct` passed to `HandleAsync` canceled -> `D2Result.Canceled` + info log
     (intentional caller cancellation)
   - `OperationCanceledException` without that `ct` canceled -> `D2Result.ServiceUnavailable` + warn log (downstream
     timeout - HttpClient timeout, SQL command timeout, internal handler watchdog)
   - Other exception -> `D2Result.UnhandledException` + error log + activity status set
10. Duration: `HandlerTelemetry.SR_Duration.Record(elapsedMs, handlerNameTag)` always
11. Elevate restore: lease dispose removes exactly the scopes that lease added, so overlapping
    leases over one request combine - a scope stays in force until the last lease that added it is disposed,
    and a scope the ambient set already contained is never removed

The pipeline injects `TraceId` automatically into every emitted `D2Result` from `Context.Request.TraceId`.

### Require vs Elevate vs Strip vs S2S

| Concept | Meaning                                                                                                           |
| ------- | ----------------------------------------------------------------------------------------------------------------- |
| Require | Who may enter this op (`ScopeRequirement` vs ambient Scopes)                                                      |
| Elevate | Composition scopes for a lease window only, declared as `HandlerOptions.ElevateScopes` and applied by BaseHandler |
| Strip   | Untrusted external HTTP JWT/session presentation never retains `internal.*` ambient scopes                        |
| S2S     | Workload token on gRPC+mTLS may include allow-listed `internal.*`; not a substitute for in-process elevate        |

`ScopeElevation` is internal to this assembly and is not part of the published surface.
`HandlerOptions.ElevateScopes` is the whole elevation contract a consumer of this package has: declare the scopes on the
handler and nest plain `HandleAsync`. No PublicAPI entry exposes `Push`, so its shape may change in any release without
a major bump. A consumer that needs elevation outside a handler should ask for a public seam.

One recorded exception calls `Push` from outside this assembly. The project file grants `InternalsVisibleTo` to a
private host assembly and its test assembly for scope elevation in one middleware call, which establishes an anonymous
session before a handler exists to declare `ElevateScopes`. The assemblies are not strong-named, so the grant is not a
security boundary: any assembly can take that name. The grant adds no published surface and no compatibility promise
for `Push`.

### Why distinguish caller-canceled from downstream-timeout

A downstream timeout, such as `HttpClient.Timeout` firing, surfaces as an `OperationCanceledException` whose token is
the timeout's internal token and not the `ct` passed to `HandleAsync`. `UnhandledException` (HTTP 500) would imply a bug
in the handler's own code. `ServiceUnavailable` (HTTP 503) correctly signals that a dependency the handler needed is not
responding, which is the right HTTP semantic and the right operational signal.

---

## DI registration

```csharp
services.AddD2Handler();   // registers open-generic HandlerContext<T> as Transient

// Per-handler registration (typically in the app layer AddXxxApp extension):
services.AddTransient<IGetUserById, GetUserById>();
```

`AddD2Handler` does not register `IRequestContext`, which is transport-specific. Each consuming transport stack must
construct the per-request `IRequestContext` and put it into the DI scope before any handler resolves:

- HTTP / gRPC.AspNetCore: the consuming service's startup wires HTTP middleware that builds `IRequestContext` from the
  validated bearer token and the ambient request data
- RabbitMQ consumer: the consuming service's consumer pipeline builds `IRequestContext` from the AMQP frame headers and
  the decrypted body

Tests supply a `MutableRequestContext` through a test fixture builder.

---

## Telemetry instruments

| Instrument             | Type               | Unit      | Description                                                   |
| ---------------------- | ------------------ | --------- | ------------------------------------------------------------- |
| `d2.handler.invoked`   | Counter (long)     | `{calls}` | Handler invocations attempted                                 |
| `d2.handler.succeeded` | Counter (long)     | `{calls}` | Handler invocations that returned `Success == true`           |
| `d2.handler.failed`    | Counter (long)     | `{calls}` | Handler invocations that returned `Success == false` or threw |
| `d2.handler.duration`  | Histogram (double) | `ms`      | Handler invocation wall-clock duration                        |

All instruments are tagged with `d2.handler.name = typeof(TSelf).Name`. Hosts register the OpenTelemetry SDK via
`DcsvIo.D2.Telemetry` (`AddD2Telemetry`) so `MeterProvider` / `TracerProvider` capture the `DcsvIo.D2.Handler` source.

---

## Subclassing pattern

```csharp
public sealed class GetUserById(HandlerContext<GetUserById> context, IUserRepo repo)
    : BaseHandler<GetUserById, GetUserByIdInput, UserDto>, IGetUserById
{
    protected override HandlerOptions DefaultOptions => new()
    {
        ScopeRequirement = new ScopeRequirement(HandlerScopeMatch.Any, new HashSet<string>(StringComparer.Ordinal) { Scopes.Self.Read }),
        SlowThreshold = TimeSpan.FromMilliseconds(50),
    };

    protected override async ValueTask<D2Result<UserDto?>> ExecuteAsync(
        GetUserByIdInput input, CancellationToken ct)
    {
        var user = await repo.FindAsync(input.Id, ct);
        if (user is null)
        {
            return D2Result<UserDto?>.NotFound();
        }

        return D2Result<UserDto?>.Ok(user.ToDto());
    }
}
```

Handler primary-constructor parameters do not take the `r_` prefix (an exception to the standard naming convention).

---

## Dependencies

- `DcsvIo.D2.Handler.Abstractions` - `IHandler`, `HandlerOptions`, `IHandlerContext`
- `DcsvIo.D2.Context.Abstractions` - request context
- `DcsvIo.D2.Result` - result type
- `Microsoft.Extensions.DependencyInjection.Abstractions` / `Microsoft.Extensions.Logging.Abstractions`

---

## Related packages

- `DcsvIo.D2.Handler.Abstractions` - `IHandler` + `HandlerOptions` + `IHandlerContext`
- `DcsvIo.D2.Handler.Repo` - subclass that remaps EF and PostgreSQL exceptions
- `DcsvIo.D2.Telemetry` - OpenTelemetry SDK wiring that captures handler instruments

Recommended layout: one handler folder per operation (`Application/Handlers/{Commands,Queries}/<Op>/`) with
primary-constructor handlers (constructor parameters do not take the `r_` field prefix).
