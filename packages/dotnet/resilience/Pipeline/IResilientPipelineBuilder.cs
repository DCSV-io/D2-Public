// -----------------------------------------------------------------------
// <copyright file="IResilientPipelineBuilder.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Resilience.Pipeline;

using DcsvIo.D2.Resilience.CircuitBreaker;
using DcsvIo.D2.Resilience.RateLimiting;
using DcsvIo.D2.Resilience.Retry;
using DcsvIo.D2.Resilience.Singleflight;
using DcsvIo.D2.Resilience.Timeout;

/// <summary>
/// Fluent builder for composing a <see cref="ResilientPipeline{TKey, TValue}"/>
/// at registration time. The order in which <c>Use*</c> methods are called
/// IS the layer order in the resulting pipeline (outer-first).
/// </summary>
/// <typeparam name="TKey">Per-call key type.</typeparam>
/// <typeparam name="TValue">The value type produced by the operation.</typeparam>
/// <remarks>
/// Resilience primitives must be registered as keyed services in DI - every
/// <c>Use*</c> overload that resolves from DI requires an explicit
/// <c>serviceKey</c>. The library provides no unkeyed resolution path,
/// because two unkeyed registrations of the same shape overwrite each other
/// silently (the last registration wins). The keyed-only rule also keeps the
/// call site unambiguous - every <c>UseX(serviceKey)</c> states which
/// primitive instance it resolves, with no implicit context to track.
/// <para>
/// For manual composition where DI registration is not appropriate, the
/// explicit-instance overloads (<c>UseSingleflight(instance)</c> /
/// <c>UseCircuitBreaker(instance)</c>) bypass DI entirely.
/// </para>
/// </remarks>
public interface IResilientPipelineBuilder<TKey, TValue>
    where TKey : notnull
{
    /// <summary>
    /// Adds a <see cref="SingleflightLayer{TKey, TValue}"/> resolved from DI
    /// via <paramref name="serviceKey"/>.
    /// </summary>
    /// <param name="serviceKey">The DI key the primitive was registered with.</param>
    IResilientPipelineBuilder<TKey, TValue> UseSingleflight(object serviceKey);

    /// <summary>
    /// Adds a <see cref="SingleflightLayer{TKey, TValue}"/> wrapping the
    /// supplied <paramref name="instance"/>. Bypasses DI - use for manual
    /// composition where DI registration is not appropriate.
    /// </summary>
    /// <param name="instance">The Singleflight instance.</param>
    IResilientPipelineBuilder<TKey, TValue> UseSingleflight(Singleflight<TKey, TValue> instance);

    /// <summary>
    /// Adds a <see cref="CircuitBreakerLayer{TKey, TValue}"/> resolved from
    /// DI via <paramref name="serviceKey"/>.
    /// </summary>
    /// <param name="serviceKey">The DI key the primitive was registered with.</param>
    IResilientPipelineBuilder<TKey, TValue> UseCircuitBreaker(object serviceKey);

    /// <summary>
    /// Adds a <see cref="CircuitBreakerLayer{TKey, TValue}"/> wrapping the
    /// supplied <paramref name="instance"/>. Bypasses DI.
    /// </summary>
    /// <param name="instance">The CircuitBreaker instance.</param>
    IResilientPipelineBuilder<TKey, TValue> UseCircuitBreaker(CircuitBreaker<TValue> instance);

    /// <summary>
    /// Adds a <see cref="RetryLayer{TKey, TValue}"/> with the supplied
    /// <paramref name="options"/> (or defaults if null). Retry has no DI
    /// primitives to resolve - its config is passed inline.
    /// </summary>
    /// <param name="options">Retry configuration; <c>null</c> = defaults.</param>
    IResilientPipelineBuilder<TKey, TValue> UseRetries(RetryOptions<TValue>? options = null);

    /// <summary>
    /// Adds a <see cref="TimeoutLayer{TKey, TValue}"/> at the current position in
    /// the pipeline. Call at <b>two positions</b> to apply separate total-request
    /// and per-attempt deadlines:
    /// <list type="number">
    ///   <item><description>
    ///     Before <c>UseRetries</c> - total-request timeout: bounds all retry
    ///     attempts combined.
    ///   </description></item>
    ///   <item><description>
    ///     After <c>UseRetries</c> (before <c>UseCircuitBreaker</c>) - per-attempt
    ///     timeout: bounds each individual attempt; a fired timeout surfaces as a
    ///     <see cref="TimeoutException"/> that the outer retry layer retries
    ///     (it is already classified transient by the default classifier).
    ///   </description></item>
    /// </list>
    /// Timeout has no DI primitive - its config is passed inline.
    /// </summary>
    /// <param name="options">Timeout configuration; <c>null</c> = defaults (10 s).</param>
    IResilientPipelineBuilder<TKey, TValue> UseTimeout(TimeoutOptions? options = null);

    /// <summary>
    /// Adds a <see cref="RateLimiterLayer{TKey, TValue}"/> resolved from DI via
    /// <paramref name="serviceKey"/>. Use this overload when the same
    /// <see cref="RateLimiting.RateLimiter"/> instance should be shared across
    /// multiple pipelines (e.g. a shared broker-level concurrency cap) - the DI
    /// container owns the limiter's lifetime and disposal.
    /// </summary>
    /// <param name="serviceKey">
    /// The DI key the <see cref="RateLimiting.RateLimiter"/> was registered with.
    /// </param>
    IResilientPipelineBuilder<TKey, TValue> UseRateLimiter(object serviceKey);

    /// <summary>
    /// Adds a <see cref="RateLimiterLayer{TKey, TValue}"/> wrapping an inline
    /// <see cref="RateLimiting.RateLimiter"/> constructed from
    /// <paramref name="options"/>. Use this overload when the concurrency limit is
    /// private to this pipeline. The layer owns the limiter; the
    /// <see cref="ResilientPipeline{TKey, TValue}"/> (which implements
    /// <see cref="IDisposable"/>) owns the layer and disposes it when the
    /// container tears down the pipeline singleton.
    /// </summary>
    /// <param name="options">
    /// Rate-limiter configuration; <c>null</c> = defaults (MaxConcurrency = 100,
    /// AcquisitionTimeout = Zero / reject-fast).
    /// </param>
    IResilientPipelineBuilder<TKey, TValue> UseRateLimiter(RateLimiterOptions? options = null);

    /// <summary>
    /// Snapshots the accumulated layers into a new
    /// <see cref="ResilientPipeline{TKey, TValue}"/>.
    /// </summary>
    ResilientPipeline<TKey, TValue> Build();
}
