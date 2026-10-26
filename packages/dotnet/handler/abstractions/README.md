<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Handler.Abstractions

Domain-safe slice of the handler stack: `IHandler<in TInput, TOutput>`, `IHandlerContext`, `HandlerOptions`. Domain code
references this package. The runtime types (`BaseHandler`, `HandlerContext`, `HandlerTelemetry`, `AddD2Handler`) are in
`DcsvIo.D2.Handler`.

## Install

```bash
dotnet add package DcsvIo.D2.Handler.Abstractions
```

---

## Public surface

```csharp
public interface IHandler<in TInput, TOutput>
    where TInput : notnull
{
    ValueTask<D2Result<TOutput?>> HandleAsync(
        TInput input,
        CancellationToken ct = default,
        HandlerOptions? options = null);
}

public interface IHandlerContext
{
    IRequestContext Request { get; }
    ILogger Logger { get; }
}

public sealed record HandlerOptions
{
    public bool LogInput { get; init; } = true;
    public bool LogOutput { get; init; } = true;
    public TimeSpan? SlowThreshold { get; init; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan? CriticalThreshold { get; init; } = TimeSpan.FromMilliseconds(500);
    public ScopeRequirement? ScopeRequirement { get; init; }
    /// <summary>
    /// Composition scopes elevated for the ExecuteAsync window after Require passes.
    /// Null/empty disables elevation; the lease removes what it added. A non-empty
    /// value requires a non-empty ScopeRequirement or the pipeline answers Forbidden.
    /// </summary>
    public IReadOnlySet<string>? ElevateScopes { get; init; }
}

/// <summary>Declares the per-handler scope requirement.</summary>
public sealed record ScopeRequirement(HandlerScopeMatch Match, IReadOnlySet<string> Scopes);

public enum HandlerScopeMatch { Any, All }

public enum D2HandlerScopeMode { Any, All, InfraExempt }

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class D2GeneratedHandlerScopeAttribute(D2HandlerScopeMode mode) : Attribute
{
    public D2HandlerScopeMode Mode { get; }
}
```

`TInput : notnull` means the whole input is never a nullable type argument (`string?`, `OrderInput?`). Optional or absent
values are members of the non-null input type. A null input at runtime still fails closed as `ValidationFailed` in
`BaseHandler`.

`ScopeRequirement` combines an explicit match mode with the scope set: `Match` is `HandlerScopeMatch.Any` (caller must
hold at least one of the scopes) or `HandlerScopeMatch.All` (caller must hold every scope). A `null`
`HandlerOptions.ScopeRequirement` disables the per-handler pre-check entirely. The `ScopeRequirement` constructor throws
`ArgumentException` for an empty `Scopes` set, and the pipeline guard `is { Scopes.Count: > 0 }` also skips the check
for a requirement that has no scopes. `HandlerScopeMatch` is declared in this assembly. Handlers never take a
compile-time dependency on `DcsvIo.D2.Auth.Abstractions` (layer-hygiene invariant).

`D2GeneratedHandlerScopeAttribute` marks a handler class whose scope binding is declared in a generated `Scope` partial
and not by a hand-written `DefaultOptions` override. A consumer does not write it by hand: the scope generator stamps
it, and `HandlerScopeAnalyzer` in `DcsvIo.D2.Handler.Analyzers` reads it as proof that the class bound its requirement,
so that analyzer skips a generated partial's AST. `Mode` is a `D2HandlerScopeMode`, the closed set of bindings the
generator can stamp: `Any` (the caller holds at least one declared scope), `All` (the caller holds every declared
scope), and `InfraExempt` (an infrastructure operation that declares no scope requirement at all). A consumer reads
`Mode` to see which of those bindings a generated handler took.

> JWT signature, expiry, audience and fingerprint-binding validation are not per-handler. Auth middleware (HTTP, gRPC,
> AMQP) performs them at the transport level before the handler runs. Per-handler scope requirements
> (`ScopeRequirement`) are here because they vary by operation. Audience and signature are per-service constants, and
> putting them on `HandlerOptions` would let each handler set them. See the `HandlerOptions` remarks.

---

## Why split from `DcsvIo.D2.Handler`?

Domain code (entities, value objects and domain services) should not depend on
`Microsoft.Extensions.DependencyInjection`, `OpenTelemetry`, or any infrastructure package. The split lets domain
projects reference only this package, and use `IHandler` for handler-shaped domain services. The concrete handler and
the DI extension are in the sibling package `DcsvIo.D2.Handler`.

---

## Dependencies

- `DcsvIo.D2.Result` - `D2Result<T>` return type
- `DcsvIo.D2.Context.Abstractions` - `IRequestContext` on the context
- `Microsoft.Extensions.Logging.Abstractions` - `ILogger` on the context

---

## Related packages

- `DcsvIo.D2.Handler` - concrete `BaseHandler` + `HandlerContext` + DI extension
- `DcsvIo.D2.Handler.Repo` - handler for Entity Framework Core that maps PostgreSQL and EF exceptions to `D2Result`
  failure codes

Recommended layout: one handler folder per operation (`Application/Handlers/{Commands,Queries}/<Op>/`).
