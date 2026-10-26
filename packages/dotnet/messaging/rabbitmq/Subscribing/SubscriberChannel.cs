// -----------------------------------------------------------------------
// <copyright file="SubscriberChannel.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Subscribing;

using System.Diagnostics;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Headers.Amqp;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Encryption;
using DcsvIo.D2.Messaging.RabbitMq.Telemetry;
using DcsvIo.D2.Messaging.RabbitMq.Topology;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Diagnostics;
using DcsvIo.D2.Utilities.Extensions;
using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Events;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Owns one dedicated <see cref="IChannel"/> and one
/// <see cref="AsyncEventingBasicConsumer"/> for a single subscriber. Each
/// channel has its own prefetch and ack window, and channels are not shared
/// across subscribers, so a slow handler cannot stall another subscriber's
/// ack window.
/// </summary>
[MustDisposeResource(false)]
internal sealed class SubscriberChannel : IAsyncDisposable
{
    /// <summary>Hard cap on accepted message-id length. The publisher in this
    /// package emits a 36-char UUIDv7 string, while an attacker who controls a
    /// subscribed queue could send a 64KB id and bloat the idempotency-store
    /// key. A longer id is rejected, and the consumer then treats the message
    /// as having no id: it skips idempotency and still acks.</summary>
    private const int _MAX_MESSAGE_ID_LENGTH = 128;

    /// <summary>Bounded wait for in-flight callbacks to complete during
    /// disposal. After this timeout the channel is closed anyway, and a
    /// handler that is still running may see an
    /// <see cref="ObjectDisposedException"/> on its next ack or nack.</summary>
    private const int _DISPOSE_DRAIN_TIMEOUT_MS = 30_000;

    private readonly ID2Connection r_connection;
    private readonly IServiceScopeFactory r_scopeFactory;
    private readonly HandlerDispatcherFactory r_dispatcherFactory;
    private readonly MessagingCatalog r_catalog;
    private readonly ISubscriberRegistration r_registration;
    private readonly ILogger<SubscriberChannel> r_logger;
    private readonly DlqRepublisher r_dlqRepublisher;
    private readonly CancellationTokenSource r_shutdown = new();
    private readonly CancellationToken r_shutdownToken;

    private IChannel? _channel;
    private AsyncEventingBasicConsumer? _consumer;
    private string? _consumerTag;
    private bool _disposed;

    // Count of in-flight delivery callbacks. It prevents DisposeAsync from
    // closing the channel while a handler runs, which would surface as an
    // ObjectDisposedException on the BasicAck or BasicNack call. Disposal
    // unsubscribes and cancels first, then waits, bounded by a timeout, for
    // this count to reach zero.
    private int _inflightCallbacks;

    /// <summary>Initializes a subscriber channel.</summary>
    /// <param name="connection">Connection wrapper.</param>
    /// <param name="scopeFactory">Per-message DI scope factory.</param>
    /// <param name="dispatcherFactory">Closed-generic dispatchers per subscriber.</param>
    /// <param name="catalog">The message descriptors this host consumes against.</param>
    /// <param name="registration">The subscriber to host.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="connectionOptions">Configuration copied for dedicated DLQ transport.</param>
    /// <param name="subscriberOptions">Independent DLQ operation and cleanup bounds.</param>
    [MustDisposeResource(false)]
    public SubscriberChannel(
        ID2Connection connection,
        IServiceScopeFactory scopeFactory,
        HandlerDispatcherFactory dispatcherFactory,
        MessagingCatalog catalog,
        ISubscriberRegistration registration,
        ILogger<SubscriberChannel> logger,
        RabbitMqConnectionOptions connectionOptions,
        RabbitMqSubscriberOptions subscriberOptions)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(dispatcherFactory);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(logger);
        r_connection = connection;
        r_scopeFactory = scopeFactory;
        r_dispatcherFactory = dispatcherFactory;
        r_catalog = catalog;
        r_registration = registration;
        r_logger = logger;
        r_dlqRepublisher = new DlqRepublisher(
            connectionOptions, subscriberOptions, registration.Descriptor.Prefetch, logger);
        r_shutdownToken = r_shutdown.Token;
    }

    /// <summary>Gets or sets a test-only seam. When it is not null, it
    /// replaces the real <c>BasicAckAsync</c> call with the supplied
    /// delegate, so a test can inject an ack failure deterministically.
    /// Production code must not set it.</summary>
    internal static Func<IChannel, ulong, Task>? AckHookForTesting { get; set; }

    /// <summary>Gets the in-flight delivery callback count, read with a
    /// volatile read of <c>_inflightCallbacks</c>, so tests can assert the
    /// in-flight drain count.</summary>
    internal int InflightCallbackCountForTesting =>
        Volatile.Read(ref _inflightCallbacks);

    /// <summary>Opens a channel and starts consuming.</summary>
    /// <param name="ct">Cancellation token.</param>
    public async ValueTask StartAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var descriptor = r_registration.Descriptor;
        var queueName = r_registration.ResolvedQueueName;
        var prefetch = (ushort)Math.Min(ushort.MaxValue, descriptor.Prefetch);

        _channel = await r_connection.CreateChannelAsync(options: null, ct);

        // A broker-closed consumer channel is otherwise silent: the only symptom is that
        // deliveries stop. Automatic recovery re-attaches the consumer, so the shutdown is
        // recoverable, but it must be visible when it happens.
        _channel.ChannelShutdownAsync += (_, e) =>
        {
            // An Application initiator is this host closing the channel on shutdown.
            // Peer and Library initiators are the silent ones - the consumer stops
            // receiving and nothing else says why.
            if (e.Initiator != ShutdownInitiator.Application)
            {
                SubscriberLog.ConsumerChannelShutdown(
                    r_logger, queueName, e.Initiator.ToString(), e.ReplyCode, e.ReplyText);
            }

            return Task.CompletedTask;
        };

        try
        {
            await _channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: prefetch,
                global: false,
                cancellationToken: ct);

            // FanoutExclusiveAutoDelete: ensure the queue and its binding exist on the
            // long-lived consumer channel before BasicConsume. Topology declare uses a
            // short-lived channel; under load a NOT_FOUND window can still appear between
            // declare and consume. Args must match DefaultTopologyDeclarer (via QueueFlagsFor
            // and the DLX args). Exclusive queues are deleted when their declaring connection
            // closes, not merely when a declare channel is disposed, so this re-declare
            // exists for consume-channel readiness and arg parity, and not because disposing
            // a channel deleted the queue.
            if (descriptor.Pattern == QueuePattern.FanoutExclusiveAutoDelete)
                await EnsureExclusiveQueueOnConsumerChannelAsync(_channel, queueName, ct);

            _consumer = new AsyncEventingBasicConsumer(_channel);
            _consumer.ReceivedAsync += OnReceivedAsync;

            _consumerTag = await _channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumerTag: string.Empty,
                noLocal: false,
                exclusive: false,
                arguments: null,
                consumer: _consumer,
                cancellationToken: ct);
        }
        catch
        {
            // If QoS, the exclusive re-declare or BasicConsume throws after the channel
            // is assigned, dispose the orphan channel: a channel whose start failed has
            // no owner, and leaving it open would leak a broker channel.
            var orphan = _channel;
            _channel = null;
            _consumer = null;
            _consumerTag = null;

            if (orphan is not null)
            {
                try
                {
                    await orphan.DisposeAsync();
                }
                catch
                {
                    // Best-effort; StartAsync is already failing.
                }
            }

            throw;
        }

        SubscriberLog.ConsumerStarted(
            r_logger,
            queueName,
            prefetch,
            descriptor.Pattern.ToString(),
            r_registration.HandlerType.Name);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;
        await r_shutdown.CancelAsync();
        await r_dlqRepublisher.DisposeAsync();

        SubscriberLog.ConsumerStopping(r_logger, r_registration.ResolvedQueueName);

        if (_consumer is not null)
            _consumer.ReceivedAsync -= OnReceivedAsync;

        if (_channel is not null)
        {
            try
            {
                if (_consumerTag.Truthy())
                {
                    await _channel.BasicCancelAsync(
                        _consumerTag!,
                        noWait: false,
                        cancellationToken: default);
                }
            }
            catch
            {
                // Best-effort during shutdown - broker may already be gone.
            }

            // Drain in-flight callbacks before closing the channel. The wait is
            // bounded by _DISPOSE_DRAIN_TIMEOUT_MS: a slow handler cannot hold
            // the host shutdown indefinitely, and a handler that finishes in
            // time can ack its message cleanly.
            await DrainInflightCallbacksAsync();

            try
            {
                if (_channel.IsOpen) await _channel.CloseAsync();
            }
            catch
            {
                // Best-effort.
            }

            await _channel.DisposeAsync();
        }

        r_shutdown.Dispose();
    }

    /// <summary>Counts redelivery attempts via the broker's <c>x-death</c>
    /// header.</summary>
    /// <remarks>RabbitMQ pushes one entry per (queue, reason) pair the message
    /// has cycled through; the <c>count</c> field on an entry increments each
    /// additional cycle. This method sums <c>count</c> across entries whose
    /// <c>reason</c> reflects a retry-cycle event:
    /// <list type="bullet">
    ///   <item><c>expired</c> - retry-tier TTL expiry (our retry path).</item>
    ///   <item><c>rejected</c> - consumer NACK without requeue.</item>
    /// </list>
    /// Other reasons (<c>maxlen</c>, <c>delivery_limit</c>) are broker-side
    /// flow control, not consumer-side retries - counting them would
    /// trigger <c>RETRIES_EXHAUSTED</c> prematurely. Returns zero on first
    /// delivery / malformed header / missing field. It fails open, so a broker
    /// quirk cannot keep a message in retry forever.</remarks>
    /// <param name="ea">The delivery event args carrying broker headers.</param>
    internal static int ReadAttemptCount(BasicDeliverEventArgs ea)
    {
        if (ea.BasicProperties.Headers is null) return 0;
        if (!ea.BasicProperties.Headers.TryGetValue("x-death", out var raw)) return 0;
        if (raw is not IList<object?> entries) return 0;

        var total = 0;
        foreach (var entry in entries)
        {
            if (entry is not IDictionary<string, object?> dict) continue;

            // Filter on reason - only retry-cycle events count.
            if (!dict.TryGetValue("reason", out var reasonObj)) continue;
            var reason = reasonObj switch
            {
                string s => s,
                byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
                _ => null,
            };
            if (reason is not ("expired" or "rejected")) continue;

            if (!dict.TryGetValue("count", out var countObj)) continue;

            // x-death "count" is broker-emitted as long.
            var count = countObj switch
            {
                long l => (int)l,
                int i => i,
                _ => 0,
            };
            total += count;
        }

        return total;
    }

    /// <summary>Settles only the original delivery channel after the dead-letter outcome.</summary>
    /// <param name="channel">The original consumer channel.</param>
    /// <param name="confirmed">Whether the DLQ publication was confirmed.</param>
    /// <param name="deliveryTag">The original channel's delivery tag.</param>
    /// <param name="queue">The queue name for diagnostics.</param>
    /// <param name="messageId">The validated processing identity.</param>
    /// <param name="logger">Sanitized settlement diagnostics.</param>
    internal static async ValueTask SettleDlqDeliveryAsync(
        IChannel channel,
        bool confirmed,
        ulong deliveryTag,
        string queue,
        string? messageId,
        ILogger logger)
    {
        try
        {
            if (!channel.IsOpen) return;

            if (confirmed) await channel.BasicAckAsync(deliveryTag, multiple: false);
            else await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false);
        }
        catch (Exception ex)
        {
            try
            {
                SubscriberLog.DlqSettlementFailed(
                    logger,
                    queue,
                    deliveryTag,
                    messageId,
                    SanitizedExceptionRender.TypeName(ex),
                    SanitizedExceptionRender.FirstFrame(ex));
            }
            catch (Exception)
            {
                // Diagnostic failures must not restart dead-letter publication after settlement.
            }
        }
    }

    private static string? ReadMessageId(BasicDeliverEventArgs ea)
    {
        var props = ea.BasicProperties;
        var raw = ReadFromPropertiesOrHeader(props);
        if (raw is null) return null;

        // Reject lengths above the cap (protection against key injection and
        // Redis memory exhaustion) and any control character, ":" or space,
        // which could split the idempotency store's key namespace.
        if (raw.Length > _MAX_MESSAGE_ID_LENGTH) return null;
        foreach (var ch in raw)
        {
            if (char.IsControl(ch) || ch == ':' || ch == ' ')
                return null;
        }

        return raw;
    }

    private static string? ReadFromPropertiesOrHeader(IReadOnlyBasicProperties props)
    {
        if (!props.MessageId.Falsey()) return props.MessageId;

        if (props.Headers is not null
            && props.Headers.TryGetValue(AmqpHeaders.MESSAGE_ID, out var raw)
            && raw is byte[] bytes)
        {
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        return null;
    }

    private static Dictionary<string, object?> BuildRepublishHeaders(
        IReadOnlyBasicProperties sourceProps, byte[] failureHeader)
    {
        var headers = new Dictionary<string, object?>(StringComparer.Ordinal);

        // Carry forward producer-set headers (traceparent, x-d2-context,
        // etc.) so DLQ inspection sees the same context as a normal delivery.
        if (sourceProps.Headers is not null)
        {
            foreach (var kvp in sourceProps.Headers)
                headers[kvp.Key] = kvp.Value;
        }

        // Overwrite (or set) the failure-reason header, which is the purpose
        // of the republish path.
        headers[AmqpHeaders.FAILURE_REASON] = failureHeader;
        return headers;
    }

    private static PropagatedContext? ReadPropagatedContext(BasicDeliverEventArgs ea)
    {
        if (ea.BasicProperties.Headers is null) return null;
        if (!ea.BasicProperties.Headers.TryGetValue(AmqpHeaders.PROPAGATED_CONTEXT, out var raw)
            || raw is not byte[] bytes)
        {
            return null;
        }

        var encoded = System.Text.Encoding.UTF8.GetString(bytes);
        return PropagatedContextSerializer.TryDecode(encoded);
    }

    private static ActivityContext ReadTraceContext(BasicDeliverEventArgs ea)
    {
        if (ea.BasicProperties.Headers is null) return default;

        string? traceparent = null;
        string? tracestate = null;
        if (ea.BasicProperties.Headers.TryGetValue(AmqpHeaders.TRACEPARENT, out var tpRaw)
            && tpRaw is byte[] tpBytes)
        {
            traceparent = System.Text.Encoding.UTF8.GetString(tpBytes);
        }

        if (ea.BasicProperties.Headers.TryGetValue(AmqpHeaders.TRACESTATE, out var tsRaw)
            && tsRaw is byte[] tsBytes)
        {
            tracestate = System.Text.Encoding.UTF8.GetString(tsBytes);
        }

        if (traceparent is null) return default;

        // ActivityContext.TryParse handles full traceparent string format
        // (00-{traceId}-{spanId}-{flags}). On failure (a malformed header)
        // the parent context is dropped silently, and the consume span starts
        // a new trace instead of crashing on a forged header.
        return ActivityContext.TryParse(traceparent, tracestate, out var ctx)
            ? ctx
            : default;
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        // Track the in-flight callback count so DisposeAsync can drain
        // in-flight handlers before closing the channel. Increment before the
        // work, and decrement in finally so any throw still releases the slot.
        Interlocked.Increment(ref _inflightCallbacks);
        try
        {
            var consumerChannel = ((AsyncEventingBasicConsumer)sender).Channel;
            var original = new DlqRepublishRequest(
                DlqNaming.DlxFor(r_registration.ResolvedQueueName),
                ea.Body.ToArray(),
                BuildRepublishHeaders(ea.BasicProperties, []),
                ea.BasicProperties.MessageId,
                ea.BasicProperties.ContentType,
                ea.BasicProperties.ContentEncoding,
                ea.BasicProperties.CorrelationId);
            await OnReceivedCoreAsync(ea, consumerChannel, original);
        }
        finally
        {
            Interlocked.Decrement(ref _inflightCallbacks);
        }
    }

    private async Task OnReceivedCoreAsync(
        BasicDeliverEventArgs ea, IChannel consumerChannel, DlqRepublishRequest original)
    {
        var queue = r_registration.ResolvedQueueName;
        var deliveryTag = ea.DeliveryTag;
        var stopwatch = Stopwatch.StartNew();
        var messageId = ReadMessageId(ea);

        // Start a Consumer-kind span linked to the producer's publish span
        // via traceparent. Activity.Current is set inside the using block,
        // so SubscriberLog, handler dispatch and the DLQ failure header all
        // see the consume span as their ambient activity.
        var parentContext = ReadTraceContext(ea);
        var activityName = $"receive {queue}";
        using var activity = MessagingTelemetry.SR_ActivitySource.StartActivity(
            activityName,
            kind: ActivityKind.Consumer,
            parentContext: parentContext);

        if (activity is not null)
        {
            activity.SetTag(MessagingActivityTags.MESSAGING_SYSTEM, "rabbitmq");
            activity.SetTag(MessagingActivityTags.MESSAGING_DESTINATION_NAME, queue);

            // The OpenTelemetry semantic-convention attribute is
            // "messaging.operation.type", not "messaging.operation". Driving
            // the tag names from the spec prevents drift from the standard.
            activity.SetTag(MessagingActivityTags.MESSAGING_OPERATION_TYPE, "receive");
            activity.SetTag(MessagingActivityTags.MESSAGING_MESSAGE_ID, messageId);
            activity.SetTag(MessagingActivityTags.MESSAGING_RABBITMQ_DELIVERY_TAG, deliveryTag);
            activity.SetTag(MessagingActivityTags.MESSAGING_RABBITMQ_REDELIVERED, ea.Redelivered);
        }

        SubscriberLog.DeliveryReceived(
            r_logger, queue, deliveryTag, ea.Redelivered, messageId);

        try
        {
            await using var scope = r_scopeFactory.CreateAsyncScope();

            // Apply the producer-side propagated context (RequestId,
            // RequestPath, fingerprints, WhoIs hash) onto the per-message
            // scope's MutableRequestContext before the handler resolves its
            // IRequestContext through HandlerContext. Identity (UserId /
            // OrgId / Scopes) is not in this header. In a synchronous hop it
            // is rebuilt from a JWT, but an async event has no JWT, so the
            // consumer-side handler must not claim caller identity.
            var propagated = ReadPropagatedContext(ea);
            if (propagated is not null)
            {
                var mutableCtx = scope.ServiceProvider.GetService<MutableRequestContext>();
                mutableCtx?.ApplyPropagatedContext(propagated);
            }

            // Optional idempotency pre-check - opt-in per subscriber.
            if (r_registration.Descriptor.Idempotency && messageId.Truthy())
            {
                var store = scope.ServiceProvider
                    .GetService<IMessageIdempotencyStore>();
                if (store is not null)
                {
                    var seen = await store.HasSeenAsync(messageId!);
                    if (seen is { Failed: false, Data: true })
                    {
                        // Already processed - ack without re-doing the work.
                        await consumerChannel.BasicAckAsync(deliveryTag, multiple: false);
                        SubscriberLog.DeliverySkipped(
                            r_logger, queue, deliveryTag, messageId);
                        return;
                    }

                    // When the store returns ServiceUnavailable, fail open and
                    // process the message: a duplicate is better than a
                    // rejection during a Redis outage, and handlers must be
                    // safe under at-least-once delivery anyway.
                }
            }

            // Tiered-retry attempt-count enforcement. When the subscription
            // declares a TieredRetry block and the broker's x-death history
            // shows that the attempt budget is used up, route the message
            // straight to the DLQ with cause=RETRIES_EXHAUSTED, so a
            // permanently broken payload does not start another tier-bounce
            // cycle.
            if (r_registration.Descriptor.TieredRetry is { } retry)
            {
                var attemptCount = ReadAttemptCount(ea);
                if (attemptCount >= retry.MaxAttempts)
                {
                    await NackRetriesExhaustedAsync(
                        ea, attemptCount, messageId, consumerChannel, original);
                    SubscriberLog.RetriesExhausted(
                        r_logger, queue, messageId, attemptCount, retry.MaxAttempts);
                    return;
                }
            }

            var dispatcher = r_dispatcherFactory.GetForQueue(queue);
            D2Result result;
            try
            {
                result = await dispatcher.DispatchAsync(scope.ServiceProvider, ea.Body, default);
            }
            catch (MessageBodyDecodeException dex)
            {
                var cause = dex.InnerException is System.Text.Json.JsonException
                    ? DlqFailureCauses.DESERIALIZE_FAILURE
                    : DlqFailureCauses.DECRYPT_FAILURE;
                var rootEx = dex.InnerException ?? dex;
                await NackToDlqAsync(ea, cause, rootEx, messageId, consumerChannel, original);
                SubscriberLog.BoundaryFailure(
                    r_logger,
                    queue,
                    cause,
                    messageId,
                    SanitizedExceptionRender.TypeName(rootEx),
                    SanitizedExceptionRender.FirstFrame(rootEx));
                return;
            }
            catch (Exception ex)
            {
                await NackHandlerExceptionAsync(ea, ex, messageId, consumerChannel, original);
                SubscriberLog.HandlerThrew(
                    r_logger,
                    queue,
                    messageId,
                    SanitizedExceptionRender.TypeName(ex),
                    SanitizedExceptionRender.FirstFrame(ex));
                return;
            }

            if (result.Failed)
            {
                await NackHandlerResultAsync(ea, result, messageId, consumerChannel, original);
                SubscriberLog.HandlerResultFailure(r_logger, queue, result.ErrorCode);
                return;
            }

            // On success, record the idempotency mark before the ack. A crash
            // between the mark and the ack causes a safe redelivery that the
            // idempotency check skips. Acking first would leave a window where
            // a crash before the mark causes a duplicate that the handler must
            // defend against itself.
            if (r_registration.Descriptor.Idempotency && messageId.Truthy())
            {
                var store = scope.ServiceProvider
                    .GetService<IMessageIdempotencyStore>();
                if (store is not null)
                {
                    var markResult = await store.MarkSeenAsync(messageId!);
                    if (markResult.Failed)
                    {
                        // The mark failed (a Redis or store outage). Acking now
                        // would leave the dedup window unguarded for this
                        // message, and a redelivery would re-run the handler.
                        // NACK without requeue routes the message to the DLQ
                        // via x-dead-letter-exchange, so an operator sees the
                        // store-degradation impact instead of duplicates
                        // being processed silently.
                        MessagingTelemetry.SR_AckFailuresCounter.Add(1);
                        SubscriberLog.IdempotencyMarkFailed(
                            r_logger,
                            queue,
                            deliveryTag,
                            messageId,
                            markResult.ErrorCode);
                        await consumerChannel.BasicNackAsync(
                            deliveryTag, multiple: false, requeue: false);
                        return;
                    }
                }
            }

            // An ack failure is not a handler failure. The catch is narrowed
            // to the BasicAckAsync call so that a broker-side ack rejection
            // (channel closed mid-flight, etc.) is not reported as
            // HANDLER_EXCEPTION and does not route the already-processed
            // message to the DLQ. The code does not requeue: the broker's
            // at-least-once semantics redeliver the message on reconnect, and
            // the idempotency mark written above keeps the duplicate from
            // re-running the handler.
            try
            {
                if (AckHookForTesting is { } hook)
                    await hook(consumerChannel, deliveryTag);
                else
                    await consumerChannel.BasicAckAsync(deliveryTag, multiple: false);
            }
            catch (Exception ackEx)
            {
                MessagingTelemetry.SR_AckFailuresCounter.Add(1);
                SubscriberLog.AckFailed(
                    r_logger,
                    queue,
                    deliveryTag,
                    messageId,
                    SanitizedExceptionRender.TypeName(ackEx),
                    SanitizedExceptionRender.FirstFrame(ackEx));
                return;
            }

            stopwatch.Stop();
            SubscriberLog.DeliveryAcked(
                r_logger, queue, deliveryTag, stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            // Defensive: any other exception is handled as a handler
            // exception. The message is republished to the DLX with a failure
            // header and acked, or, when that republish fails, nacked without
            // requeue so the broker routes it to the DLQ.
            await NackHandlerExceptionAsync(ea, ex, messageId, consumerChannel, original);
            SubscriberLog.BoundaryFailure(
                r_logger,
                queue,
                "UNEXPECTED",
                messageId,
                SanitizedExceptionRender.TypeName(ex),
                SanitizedExceptionRender.FirstFrame(ex));
        }
    }

    private async Task NackToDlqAsync(
        BasicDeliverEventArgs ea,
        string cause,
        Exception ex,
        string? messageId,
        IChannel consumerChannel,
        DlqRepublishRequest original)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        var headerBytes = DlqFailureHeaderBuilder.FromBoundary(cause, ex, traceId);
        await PublishFailureHeaderAsync(ea, headerBytes, messageId, consumerChannel, original);
    }

    private async Task NackHandlerExceptionAsync(
        BasicDeliverEventArgs ea,
        Exception ex,
        string? messageId,
        IChannel consumerChannel,
        DlqRepublishRequest original)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        var headerBytes = DlqFailureHeaderBuilder.FromException(ex, traceId: traceId);
        await PublishFailureHeaderAsync(ea, headerBytes, messageId, consumerChannel, original);
    }

    private async Task NackHandlerResultAsync(
        BasicDeliverEventArgs ea,
        D2Result result,
        string? messageId,
        IChannel consumerChannel,
        DlqRepublishRequest original)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        var headerBytes = DlqFailureHeaderBuilder.FromResult(result, traceId: traceId);
        await PublishFailureHeaderAsync(ea, headerBytes, messageId, consumerChannel, original);
    }

    private async Task NackRetriesExhaustedAsync(
        BasicDeliverEventArgs ea,
        int attemptCount,
        string? messageId,
        IChannel consumerChannel,
        DlqRepublishRequest original)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        var headerBytes = DlqFailureHeaderBuilder.FromRetriesExhausted(
            attemptCount, traceId: traceId);
        await PublishFailureHeaderAsync(ea, headerBytes, messageId, consumerChannel, original);
    }

    private async Task PublishFailureHeaderAsync(
        BasicDeliverEventArgs ea,
        byte[] failureHeader,
        string? messageId,
        IChannel consumerChannel,
        DlqRepublishRequest original)
    {
        // RabbitMQ's BasicNack cannot attach headers to the dead-lettered
        // message directly. This method republishes the same body to the
        // queue's DLX with the failure header attached, then calls BasicAck
        // on the original delivery, so the broker's x-dead-letter-exchange
        // routing does not also fire (which would put a header-less
        // duplicate in the DLQ).
        var queue = r_registration.ResolvedQueueName;
        var dlxName = DlqNaming.DlxFor(queue);
        var deliveryTag = ea.DeliveryTag;

        var headers = new Dictionary<string, object?>(original.Headers)
        {
            [AmqpHeaders.FAILURE_REASON] = failureHeader,
        };
        var outcome = await r_dlqRepublisher.RepublishAsync(
            original with { Headers = headers }, r_shutdownToken);
        if (outcome == DlqRepublishOutcome.Confirmed)
        {
            await SettleDlqDeliveryAsync(
                consumerChannel, true, deliveryTag, queue, messageId, r_logger);
        }
        else
        {
            // The republish failed, so fall back to BasicNack without requeue:
            // the broker's x-dead-letter-exchange then routes the message to
            // the DLQ without the failure header, which is better than losing
            // it. The fallback cannot save a returned republish, because the
            // broker's own dead-lettering targets the same exchange. The outcome
            // tag distinguishes that case from a transport failure.
            var unroutable = outcome == DlqRepublishOutcome.Unroutable;
            MessagingTelemetry.SR_DlqRepublishFailuresCounter.Add(
                1,
                new KeyValuePair<string, object?>(
                    MessagingMetricTags.OUTCOME,
                    unroutable
                        ? MessagingMetricTags.REPUBLISH_FAIL_UNROUTABLE
                        : MessagingMetricTags.REPUBLISH_FAIL_TRANSPORT));
            SubscriberLog.DlqRepublishFailed(
                r_logger,
                queue,
                dlxName,
                deliveryTag,
                messageId,
                outcome.ToString());
            await SettleDlqDeliveryAsync(
                consumerChannel, false, deliveryTag, queue, messageId, r_logger);
        }
    }

    private async Task DrainInflightCallbacksAsync()
    {
        // Poll with a bounded timeout. Channel and consumer pumps are
        // cooperative: once BasicCancel returns, the broker stops pushing
        // new deliveries, so the loop only waits for already-pushed ones to
        // finish their handler dispatch.
        var deadline = Environment.TickCount64 + _DISPOSE_DRAIN_TIMEOUT_MS;
        while (Volatile.Read(ref _inflightCallbacks) > 0)
        {
            if (Environment.TickCount64 >= deadline)
            {
                SubscriberLog.DisposeDrainTimeout(
                    r_logger,
                    r_registration.ResolvedQueueName,
                    Volatile.Read(ref _inflightCallbacks),
                    _DISPOSE_DRAIN_TIMEOUT_MS);
                return;
            }

            await Task.Delay(50);
        }
    }

    /// <summary>
    /// Idempotent exclusive-queue declare and bind on the consumer channel. Declare order and
    /// flags match <see cref="DefaultTopologyDeclarer"/> for
    /// <see cref="QueuePattern.FanoutExclusiveAutoDelete"/> (DLX before main queue; flags via
    /// <see cref="DefaultTopologyDeclarer.QueueFlagsFor"/>).
    /// </summary>
    private async ValueTask EnsureExclusiveQueueOnConsumerChannelAsync(
        IChannel channel, string queueName, CancellationToken ct)
    {
        var descriptor = r_registration.Descriptor;
        var wire = MessageWireResolver.Resolve(r_catalog, r_registration.MessageType);
        var dlxName = DlqNaming.DlxFor(queueName);
        var dlqName = DlqNaming.DlqFor(queueName);

        await channel.ExchangeDeclareAsync(
            exchange: wire.Exchange,
            type: wire.ExchangeType,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        // DLX before main queue so x-dead-letter-exchange arg always references a declared
        // exchange (same order as DefaultTopologyDeclarer).
        await channel.ExchangeDeclareAsync(
            exchange: dlxName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        await channel.QueueDeclareAsync(
            queue: dlqName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: dlqName,
            exchange: dlxName,
            routingKey: string.Empty,
            arguments: null,
            cancellationToken: ct);

        var queueArgs = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["x-dead-letter-exchange"] = dlxName,
            ["x-dead-letter-routing-key"] = string.Empty,
        };

        var (durable, exclusive, autoDelete) =
            DefaultTopologyDeclarer.QueueFlagsFor(QueuePattern.FanoutExclusiveAutoDelete);

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: durable,
            exclusive: exclusive,
            autoDelete: autoDelete,
            arguments: queueArgs,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: wire.Exchange,
            routingKey: descriptor.RoutingKeyBinding,
            arguments: null,
            cancellationToken: ct);
    }
}
