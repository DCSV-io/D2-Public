<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Resilience

The platform's one resilience mechanism. It provides retry, circuit-breaker, singleflight, timeout, and concurrency
rate-limiting as composable pipeline layers. It is lock-free where possible (`Interlocked` operations,
`ConcurrentDictionary`) and has test seams for the clock and the delay (override functions). Resilience is
**caller-side, opt-in** (off by default - it costs latency) and **per-call overridable**.

Depends only on `DcsvIo.D2.Result` (for the `D2Result`-aware retry overload) and
`Microsoft.Extensions.DependencyInjection.Abstractions` (for keyed DI).

## Install

```bash
dotnet add package DcsvIo.D2.Resilience
```

> **Design rationale: custom primitives over Polly.** Most outbound boundaries of the platform are not HTTP (RabbitMQ
> publishes, EF Core, Redis via StackExchange, internal handler chains, SeaweedFS via SDK). Polly's main "free win" -
> its HttpClientFactory integration via `AddStandardResilienceHandler()` - applies cleanly to gRPC (since
> `Grpc.Net.Client` is built on `HttpClient`) and external HTTP APIs, but the HTTP-level integration only sees HTTP
> 200 + trailing gRPC status codes; retry-on-`StatusCode.Unavailable` requires custom predicates anyway. With <500 LOC
> of pure-logic primitives + first-class `D2Result.IsTransientRetryable` integration, owning the primitives is cheaper
> than wrapping Polly.

## Types (source map)
| Path                                                                       | Contents                                                                                                                                                                                                                                                   |
| -------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Retry/RetryHelper.cs`                                                     | Static `RetryAsync<T>` (generic) and `RetryD2ResultAsync<TData>` (D2Result-aware overload). Internal `IsTransientException` classifier and `CalculateDelay` math.                                                                                          |
| `Retry/RetryOptions.cs` + `Retry/RetryDefaults.cs`                         | `RetryOptions<T>` record - `MaxAttempts`, `BaseDelayMs`, `BackoffMultiplier`, `MaxDelayMs`, `Jitter`, `ShouldRetry`, `IsTransient`, `DelayFunc`. Defaults centralized in the non-generic `RetryDefaults` peer (sole definition, no per-T duplication).     |
| `CircuitBreaker/CircuitBreaker.cs`                                         | `CircuitBreaker<T>` - three-state Closed / Open / Half-Open with lock-free state transitions.                                                                                                                                                              |
| `CircuitBreaker/CircuitBreakerOptions.cs`                                  | `CircuitBreakerOptions` - `FailureThreshold`, `CooldownDuration`, `NowFunc` (test clock). Holds the single source of truth for breaker defaults; the breaker reads from a parameterless Options instance when nothing is supplied.                         |
| `CircuitBreaker/CircuitState.cs`                                           | `CircuitState` enum - `Closed`, `Open`, `HalfOpen`.                                                                                                                                                                                                        |
| `CircuitBreaker/CircuitOpenException.cs`                                   | Thrown by `ExecuteAsync` when the circuit is open and no fallback is supplied.                                                                                                                                                                             |
| `Singleflight/Singleflight.cs`                                             | `Singleflight<TKey, TValue>` - deduplicates concurrent in-flight async operations by key. Not a cache: keys are removed once the operation completes.                                                                                                      |
| `Timeout/TimeoutOptions.cs`                                                | `TimeoutOptions` - `Duration` (default 10 s). Zero or negative = pass-through (no CTS created). Canonical small-Options-record shape.                                                                                                                      |
| `RateLimiting/RateLimiter.cs`                                              | Hand-rolled `SemaphoreSlim`-based concurrency limiter (no `System.Threading.RateLimiting` package). `ExecuteAsync<T>` acquires a permit within `AcquisitionTimeout`, runs the op, releases in `finally`; throws `RateLimitRejectedException` on rejection. |
| `RateLimiting/RateLimiterOptions.cs`                                       | `RateLimiterOptions` - `MaxConcurrency` (default 100), `AcquisitionTimeout` (default `TimeSpan.Zero` = reject-fast). Canonical small-Options-record shape.                                                                                                 |
| `RateLimiting/RateLimitRejectedException.cs`                               | Thrown when a permit cannot be acquired within `AcquisitionTimeout`. Maps to `D2Result.TooManyRequests()` (429 / `RATE_LIMITED`) at the pipeline boundary.                                                                                                 |
| `Pipeline/IResilientLayer.cs`                                              | The decorator interface - one `WrapAsync(key, next, ct)` method.                                                                                                                                                                                           |
| `Pipeline/{Singleflight,CircuitBreaker,Retry,Timeout,RateLimiter}Layer.cs` | The concrete layer wrappers around the primitives above.                                                                                                                                                                                                   |
| `Pipeline/ResilientPipeline.cs`                                            | Composes layers in outer-first order; one `ExecuteAsync(key, op, ct)` returning `D2Result<TValue>` (never throws - every exception is mapped to a result). Exposes `PassThrough` static sentinel for the bypass-but-keep-D2Result-shape use case.          |
| `Pipeline/IResilientPipelineBuilder.cs` + `ResilientPipelineBuilder.cs`    | Fluent registration DSL - `.UseTimeout().UseRateLimiter().UseSingleflight().UseCircuitBreaker().UseRetries(opts)` etc.; `Build()` snapshots the accumulated layers into a `ResilientPipeline<TKey, TValue>`.                                               |
| `Pipeline/ResilientPipelineServiceCollectionExtensions.cs`                 | `AddResilientPipeline<TKey, TValue>(p => ...)` extension on `IServiceCollection`.                                                                                                                                                                          |

---

## Public API

### `RetryHelper.RetryAsync<T>` - exponential backoff with jitter

```csharp
var result = await RetryHelper.RetryAsync(
    operation: async (attempt, ct) =>
    {
        // 'attempt' is 1-based.
        return await SomeFlakyApiCall(ct);
    },
    options: new RetryOptions<MyResponse>
    {
        MaxAttempts = 5,
        BaseDelayMs = 200,
        BackoffMultiplier = 2.0,
        MaxDelayMs = 30_000,
        Jitter = true,
    },
    ct);
```

Defaults (when `options` is null): 5 attempts, 1s base delay, x2 multiplier, 30s ceiling, full jitter (uniform
`[0, calculated)`), retry transient exceptions only (`HttpRequestException` >=500 / 429 / 408, `TaskCanceledException`,
`TimeoutException`, `SocketException`), accept all returned values.

Behavioral guarantees:

- **Two retry triggers, evaluated independently per attempt:**
  - `IsTransient(ex)` - thrown exceptions. Default: the helper's built-in classifier. Override for gRPC
    `StatusCode.Unavailable`, custom transient codes, etc.
  - `ShouldRetry(value)` - returned values. Default: never retry returns. Useful for "retry on 5xx body without a thrown
    exception" patterns.
- **Final attempt always terminates the loop** by returning the value or throwing the exception - no defensive epilogue.
- **`OperationCanceledException` from `ct`** is re-raised as cancellation, never classified as transient (would
  otherwise mask user-initiated cancellation as a retryable network blip).
- **Backoff math**: `min(BaseDelayMs x BackoffMultiplier^retryIndex, MaxDelayMs)`, then jittered to
  `random(0, calculated)` when `Jitter=true`. Exponent clamped to 63 to avoid `Math.Pow` overflow on degenerate retry
  counts.

#### `RetryD2ResultAsync<TData>` - `D2Result`-aware overload

When the operation returns a `D2Result<TData>` instead of throwing, the default `ShouldRetry` predicate becomes
`r => r.Failed && r.IsTransientRetryable`:

```csharp
var result = await RetryHelper.RetryD2ResultAsync(
    operation: (_, ct) => SomeHandlerThatReturnsD2Result(ct),
    options: new RetryOptions<D2Result<MyDto>> { MaxAttempts = 4 },
    ct);
```

Retries on `ServiceUnavailable` and `RateLimited` results (the error codes `IsTransientRetryable` covers). It does
not retry on `UnhandledException` results - unknown system state must never be auto-retried (a side effect may have
committed). A caller-supplied `ShouldRetry` always replaces the default.

### `CircuitBreaker<T>` - three-state lock-free breaker

```csharp
var cb = new CircuitBreaker<MyResponse>(
    isFailure: r => !r.Success,                         // value-failure predicate
    options: new(failureThreshold: 5, cooldownDuration: TimeSpan.FromSeconds(30)),
    onStateChange: (from, to) =>
        logger.LogInformation("circuit: {From} -> {To}", from, to));

var result = await cb.ExecuteAsync(
    operation: ct => SomeUpstreamCall(ct),
    fallback: () => ValueTask.FromResult(MyResponse.Cached),
    ct);
```

`CircuitBreakerOptions` follows the project's small-Options-record convention: every parameter is nullable + falls back
to its documented default in the ctor body, so call sites stay terse:

```csharp
new()                                                 // all defaults (5, 30s, null)
new(3)                                                // FailureThreshold=3, rest defaulted
new(3, TimeSpan.FromMilliseconds(100), clock.Now)     // all three positionally
new(failureThreshold: 3, nowFunc: clock.Now)          // skip middle, named args
new(0)                                                // explicit 0 is preserved (not coerced to default)
```

`with`-expressions still work for record-style overrides on an existing instance.

State machine:

- **Closed** - calls pass through. Failures (thrown exceptions or `isFailure(value) == true`) increment a counter.
  Success resets the counter. When counter hits `FailureThreshold` -> **Open**.
- **Open** - fast-fails. With a `fallback`, returns the fallback's value; without one, throws `CircuitOpenException`.
  Stays open until `CooldownDuration` has elapsed.
- **Half-Open** (after cooldown) - exactly one caller takes the probe slot (lock-free `Interlocked.CompareExchange` on a
  probe-in-flight flag); concurrent callers during the probe receive the fallback (or `CircuitOpenException`). On probe
  success -> **Closed** (counter reset). On probe failure -> straight back to **Open** (cooldown timer reset).

Thread-safety:

- All state via `Interlocked` operations + `Volatile.Read`. No locks.
- `onStateChange` callback fires synchronously on the thread that triggered the transition. Idempotent transitions
  (Closed -> Closed) do not fire it. Keep callbacks fast and non-blocking.
- **`onStateChange` must not throw.** A throwing callback replaces the upstream exception that triggered the
  transition: a buggy logger inside the callback can replace a meaningful "TimeoutException from upstream X" with its
  own "InvalidOperationException from logger", which makes outage diagnosis harder. Wrap the callback body in your own
  try/catch (or use only plain log/metric calls that do not throw) to preserve the upstream exception for callers.
- `Reset()` manually returns the breaker to Closed (clears counter + probe flag); only fires `onStateChange` if state
  actually changed.
- `FailureCount` reads the current consecutive failure count (a `Volatile.Read`); every observed success and `Reset()`
  set it back to zero.

> **Telemetry is consumer-owned.** This lib emits no spans / metrics / logs of its own - by design, to stay free of
> `System.Diagnostics.DiagnosticSource` and `Microsoft.Extensions.Logging` transitive deps. The `onStateChange` callback
> is the canonical observability seam for circuit-breaker transitions; the `RetryHelper.RetryAsync` `logger` parameter
> is the seam for retry attempts. Pipeline-level observability (per-layer counters, per-attempt spans) is out of scope
> for this lib - the consumer composes it at their own observability boundary.

Test seams:

- `CircuitBreakerOptions.NowFunc` - override for the monotonic-millisecond clock (default `Environment.TickCount64`).
  Tests use a `FakeClock` to advance time deterministically without `Task.Delay`.

### `TimeoutLayer<TKey, TValue>` - wall-clock deadline

Bounds the wrapped operation with a linked `CancellationTokenSource`. On expiry throws `TimeoutException` (distinct from
caller cancellation) so:

- an outer `RetryLayer` retries it (`TimeoutException` is already classified transient by
  `RetryHelper.IsTransientException` - zero classifier change needed)
- a leaked timeout at the pipeline boundary maps to `D2Result.ServiceUnavailable()` (503, `IsTransientRetryable = true`)

Place at **two positions** in the same pipeline to express separate total-request and per-attempt deadlines (the
builder's flat-list accumulator supports duplicate layer types):

```csharp
services.AddResilientPipeline<string, T>("key", p => p
    .UseRateLimiter()
    .UseTimeout(new(TimeSpan.FromSeconds(30)))  // total: bounds all retries combined
    .UseRetries(new() { MaxAttempts = 3 })
    .UseCircuitBreaker("key")
    .UseTimeout(new(TimeSpan.FromSeconds(5))));  // per-attempt: inside retry loop
```

`TimeoutOptions` follows the small-Options-record convention: parameterless ctor = 10-second default;
`Duration <= Zero` = pass-through (no CTS allocated).

### `RateLimiter` + `RateLimiterLayer<TKey, TValue>` - concurrency limiter

Hand-rolled `SemaphoreSlim`-based concurrency limiter (no `System.Threading.RateLimiting` package - matches the
library's aim of <500 lines of code and one external dependency). Bounds the number of concurrent in-flight calls to
`MaxConcurrency`. Callers that cannot acquire a permit within `AcquisitionTimeout` are rejected via
`RateLimitRejectedException` (-> `D2Result.TooManyRequests()` / 429 at the pipeline boundary).

> **Client-side, in-process only.** This is admission control for outbound calls - it limits concurrent pressure from
> this process on an upstream. It is not the server-side, distributed per-tier rate-limit middleware (which uses
> `IDistributedCache` counters).

```csharp
services.AddResilientPipeline<string, T>("key", p => p
    .UseRateLimiter(new RateLimiterOptions(maxConcurrency: 10))
    .UseRetries());
```

To share one `RateLimiter` across multiple pipelines (e.g. a shared broker-level concurrency cap), register it as a
keyed singleton and use the service-key overload - mirroring the shared circuit-breaker pattern:

```csharp
services.AddKeyedSingleton<RateLimiter>("broker", (_, _) => new(new(maxConcurrency: 10)));
services.AddResilientPipeline<string, T>("critical", p => p.UseRateLimiter("broker"));
services.AddResilientPipeline<string, T>("routine",  p => p.UseRateLimiter("broker"));
```

`RateLimiterOptions` follows the small-Options-record convention. `RateLimiterLayer` is `IDisposable`. In the
inline-options case, the layer owns the `RateLimiter` and its `SemaphoreSlim`; disposal propagates through the pipeline:
`ResilientPipeline<TKey, TValue>` implements `IDisposable` and iterates its layers on dispose, so the container disposes
the pipeline singleton at teardown and the owned semaphore is released. In the keyed-DI case, the `RateLimiter`
singleton is registered directly with the container, which disposes it at teardown; the layer holds a reference only and
its `Dispose` is a no-op.

### `ResilientPipeline<TKey, TValue>.PassThrough` - bypass sentinel

```csharp
// Caller explicitly wants no resilience but still wants the D2Result shape.
var result = await ResilientPipeline<string, T>.PassThrough.ExecuteAsync(key, op, ct);
```

A zero-layer pipeline singleton that performs only the exception -> `D2Result<TValue>` mapping - no retry, no CB, no
timeout, no rate-limiter. Equivalent to `new ResilientPipeline<TKey, TValue>()` but named so the bypass intent is
explicit in generated clients and per-call override sites.

### `Singleflight<TKey, TValue>` - concurrent-call deduplication

The first caller for a given key runs the operation; concurrent callers for the same key share the same `Task<TValue>`.
Once the operation completes (success or failure), the key is removed from the in-flight map.

```csharp
private static readonly Singleflight<string, WhoIsRecord> sr_whoIsLookups = new();

public Task<WhoIsRecord> ResolveAsync(string ip, CancellationToken ct) =>
    sr_whoIsLookups.ExecuteAsync(
        key: ip,
        operation: token => FetchExpensiveWhoIsAsync(ip, token),
        ct: ct).AsTask();
```

**This is not a cache.** Once the operation completes, the key is removed and the next call re-runs the operation. Use
Singleflight to prevent thundering-herd duplication of in-progress work, then layer a real cache
(`DcsvIo.D2.Caching.Memory`, `DcsvIo.D2.Caching.Redis`, etc.) on top of the singleflight call site if you want
persistent reuse of the result.

#### When to use Singleflight - and when not

| Use SF when...                                                                                       | Don't use SF when...                                                                                    |
| ---------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| Multiple concurrent callers ask for the **same logical thing** by key                                | Each call is a **distinct intent** that should produce a distinct effect                                |
| Operation is **idempotent** - running it once and sharing the result is correct for everyone waiting | Operation has a **per-call side effect** (publish a message, send an email, write a row, charge a card) |
| You're preventing a **thundering-herd / cache-miss stampede** on a hot key                           | Two callers happen to share a transport but represent different business events                         |

Concrete:

- ✅ **WhoIs / IPinfo lookup by IP** - 50 requests for `1.2.3.4` collapse to 1 upstream call.
- ✅ **JWKS fetch** - every concurrent token validation wants the same document.
- ✅ **Reference-data lookups** (currencies, countries, feature-flag manifests).
- ✅ **Cache-miss read for a hot key** (config-by-name, user-by-id during a request burst).
- ❌ **Audit / event publishes** - each event is unique data; SF would silently drop events whose key collided.
- ❌ **Email / SMS / push delivery** - each `Notify` is a discrete intent. Two callers asking to email Alice = two
  emails.
- ❌ **gRPC / HTTP writes** (`Create*`, `Update*`, `Delete*`) - SF in front skips the work of every caller after the
  first.
- ❌ **Outbox / queue drain publishers** - dedup belongs upstream (in the outbox itself), not in the publisher.

Heuristic: if the answer to _"if two callers ask, is one shared answer correct for both?"_ is **yes**, SF fits. If the
answer is _"two callers means two side effects we want"_, SF is a bug.

Behavioral guarantees:

- **`TKey` constraint**: `notnull`. Strings, GUIDs, value-types, custom hashable records all work.
- **Per-caller cancellation does not affect siblings.** When you pass a `CancellationToken`, only your wait is
  cancelable. The shared operation runs with `CancellationToken.None`, so one caller bailing out cannot poison the
  result for everyone else sharing it.
- **Exception propagation**: an operation throw propagates to all waiting callers (they share the same `Task`). The key
  is still removed in the `finally` block, so the next call after the throw starts a fresh operation.
- **Lazy initialization** via `Lazy<Task<TValue>>` with `LazyThreadSafetyMode.ExecutionAndPublication` - the operation
  is started exactly once per key even under aggressive concurrency.
- **`Size`** property exposes the current in-flight count (instantaneous; useful for metrics).

---

## When to reach for which

| Need                                                                                         | Tool                                                                                               |
| -------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| Backoff-and-retry around a flaky external call                                               | `RetryHelper.RetryAsync`                                                                           |
| Backoff-and-retry around a `D2Result`-returning handler chain                                | `RetryHelper.RetryD2ResultAsync`                                                                   |
| Avoid hammering a confirmed-down upstream while it recovers                                  | `CircuitBreaker<T>`                                                                                |
| Avoid the "five concurrent first requests trigger five identical expensive lookups" stampede | `Singleflight<TKey, TValue>`                                                                       |
| Bound an operation to a wall-clock deadline; surface expiry as a retryable transient         | `TimeoutLayer<TKey, TValue>` (via `UseTimeout` on the pipeline builder)                            |
| Limit concurrent in-flight outbound calls (client-side admission control)                    | `RateLimiter` + `RateLimiterLayer<TKey, TValue>` (via `UseRateLimiter`)                            |
| Bypass-but-keep-D2Result-shape (per-call "no resilience" override)                           | `ResilientPipeline<TKey, TValue>.PassThrough`                                                      |
| Compose any of the above behind a single call site that returns a `D2Result`                 | `ResilientPipeline<TKey, TValue>` (see [Pipeline](#pipeline---the-high-level-composition-surface)) |

The primitives compose. The `Pipeline` namespace is the canonical way to do this composition; use the raw primitives
only when you need direct control or have an unusual layering requirement.

---

## Pipeline - the high-level composition surface

The pipeline builder, the stage order, what each stage observes and the options each one binds are in
`pipeline.md`, in this package's documentation folder in the repository.

## Tests

`packages/dotnet/tests/Unit/Resilience/` - adversarial tests for every public surface. Test groups:

- **`RetryHelper`**: full transient-classifier matrix (HTTP 5xx / 429 / 408 / non-transient codes / null status /
  TaskCanceled / Timeout / Socket / arbitrary). Happy path, throws-then-succeeds, throws-every-attempt-exhaustion,
  ShouldRetry-true-then-false, ShouldRetry-always-true (last-value wins on exhaustion), alternating throw+return (last
  terminator wins), pre-canceled token, OCE-from-ct (not classified transient), DelayFunc-invoked-between-retries.
- **`RetryD2ResultAsync`**: default predicate retries `ServiceUnavailable`, default predicate does not retry `NotFound`,
  caller-`ShouldRetry`-override wins, null-options behavior.
- **`CalculateDelay`** (internal): zero-index returns base, multiplier applied, max-delay clamp, exponent overflow
  clamp, jitter range property (sampled repeatedly in `[0, calculated)`).
- **`CircuitBreaker`**: initial state, single success/failure, threshold transition Closed->Open, mixed
  exception+value-failure threshold, success resets counter, Open without/with fallback, Open->HalfOpen on cooldown,
  HalfOpen probe success closes, HalfOpen probe failure (exception or value-failure) reopens, HalfOpen probe-lock
  (concurrent caller gets fallback / throws), `Reset` from Open fires callback, `Reset` from Closed is no-op,
  callback-null branches on every transition.
- **`Singleflight`**: single-call sanity, concurrent-callers-same-key dedup (1 invocation, 3 returns),
  concurrent-callers-different-keys both run, sequential-calls-same-key re-run (proves no caching), exception
  propagation to all waiters + key removed for retry, per-caller cancellation does not affect siblings,
  no-cancelable-token fast path, non-string key (Guid).
- **`CircuitBreakerOptions` / `RetryOptions` / `CircuitOpenException`**: defaults, init-only overrides, exception
  constructor variants.
- **`TimeoutLayer`**: op-completes-before-timeout (no throw), timeout-fires-throws-TimeoutException (not OCE),
  message-contains-duration, Duration-zero/negative pass-through, default-10s-no-fire,
  caller-ct-cancel-not-masked-as-timeout, both-tokens-cancel-caller-wins,
  per-attempt-timeout-inside-retry-retried-then-succeeds, per-attempt-timeout-exhausted-maps-to-ServiceUnavailable,
  total-timeout-maps-to-ServiceUnavailable.
- **`TimeoutOptions`**: default ctor 10s, parameterized null->default, explicit duration, zero preserved, negative
  preserved, with-expression.
- **`RateLimiter`**: under-limit runs, N+1-rejected-at-zero-acquisition-timeout, positive-acquisition-admits-on-release,
  positive-acquisition-rejects-after-timeout, op-throws-permit-released,
  caller-ct-canceled-before-acquire-no-permit-leaked, max-concurrency-stress (many callers against a K=5 gate),
  Dispose-no-throw.
- **`RateLimiterLayer`**: under-limit runs, gate-full-zero-timeout-throws, release-on-throw,
  pipeline-maps-to-TooManyRequests, keyed-DI resolves, inline-options builds, explicit-instance does-not-own-limiter,
  Dispose-no-throw.
- **`RateLimiterOptions`**: default ctor all-defaults, null-args->defaults, explicit values, zero/negative
  MaxConcurrency throws ArgumentOutOfRangeException (regression pin), one is minimum valid, zero AcquisitionTimeout
  preserved, with-expression.
- **`RateLimitRejectedException`**: every ctor variant, IsException subtype.
- **`ResilientPipelineTests`**: TimeoutException-maps-ServiceUnavailable,
  RateLimitRejectedException-maps-TooManyRequests, PassThrough-maps-exceptions, PassThrough-success-ok,
  PassThrough-is-singleton, PassThrough-no-retry, canonical-full-stack-order-tracing.
- **`ResilientPipelineBuilderTests`**: UseTimeout-null/explicit, UseTimeout-twice-two-positions,
  UseRateLimiter-null/explicit, UseRateLimiter-keyed-DI.
- **`PipelineCompositionTests`**: 6-layer nesting adversarial tests covering all canonical configurations and the
  order-of-operations proofs: canonical-full-stack-RL-Tt-R-CB-Ta (flaky succeeds, permanently-down exhausts), CB<->R
  order-sensitivity proof (R->CB real-op-called-once-vs CB->R real-op-called-N-times),
  total-timeout-outside-retry-terminates-loop, per-attempt-timeout-inside-retry-retried-then-succeeds,
  RL-outermost-short-circuits-inner-layers, RL-rejection-not-retried-when-inside-retry,
  SF-outermost-dedup-across-full-stack, SF-distinct-keys-distinct-executions,
  caller-cancellation-through-deep-stack-maps-to-Canceled, PassThrough-zero-layers-no-retry.
- **`ResilientPipelineServiceCollectionExtensionsTests`**:
  `Dispose_InlineOptionsPipeline_DisposesOwnedRateLimiter` - a regression pin proving `ServiceProvider.Dispose`
  propagates through the keyed singleton pipeline to the inline-owned `RateLimiter`/`SemaphoreSlim`.

Run: `dotnet test packages/dotnet/tests`

Coverage, from the repo root:

```bash
dotnet test packages/dotnet/tests --collect:"XPlat Code Coverage"
```

The run writes a Cobertura report per test project under `TestResults/`. Render it with
`dotnet tool install --global dotnet-reportgenerator-globaltool` and
`reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report`, which lists every
file and its uncovered lines.
