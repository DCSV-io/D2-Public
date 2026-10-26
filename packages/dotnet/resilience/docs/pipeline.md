<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Resilience - the pipeline surface

How [`DcsvIo.D2.Resilience`](../README.md) composes retry, circuit breaker and singleflight into one pipeline: the
builder, the order the stages run in, what each stage observes, and the options each one binds.

`ResilientPipeline<TKey, TValue>` is a configured pipeline of `IResilientLayer<TKey, TValue>` decorators that:

- composes layers in **outer-first order** (first layer wraps everything else)
- exposes one call: `ExecuteAsync(key, operation, ct)` returning `D2Result<TValue>`
- **never throws** - every terminating exception is converted to a `D2Result` per the documented mapping:

  | Exception                                                                     | Mapping                                                                 |
  | ----------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
  | `CircuitOpenException`                                                        | `ServiceUnavailable` (503)                                              |
  | `RateLimitRejectedException`                                                  | `TooManyRequests` (429 / `RATE_LIMITED`, `IsTransientRetryable = true`) |
  | `OperationCanceledException` when caller `ct` canceled                        | `Canceled`                                                              |
  | `TimeoutException` + any other transient (`RetryHelper.IsTransientException`) | `ServiceUnavailable` (503, `IsTransientRetryable = true`)               |
  | Anything else                                                                 | `UnhandledException`                                                    |

## Two-layer API: fluent at registration, one call at the call site

The intent is that **client-lib authors** configure the pipeline once at the lib's composition root using the fluent
DSL; **handlers inside the lib** see only the one-line call surface; **callers of the lib** see nothing - resilience is
invisible.

### Registration layer (lib composition root, fluent)

**All registrations are keyed.** The lib provides no unkeyed registration or resolution path because two unkeyed
registrations of the same `(TKey, TValue)` shape would silently overwrite each other (last-wins) - requiring a key
prevents that overwrite by construction. Every layer call identifies the keyed primitive it resolves.

In practice, service keys live in a per-domain constants class in the consumer's app layer (not inline strings in
registration code), so refactor-renames stay safe and `[FromKeyedServices(...)]` attributes on consumers stay in sync.
Public consts are `UPPER_CASE`:

```csharp
// app layer constants - single source of truth for every key.
namespace MyService.IpLookup;

public static class IpinfoServiceKeys
{
    public const string LOOKUP = "ipinfo";
}
```

```csharp
// Composition root for the same module - wraps the external ipinfo HTTP API.
public static IServiceCollection AddIpinfoLookup(this IServiceCollection services)
{
    services.AddKeyedSingleton<Singleflight<string, IpinfoLookupResponse>>(IpinfoServiceKeys.LOOKUP);
    services.AddKeyedSingleton<CircuitBreaker<IpinfoLookupResponse>>(
        IpinfoServiceKeys.LOOKUP, (_, _) => /* configured */);

    services.AddResilientPipeline<string, IpinfoLookupResponse>(IpinfoServiceKeys.LOOKUP, p => p
        .UseSingleflight(IpinfoServiceKeys.LOOKUP)
        .UseCircuitBreaker(IpinfoServiceKeys.LOOKUP));

    services.AddTransient<IFindWhoIsHandler, FindWhoIs>();
    return services;
}
```

### Call-site layer (handler)

The handler's constructor pulls the keyed pipeline via `[FromKeyedServices]`:

```csharp
public sealed partial class FindWhoIs(
    [FromKeyedServices(IpinfoServiceKeys.LOOKUP)] ResilientPipeline<string, IpinfoLookupResponse> pipeline,
    IIpinfoClient ipinfo) : BaseHandler<...>
{
    public override async ValueTask<D2Result<WhoIsDTO?>> ExecuteAsync(I input, CancellationToken ct)
    {
        var responseR = await pipeline.ExecuteAsync(
            $"whois:{input.IpAddress}",
            c => ipinfo.LookupAsync(input.IpAddress, c),
            ct);

        if (responseR.BubbleOnFailure<IpinfoLookupResponse, WhoIsDTO?>(out var bubbled, out var resp))
            return bubbled;

        return D2Result<WhoIsDTO?>.Ok(resp.ToDto());
    }
}
```

## Layer order **is** the protection semantic

The fluent chain order = layer order in the resulting pipeline (outer-first). Lib authors choose between
**upstream-protecting** (`UseCircuitBreaker -> UseRetries`: CB sees one execution per retry budget; opens after N failed
sequences; backoff gives the upstream time to recover - fits fragile upstreams like Resend / Twilio) or
**restart-recovery** (`UseSingleflight -> UseRetries -> UseCircuitBreaker`: each retry is a separate CB execution;
`CircuitOpenException` is treated as transient by the default classifier; retries back off through cooldown - fits
read-by-key gRPC across rolling deploys). The caller sizes `MaxAttempts + backoff` to span the breaker's
`CooldownDuration` for the restart-recovery composition; otherwise retries exhaust on `CircuitOpenException` at every
attempt. Both compositions and the other recipes are in `SCENARIOS.md`.

## Skipping layers + cross-pipeline shared primitives

Each `Use*` is independent - call zero, one, two, or three. A zero-layer pipeline still does the exception -> `D2Result`
mapping. To share a primitive across pipelines (e.g. multi-criticality audit pipelines sharing one broker-level CB so
any tier's failures count toward the same breaker state), register the shared primitive under its own key and reference
that key from each pipeline. The shared topology can be found by searching the source for the key constant.

## Order matters - the canonical layer order and the reasoning

**The recommended outer-to-inner order:**

```text
[Singleflight ->] RateLimiter -> TotalTimeout -> Retry -> CircuitBreaker -> PerAttemptTimeout
```

Singleflight is in brackets because it only applies to idempotent-by-key operations (reads, lookups, JWKS fetches).
Never use it on mutating ops.

**Why each position:**

| Layer                 | Position                                        | Why                                                                                                                                                                                                                                                                                                                          |
| --------------------- | ----------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Singleflight**      | Outermost (when used)                           | All deduplicated callers share one concurrency permit, one total-budget clock, one retry sequence. Placing it anywhere inside means N callers compete for permits and run N independent retry sequences against a breaker that may trip on one of them.                                                                      |
| **RateLimiter**       | 2nd outermost (1st when no SF)                  | Reject before spending any resources - no retry, no CB state, no timeout clock consumed. A rejected caller gets `TooManyRequests` in microseconds. Placing RL inside Retry means the system wastes retry attempts on calls that shouldn't have been made at all.                                                             |
| **TotalTimeout**      | Outside Retry                                   | Bounds the whole user-facing latency across all retry attempts. If the total budget is 30 s and the per-attempt budget is 5 s with 3 retries, the total clock keeps the caller's SLA even where every attempt takes 10 s. Placing total-timeout inside Retry bounds only a single attempt - the "total" is then meaningless. |
| **Retry**             | Outside CircuitBreaker - restart-recovery shape | Each retry is a separate CB execution. `CircuitOpenException` is treated as transient by the default classifier; retries back off through the CB cooldown and a later attempt may find the breaker probing/closed. Use this shape when callers can wait for an upstream restart (e.g. rolling deploy).                       |
| **CircuitBreaker**    | Outside Retry - upstream-protecting shape       | One full retry sequence = one CB execution. The CB opens only after N complete retry sequences fail, giving the upstream more time to recover. Use this shape for fragile third-party APIs (Resend, Twilio) where you want backoff between whole retry budgets, not just individual attempts.                                |
| **PerAttemptTimeout** | Innermost                                       | Bounds each individual attempt. A timed-out attempt surfaces as `TimeoutException` (transient), which the outer Retry layer retries. Placing per-attempt-timeout outside Retry is useless - it would bound only one attempt and then surface `ServiceUnavailable` to the caller with no retry.                               |

**The two canonical Retry<->CircuitBreaker orderings:**

```text
Retry -> CircuitBreaker  (restart-recovery)
CB opens fast; retries back off through cooldown; recovers from upstream restarts.
Use for: read-by-key gRPC, rolling-deploy resilience.
MaxAttempts + backoff must span CooldownDuration.

CircuitBreaker -> Retry  (upstream-protecting)
One full retry budget = one CB execution; CB opens only after N sequences fail.
Use for: fragile third-party writes (Resend / Twilio), outbox publishers.
```

**Anti-patterns:**

- **RL innermost** - rejects after using up retry attempts, timeout budget, and CB state.
- **Per-attempt-timeout outside Retry** - times out the entire retry loop; each individual attempt has no separate
  deadline. Equivalent to a second total-timeout, not a per-attempt one.
- **Singleflight on a mutating op** - two callers wanting to `CreateUser` share one execution; only one user is created.
  Merging distinct side-effects is a silent data-loss bug. See the table in
  [When to use Singleflight - and when not](../README.md#when-to-use-singleflight---and-when-not).

## Preferred configurations by use case

### (a) Idempotent read-by-key over gRPC/HTTP - restart-recovery shape with SF

```csharp
// Use case: request-context resolution from another service, JWKS fetch, reference-data lookups.
// Key is the entity ID / IP / etc. - many concurrent callers for the same key are common.
services.AddKeyedSingleton<Singleflight<string, T>>(key);
services.AddKeyedSingleton<CircuitBreaker<T>>(key, (_, _) => new(_ => false));
services.AddResilientPipeline<string, T>(key, p => p
    .UseSingleflight(key)
    .UseRateLimiter(new RateLimiterOptions(maxConcurrency: 20))
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(30)))           // total budget
    .UseRetries(new()
    {
        MaxAttempts = 5,
        BaseDelayMs = 500,
        MaxDelayMs = 10_000,
    })
    .UseCircuitBreaker(key)
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(5))));           // per-attempt
// Retry outside CB (restart-recovery): MaxAttempts x backoff must exceed CooldownDuration.
// With 5 attempts and 500ms base/x2: ~= 0.5 + 1 + 2 + 4 = 7.5s avg -> fits 30s default cooldown.
```

### (b) Fragile external API - upstream-protecting CB outside Retry

```csharp
// Use case: a notification service calling an email or SMS provider. Transient errors retry;
// sustained outages trip the breaker so the provider gets recovery time.
services.AddKeyedSingleton<CircuitBreaker<T>>(key, (_, _) => new(
    isFailure: _ => false,
    options: new(failureThreshold: 3, cooldownDuration: TimeSpan.FromMinutes(1))));
services.AddResilientPipeline<string, T>(key, p => p
    .UseRateLimiter(new RateLimiterOptions(maxConcurrency: 5))          // don't pile on
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(20)))            // total budget
    .UseCircuitBreaker(key)                                              // upstream-protecting
    .UseRetries(new()
    {
        MaxAttempts = 4,
        BaseDelayMs = 1000,
        BackoffMultiplier = 3.0,
        MaxDelayMs = 30_000,
    })
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(8))));           // per-attempt
// CB outside R: CB sees one execution per full retry budget. Opens slowly.
// No Singleflight - each email/SMS is a discrete delivery intent.
```

### (c) Mutating command - conservative, no Singleflight

```csharp
// Use case: Create/Update/Delete handlers calling an upstream over gRPC.
// No Singleflight - two callers wanting to CreateUser should produce two users.
// Transport-level retry is dangerous unless the upstream is idempotent (idempotency key /
// the operation is provably safe to re-issue). If uncertain: no retry layer.
// Add retry only when the upstream guarantees idempotency or the mutation is truly safe
// to re-issue (e.g. "upsert by natural key", "mark delivered" with idempotency guard).
services.AddKeyedSingleton<CircuitBreaker<T>>(key, (_, _) => new(_ => false));
services.AddResilientPipeline<string, T>(key, p => p
    .UseRateLimiter(new RateLimiterOptions(maxConcurrency: 10))
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(15)))
    .UseCircuitBreaker(key));
// No transport-level Retry - side effects must not be duplicated.
// CB still protects against a confirmed-down upstream; RL prevents stampede.
```

### (d) Internal S2S gRPC call - the canonical default full stack

```csharp
// Use case: any critical inter-service gRPC call in D2 (not idempotent-by-key,
// or SF not needed). Mirrors the strategy set of .NET's standard HTTP resilience handler (retry + circuit-breaker + rate-limiter + per-attempt timeout).
services.AddKeyedSingleton<CircuitBreaker<T>>(key, (_, _) => new(_ => false));
services.AddResilientPipeline<string, T>(key, p => p
    .UseRateLimiter(new RateLimiterOptions(maxConcurrency: 20))
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(30)))
    .UseRetries(new()
    {
        MaxAttempts = 3,
        BaseDelayMs = 500,
        MaxDelayMs = 10_000,
    })
    .UseCircuitBreaker(key)
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(8))));
// Restart-recovery without SF: each caller runs its own retry sequence.
// Add UseSingleflight(key) outermost if the call is a read-by-key hot path.
```

## Canonical layer ordering (the standard full-stack composition)

The standard outer-first composition covers the same strategy set as .NET's standard HTTP resilience handler and is
fully expressible with this lib's native layers:

```text
RateLimiter -> TotalTimeout -> Retry -> CircuitBreaker -> PerAttemptTimeout
```

Builder call:

```csharp
services.AddResilientPipeline<string, T>("key", p => p
    .UseRateLimiter()                                          // outermost: admission control
    .UseTimeout(new(TimeSpan.FromSeconds(30)))                 // total-request budget
    .UseRetries(new() { MaxAttempts = 3, BaseDelayMs = 500 })
    .UseCircuitBreaker("key")
    .UseTimeout(new(TimeSpan.FromSeconds(5))));                // per-attempt deadline
```

When Singleflight is used, place it outermost so deduped callers share one concurrency permit:

```text
Singleflight -> RateLimiter -> TotalTimeout -> Retry -> CircuitBreaker -> PerAttemptTimeout
```

See SCENARIOS.md for named recipes and their tradeoffs.

## Resilience as a caller-side, opt-in choice (cross-process clients)

Beyond the internal pattern (configure once at the composition root; handlers call one line; callers see nothing),
the pipeline also supports a **caller-facing, opt-in, per-call-overridable** usage for generated cross-process
clients. The caller modes are:

1. **(a) Use the endpoint's declared default** - the endpoint registers a keyed `ResilientPipeline`; the caller resolves
   it by key via `[FromKeyedServices(...)]`. No new API.
2. **(b) Supply your own** - the caller passes its own `ResilientPipeline` (or resolves a different keyed pipeline)
   overriding the endpoint default.
3. **(c) Bypass** - the caller explicitly wants no resilience but still needs the `D2Result<T>` return shape: use
   `ResilientPipeline<TKey, TValue>.PassThrough`.

Use the internal pattern for a service's own outbound calls (a notification service calling its email
provider, an IP lookup against a third-party API) where the lib author controls the composition. Use the caller-opt-in
pattern for shared/generated cross-process clients (gRPC-client emitter output) where the caller owns the
latency/criticality tradeoff.

## Extensibility

`IResilientLayer<TKey, TValue>` is a single-method interface. Adding a new layer is mechanical: one new `XxxLayer`
impl + one new `Use*` builder method on the pipeline - no breaking changes to existing pipelines or call sites.

---
