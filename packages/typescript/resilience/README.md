<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-resilience

Resilience primitives for TypeScript: retry, circuit breaker, singleflight, timeout, rate limiter and a composable
pipeline. Mirrors `DcsvIo.D2.Resilience` (.NET).

## Install

```bash
pnpm add @dcsv-io/d2-resilience
```

## Public API

| Export                                                                                         | Purpose                                                                                                                        |
| ---------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| `RetryHelper.retryAsync(op, opts?, signal?, rng?)`                                             | Generic retry with backoff + jitter; cancellation never retried; conservative default classifier.                              |
| `RetryHelper.retryD2ResultAsync(op, opts?, signal?, rng?)`                                     | `D2Result`-aware retry - retries failure shapes matching `shouldRetry`/`isTransient`.                                          |
| `defaultIsTransient(err)`                                                                      | The default transient-error whitelist (TimeoutError / CircuitOpenError / genuine network failures).                            |
| `RetryOptions<T>` / `RETRY_DEFAULTS`                                                           | Policy options + defaults (maxAttempts 3 / 100ms base / 2x mul / 5s cap / 20% jitter).                                         |
| `CircuitBreaker<T>`                                                                            | Three-state (Closed / Open / HalfOpen) breaker; single-probe HalfOpen; `fallback` / `isFailure` / `onStateChange` / `reset()`. |
| `CircuitBreakerOptions<T>` / `CircuitState` / `CircuitOpenError`                               | Config (incl. `isFailure` / `onStateChange`) + state enum + error type.                                                        |
| `Singleflight<K, V>`                                                                           | In-flight dedup by key.                                                                                                        |
| `TimeoutLayer` / `TimeoutOptions` / `TimeoutError` / `TIMEOUT_DEFAULTS`                        | Wall-clock deadline per position (total-request or per-attempt). Default: 10 s.                                                |
| `RateLimiterLayer` / `RateLimiterOptions` / `RateLimitRejectedError` / `RATE_LIMITER_DEFAULTS` | In-process concurrency limiter. Default: 100 slots, reject-fast.                                                               |
| `ResilientPipeline.execute(key, op, signal?)` / `.PassThrough`                                 | Composed pipeline (`op` receives the `AbortSignal` it should observe) + zero-layer bypass sentinel.                            |
| `ResilientPipelineBuilder`                                                                     | Fluent builder (outer-first ordering).                                                                                         |
| `IResilientLayer`                                                                              | Layer contract - `execute(key, op, signal?)`, mirroring .NET `WrapAsync(key, next, ct)`.                                       |

## Dependencies

- `@dcsv-io/d2-utilities` - boundary helpers
- `@dcsv-io/d2-result` (D2Result-aware retry overload)
- `@dcsv-io/d2-logging` (declared as a dependency; no source file of this package imports it)

## Usage example

```ts
import {
  RetryHelper,
  CircuitBreaker,
  ResilientPipelineBuilder,
} from "@dcsv-io/d2-resilience";

// Plain retry.
const r = await RetryHelper.retryAsync(() => fetchUser(id));

// Pipeline composition - canonical full-stack ordering.
const pipe = new ResilientPipelineBuilder()
  .useSingleflight() // outermost (optional)
  .useRateLimiter({ maxConcurrency: 10 }) // admission control
  .useTimeout({ durationMs: 30_000 }) // total-request budget
  .useRetries({ maxAttempts: 3 })
  .useCircuitBreaker({ failureThreshold: 5, cooldownMs: 30_000 })
  .useTimeout({ durationMs: 5_000 }) // per-attempt deadline
  .build();

// `op` receives the AbortSignal it should observe - pass it INTO fetch so the
// TimeoutLayer genuinely cancels the request (and releases the socket) on expiry.
const data = await pipe.execute(`users:${id}`, (signal) =>
  fetch(`/users/${id}`, { signal }),
);

// Bypass (no resilience - raw call, same call-site shape).
const raw = await ResilientPipeline.PassThrough.execute(
  `users:${id}`,
  (signal) => fetch(`/users/${id}`, { signal }),
);
```

## Canonical layer ordering

The builder uses outer-first ordering: the first call added is the outermost
wrapper. The canonical full-stack order (matching the .NET standard):

```
Singleflight -> RateLimiter -> TotalTimeout -> Retry -> CircuitBreaker -> PerAttemptTimeout
```

Order matters. A circuit breaker _outside_ a retry trips after N total
failures across all retry attempts. A circuit breaker _inside_ a retry trips
during a single burst of N consecutive per-attempt failures and may re-close
on the next retry. Placing a rate limiter outermost means rejected callers
never consume retry or timeout budget.

`useTimeout` can be called twice to add both a total-request timeout (outer,
above `useRetries`) and a per-attempt timeout (inner, below `useRetries`). Both
are independent `TimeoutLayer` instances - the outer fires across all retries
combined; the inner fires per individual attempt and allows an outer `useRetries`
to re-attempt.

## Cancellation contract (`AbortSignal` threading)

`IResilientLayer.execute(key, op, signal?)` passes an optional `AbortSignal`
down the layer stack, mirroring .NET's
`WrapAsync(key, next, ct)` / `CancellationToken`. The op receives the
`AbortSignal` it should observe for cooperative cancellation:

```ts
const controller = new AbortController();
const data = await pipe.execute(
  `users:${id}`,
  (signal) => fetch(url, { signal }), // pass the threaded signal INTO fetch
  controller.signal, // caller's own cancellation (optional)
);
```

A layer may substitute the signal it passes inward: `TimeoutLayer` gives the op a signal linked to both the caller's
signal and its own deadline (mirroring the .NET `TimeoutLayer` linked `CancellationToken`), so the op is canceled on
timeout. `Singleflight` is the deliberate exception - see [Singleflight cancellation
guarantee](#singleflight-cancellation-guarantee).

## CircuitBreaker

Three-state breaker (Closed -> Open -> HalfOpen) with full feature parity with
.NET `CircuitBreaker<T>`:

```ts
const cb = new CircuitBreaker<UserDto>({
  failureThreshold: 5,
  cooldownMs: 30_000,
  // Value-based failures (a returned-but-failed value counts WITHOUT throwing):
  isFailure: (r) => r.status === "error",
  // Observability seam - the breaker emits no telemetry of its own:
  onStateChange: (from, to) => metrics.circuitTransition(from, to),
});

// fallback (optional 2nd arg) serves cached/default data when the circuit is
// open (or a HalfOpen probe slot is already taken) instead of throwing:
const user = await cb.execute(
  () => fetchUser(id),
  () => CACHED_USER, // omit to throw CircuitOpenError instead
);

cb.reset(); // manual return to Closed (clears the failure count)
```

- HalfOpen single-probe: after the cooldown elapses, exactly one caller is
  admitted as the probe; concurrent callers that arrive while the probe is
  in-flight receive the `fallback` (when supplied) or `CircuitOpenError`. JS is
  single-threaded, so the breaker uses a synchronous check-and-set on a
  probe-in-flight flag (performed before the first `await`) - the structural
  equivalent of .NET's lock-free `Interlocked.CompareExchange`. This prevents a
  thundering herd of probes overloading a recovering upstream.
- `isFailure` value predicate: thrown errors always count as failures.
  `isFailure` additionally counts a returned (non-thrown) value as a failure,
  which operations that surface failures as values (e.g. a `D2Result`) require.
  A value satisfying the predicate increments the failure counter and is then
  returned to the caller unchanged (it is not re-thrown).
- `fallback`: the optional second argument to `execute`. Invoked when the
  circuit is Open or a HalfOpen probe slot is taken; returns the fallback's
  value instead of throwing `CircuitOpenError`.
- `onStateChange(from, to)`: fires synchronously on every real transition
  (an idempotent Closed->Closed on repeated success does not fire). The canonical
  observability seam. Warning: a throwing callback propagates out of
  `execute()` and replaces the upstream error, so keep the body to non-throwing
  log/metric calls (matches the .NET remark).
- `reset()`: manually returns the breaker to Closed, clearing the failure
  count + probe flag; fires `onStateChange` only when the state actually changed.

The pipeline `CircuitBreakerLayer` calls `execute(op)` with no fallback (as
the .NET `CircuitBreakerLayer` does), so an open breaker throws `CircuitOpenError`
to the pipeline boundary for the caller to map.

## Retry - default transient classifier

When a caller supplies neither `shouldRetry` nor `isTransient`, the retry helper
classifies errors with a conservative whitelist (`defaultIsTransient`) that
mirrors the intent of .NET `RetryHelper.IsTransientException`: retry only genuine
transient / network / timeout conditions, never arbitrary programming bugs.

The JS error taxonomy differs from .NET's (there is no `HttpRequestException` /
`SocketException`), so the TS transient set - matched by error `name`, the same
convention as the cancellation check - is:

| TS error                                       | Transient? | .NET analogue                                   |
| ---------------------------------------------- | ---------- | ----------------------------------------------- |
| `TimeoutError` (this lib's `TimeoutLayer`)     | ✅ yes      | `TimeoutException` / `TaskCanceledException`    |
| `CircuitOpenError`                             | ✅ yes      | `CircuitOpenException`                          |
| `Error` with `name === "NetworkError"`         | ✅ yes      | (DOM/whatwg network error)                      |
| `TypeError` with a `cause` (undici net fail)   | ✅ yes      | `SocketException` (network-level fetch failure) |
| `TypeError` with a known fetch-failure message | ✅ yes      | `SocketException` (e.g. `"fetch failed"`)       |
| plain `Error` / `RangeError` / assertion       | ❌ no       | (not in the .NET whitelist)                     |
| bare `TypeError` (no cause, non-net message)   | ❌ no       | (programming bug - not transient)               |
| non-`Error` thrown value                       | ❌ no       | (not transient)                                 |
| `AbortError` / message `"aborted"`             | ❌ no       | caller cancellation (never retried)             |

A caller-cancellation `AbortError` is rejected before the classifier and is
never retried. To retry an otherwise-non-transient error, supply an explicit
`isTransient` / `shouldRetry` predicate.

## TimeoutLayer

Bounds the inner operation with a wall-clock deadline and cancels
it on expiry. The op receives a linked `AbortSignal` that aborts when either the
caller's signal fires or the timeout elapses. On expiry the layer aborts that
signal - so a cooperative op (e.g. a `fetch`) is actually canceled and its
socket released - and rejects with `TimeoutError` (name `"TimeoutError"`). The
deadline stays deterministic: the inner promise is raced against the timer, so a
non-cooperative op (one that ignores its signal) still times out.

`TimeoutError` is distinct from a caller-initiated `AbortError`: an outer retry
layer treats `TimeoutError` as transient and re-attempts, but a caller abort is
never retried. A caller-initiated abort propagates as the caller's `AbortError`,
not masked as `TimeoutError` (matching .NET's
`when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)`
guard - a caller cancellation wins over a coincident timeout).

The timer and the caller-signal listener are always cleaned up on every settle
path (no leak). `durationMs <= 0` disables the timeout - the layer becomes a
pass-through (no timer is created; the caller signal is forwarded unchanged).

## Singleflight cancellation guarantee

The deduplicated shared operation runs with no signal (`undefined`, the JS
analogue of `CancellationToken.None`): one caller aborting must not cancel the
shared work that the other waiters still depend on. Each caller's _wait_ is
cancelable by that caller's own signal - an aborting caller's promise rejects
with `AbortError` while the shared op continues and the remaining waiters
receive its result. This mirrors .NET `Singleflight.ExecuteAsync`'s
`Task.WaitAsync(ct)` (cancels the wait, never the shared `Task`).

## RateLimiterLayer

Hand-rolled in-process concurrency limiter (counter + FIFO waiter queue).
Limits the number of concurrent in-flight operations to `maxConcurrency`.
A caller that cannot acquire a permit within `acquisitionTimeoutMs` is rejected
via `RateLimitRejectedError` (not queued indefinitely).

`acquisitionTimeoutMs <= 0` means reject-fast (non-blocking). Permits are
released in a `finally` block - released on both success and throw, so no
permits leak on inner-op failures.

`maxConcurrency < 1` throws `RangeError` at construction.

The limiter is client-side and in-process only. It is admission control for outbound calls -
it limits concurrent pressure from this process on an upstream. It is not
the server-side distributed rate-limit middleware.

## Caller-side opt-in and bypass

Resilience is opt-in - it costs latency (retries, timeouts, admission waits)
and should be an explicit caller choice. The usage modes:

1. Declared default - resolve a keyed pipeline registered at the
   composition root and pass it to the client method.
2. Custom override - pass a caller-owned `ResilientPipeline` to the client
   method, overriding any declared default.
3. Bypass - use `ResilientPipeline.PassThrough` for a zero-layer pipeline
   that runs the op directly with no wrapping.

## Parity with .NET

Mirrors `DcsvIo.D2.Resilience`:

- `RetryHelper.retryAsync` <-> `RetryHelper.RetryAsync<T>` - same conservative
  default classifier (`defaultIsTransient` <-> `IsTransientException`): only
  genuine transient/network/timeout errors retry when no caller predicate is
  supplied.
- `RetryHelper.retryD2ResultAsync` <-> `RetryHelper.RetryD2ResultAsync<T>` -
  same "only retry transient fail-results" carve-out.
- `CircuitBreaker` <-> `CircuitBreaker<T>` - full feature parity: same
  three-state lifecycle, HalfOpen single-probe enforcement, `isFailure`
  value-based failure predicate, `fallback`, `onStateChange` observability
  seam, and `reset()`.
- `Singleflight` <-> `Singleflight<TKey, TValue>` - same key-coalescing; same
  per-caller-cancellation-only guarantee (the shared op runs uncancelable by
  any single caller; each caller's wait is cancelable by that caller's signal).
- `TimeoutLayer` <-> `TimeoutLayer<TKey, TValue>` - same deadline semantics; same
  linked-signal cooperative cancellation (TS threads an `AbortSignal` linked to
  the caller signal + the deadline, mirroring the .NET linked `CancellationToken`);
  timeout surfaces as a distinct error (not caller-abort); a caller abort wins
  over a coincident timeout.
- `RateLimiterLayer` <-> `RateLimiterLayer<TKey, TValue>` - same concurrency-
  limiter semantics; hand-rolled (no `System.Threading.RateLimiting` equivalent
  in Node).
- `ResilientPipeline.PassThrough` <-> `ResilientPipeline<TKey, TValue>.PassThrough`.
- `ResilientPipelineBuilder` <-> `ResilientPipelineBuilder` - same outer-first
  ordering; same layer-at-two-positions capability.
- Cancellation never classified as transient (matches .NET behavior).

Documented divergences (ADR-0014):

- TS pipeline returns `Promise<T>` (throws); .NET pipeline returns
  `D2Result<T>` (maps). Callers map `TimeoutError` / `RateLimitRejectedError`
  to their own `D2Result` shape.
- TS jitter is a fractional multiplier (e.g. `0.2` = +/-20%); .NET jitter is a
  boolean (full-jitter `random(0, computed)`).
- Retry numeric defaults differ intentionally: TS `RETRY_DEFAULTS` =
  3 attempts / 100 ms base / 5 s cap; .NET `RetryDefaults` = 5 attempts /
  1000 ms base / 30 s cap. The faster TS defaults are tuned for browser/Node
  UX - a browser request must not spend 30 s retrying - aligning with the same
  browser/Node-timing rationale as the multiplicative-jitter choice. Both
  surfaces accept explicit overrides; only the no-override default differs.
- TS key type is always `string`; .NET key is generic `TKey`.
- TS passes an `AbortSignal` through `IResilientLayer.execute(key, op, signal?)`,
  mirroring .NET's `CancellationToken` in
  `WrapAsync(key, next, ct)`. `TimeoutLayer` cancels the inner op via a linked
  signal; `Singleflight` runs the shared op with no signal (~= `CancellationToken.None`).

## Edge cases

- An already-aborted `AbortSignal` short-circuits the retry loop and the
  rate-limiter gate before the first attempt (`AbortError`).
- A caller abort while the rate-limiter is waiting for a permit rejects
  `AbortError` and does not consume / leak a permit.
- On `TimeoutLayer` expiry the inner op's (linked) signal is aborted - a
  cooperative op is canceled; a caller abort wins over a coincident
  timeout (`AbortError`, not `TimeoutError`).
- A `Singleflight` caller aborting cancels only its own wait - the shared op
  keeps running and the remaining waiters still receive its result.
- `maxAttempts < 1` -> `RangeError`.
- `TimeoutLayer` with `durationMs <= 0` -> pass-through (no timer created).
- `RateLimiterLayer` with `maxConcurrency < 1` -> `RangeError` at construction.
- `Singleflight` clears entries after settle - back-pressure does not
  accumulate indefinitely.
- HalfOpen failure re-arms cooldown (single-trip semantics).
- HalfOpen admits exactly one probe; concurrent callers during the probe get
  the `fallback` (or `CircuitOpenError` when none is supplied).
- An Open `CircuitBreaker` with a `fallback` returns the fallback's value
  instead of throwing `CircuitOpenError`.
- A returned value satisfying `isFailure` trips the breaker without throwing.
- `onStateChange` fires only on a real transition (idempotent Closed->Closed on
  repeated success does not fire); `reset()` returns to Closed and clears the
  failure count.
- A non-transient default error (plain `Error`, programming `TypeError`, etc.)
  is not retried unless an explicit `isTransient` / `shouldRetry` opts it in.
- `CircuitBreaker` rejects `failureThreshold < 1` and `cooldownMs < 0`.
