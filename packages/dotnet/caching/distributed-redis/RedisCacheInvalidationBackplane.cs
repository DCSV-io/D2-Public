// -----------------------------------------------------------------------
// <copyright file="RedisCacheInvalidationBackplane.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching.Distributed.Redis;

using System.Collections.Concurrent;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

/// <summary>
/// Redis pub/sub-backed implementation of
/// <see cref="ICacheInvalidationBackplane"/>. Subscribes to a single
/// channel (<see cref="RedisCacheOptions.InvalidationChannel"/>) at
/// construction and dispatches incoming messages to every subscribed
/// handler. Each handler is started without waiting for the others to
/// finish; exceptions are isolated (one throwing handler doesn't break
/// delivery to others).
/// </summary>
/// <remarks>
/// Every subscriber acts on every message: there is no sender-ID filtering,
/// so publishers receive their own messages. The cost is bounded - see the
/// abstraction's XML doc and the library README for the full semantics.
/// </remarks>
[MustDisposeResource(false)]
public sealed class RedisCacheInvalidationBackplane : ICacheInvalidationBackplane
{
    private readonly IConnectionMultiplexer r_redis;
    private readonly RedisCacheOptions r_options;
    private readonly ILogger<RedisCacheInvalidationBackplane> r_logger;
    private readonly ConcurrentDictionary<Guid, Subscription> r_subscriptions = new();
    private bool _disposed;

    /// <summary>Initializes a new <see cref="RedisCacheInvalidationBackplane"/>.</summary>
    /// <param name="redis">Redis connection multiplexer (singleton).</param>
    /// <param name="options">Cache options.</param>
    /// <param name="logger">Logger.</param>
    [MustDisposeResource(false)]
    public RedisCacheInvalidationBackplane(
        IConnectionMultiplexer redis,
        IOptions<RedisCacheOptions> options,
        ILogger<RedisCacheInvalidationBackplane> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        r_redis = redis;
        r_options = options.Value;
        r_logger = logger;

        // Subscribe to the channel once for the lifetime of this backplane
        // instance. Single Redis subscription dispatches to all handlers.
        var sub = r_redis.GetSubscriber();
        sub.Subscribe(
            RedisChannel.Literal(r_options.InvalidationChannel),
            (_, value) => OnMessageReceived(value!));
    }

    /// <inheritdoc />
    [MustDisposeResource]
    public IAsyncDisposable Subscribe(Func<string, CancellationToken, ValueTask> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ThrowIfDisposed();
        var id = Guid.NewGuid();
        var sub = new Subscription(id, handler, this);
        r_subscriptions[id] = sub;
        return sub;
    }

    /// <inheritdoc />
    public async ValueTask<D2Result> PublishInvalidationAsync(
        string key, CancellationToken ct = default)
    {
        if (key.Falsey()) return InputFailures.Required(nameof(key));

        // A disposed backplane refuses the publish. The invalidation then reaches no other
        // instance, whose L1 copy stays in use until its local time to live ends, and the
        // caller receives ServiceUnavailable instead of a result that reports it as sent.
        if (_disposed) return D2Result.ServiceUnavailable();

        try
        {
            await r_redis.GetSubscriber().PublishAsync(
                RedisChannel.Literal(r_options.InvalidationChannel),
                key);
            return D2Result.Ok();
        }
        catch (RedisException ex)
        {
            RedisCacheLog.RedisOpFailed(r_logger, "Backplane.Publish", ex.GetType().Name, key);
            return D2Result.ServiceUnavailable();
        }
    }

    /// <inheritdoc />
    public async ValueTask<D2Result> PublishInvalidationManyAsync(
        IReadOnlyCollection<string> keys, CancellationToken ct = default)
    {
        if (keys.Falsey()) return InputFailures.Required(nameof(keys));

        if (_disposed) return D2Result.ServiceUnavailable();

        try
        {
            var sub = r_redis.GetSubscriber();
            var channel = RedisChannel.Literal(r_options.InvalidationChannel);
            var tasks = new List<Task<long>>(keys.Count);
            foreach (var key in keys)
                tasks.Add(sub.PublishAsync(channel, key));
            await Task.WhenAll(tasks);
            return D2Result.Ok();
        }
        catch (RedisException ex)
        {
            RedisCacheLog.RedisOpFailed(
                r_logger, "Backplane.PublishMany", ex.GetType().Name, $"{keys.Count} keys");
            return D2Result.ServiceUnavailable();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Unsubscribe from the Redis channel so no more messages arrive.
        try
        {
            await r_redis.GetSubscriber().UnsubscribeAsync(
                RedisChannel.Literal(r_options.InvalidationChannel));
        }
        catch (RedisException)
        {
            // Best effort on shutdown; the exception is ignored.
        }

        // Cancel any in-flight subscription work.
        foreach (var sub in r_subscriptions.Values)
            sub.SignalCancellation();
        r_subscriptions.Clear();
    }

    private void OnMessageReceived(string key)
    {
        // Dispatch to every active subscription. Each handler is started
        // with try/catch isolation and is not awaited here; this runs on
        // the StackExchange.Redis dispatch thread, and Redis pub/sub applies
        // no backpressure to it.
        foreach (var sub in r_subscriptions.Values)
        {
            // Fire-and-forget per subscription, with isolation, so a slow
            // handler does not block the Redis dispatch thread.
            _ = InvokeHandlerAsync(sub, key);
        }
    }

    private async Task InvokeHandlerAsync(Subscription sub, string key)
    {
        try
        {
            await sub.Handler(key, sub.Token);
        }
        catch (OperationCanceledException) when (sub.Token.IsCancellationRequested)
        {
            // Subscription was disposed mid-handler; expected.
        }
        catch (Exception ex)
        {
            RedisCacheLog.BackplaneHandlerThrew(r_logger, ex.GetType().Name, key);
        }
    }

    private void Unsubscribe(Guid id) => r_subscriptions.TryRemove(id, out _);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    [MustDisposeResource(false)]
    private sealed class Subscription : IAsyncDisposable
    {
        private readonly RedisCacheInvalidationBackplane r_backplane;
        private readonly CancellationTokenSource r_cts = new();
        private bool _disposed;

        [MustDisposeResource(false)]
        internal Subscription(
            Guid id,
            Func<string, CancellationToken, ValueTask> handler,
            RedisCacheInvalidationBackplane backplane)
        {
            Id = id;
            Handler = handler;
            r_backplane = backplane;
        }

        internal Func<string, CancellationToken, ValueTask> Handler { get; }

        internal CancellationToken Token => r_cts.Token;

        private Guid Id { get; }

        public ValueTask DisposeAsync()
        {
            if (_disposed) return ValueTask.CompletedTask;
            _disposed = true;
            r_backplane.Unsubscribe(Id);
            r_cts.Cancel();
            r_cts.Dispose();
            return ValueTask.CompletedTask;
        }

        internal void SignalCancellation()
        {
            if (_disposed) return;
            r_cts.Cancel();
        }
    }
}
