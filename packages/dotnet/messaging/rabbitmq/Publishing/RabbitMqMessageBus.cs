// -----------------------------------------------------------------------
// <copyright file="RabbitMqMessageBus.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Publishing;

using System.Diagnostics;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Headers.Amqp;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Messaging.RabbitMq.Channels;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Encryption;
using DcsvIo.D2.Messaging.RabbitMq.Errors;
using DcsvIo.D2.Messaging.RabbitMq.Telemetry;
using DcsvIo.D2.Resilience.Retry;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Default <see cref="IMessageBus"/> implementation backed by
/// RabbitMQ.Client 7.x. Handles route resolution, body composition
/// (optional payload encryption), header construction, channel acquisition,
/// publisher confirms, and built-in transient-failure retry.
/// </summary>
/// <remarks>
/// <para>
/// Thread-safe: relies on <see cref="IChannelPool"/> (semaphore-bounded) for
/// concurrent channel access - multiple threads may call
/// <see cref="PublishAsync"/> simultaneously without external synchronization.
/// </para>
/// <para>
/// The wire body is only the serialized message, with no envelope wrapper.
/// Encryption protects the confidentiality of the message payload itself. The
/// W3C <c>traceparent</c> and <c>tracestate</c> AMQP headers carry cross-hop
/// trace correlation. A message that needs caller-identity, organization or
/// scope fields carries them in its typed message body.
/// </para>
/// </remarks>
internal sealed class RabbitMqMessageBus : IMessageBus
{
    private readonly IChannelPool r_channelPool;
    private readonly IServiceScopeFactory r_scopeFactory;
    private readonly ID2Connection r_connection;
    private readonly MessagingCatalog r_catalog;
    private readonly RabbitMqPublisherOptions r_options;
    private readonly ILogger<RabbitMqMessageBus> r_logger;

    /// <summary>Initializes the bus.</summary>
    /// <param name="channelPool">Bounded publisher channel pool.</param>
    /// <param name="scopeFactory">Per-publish DI scope factory. It resolves
    /// the per-domain <see cref="DcsvIo.D2.Encryption.IPayloadCrypto"/> through
    /// keyed services for encrypted message types, and
    /// <see cref="IRequestContext"/> for the propagated-context header. The
    /// bus is a singleton and every publish builds a transient scope that
    /// costs microseconds, so background hosted services can publish without
    /// creating a scope of their own.</param>
    /// <param name="connection">Underlying broker connection. Its
    /// <c>ReadyTask</c> backs <see cref="IMessageBus.WaitForReadyAsync"/>.</param>
    /// <param name="catalog">The message descriptors this host publishes
    /// against.</param>
    /// <param name="options">Transport-level publisher defaults.</param>
    /// <param name="logger">Logger.</param>
    public RabbitMqMessageBus(
        IChannelPool channelPool,
        IServiceScopeFactory scopeFactory,
        ID2Connection connection,
        MessagingCatalog catalog,
        IOptions<RabbitMqPublisherOptions> options,
        ILogger<RabbitMqMessageBus> logger)
    {
        ArgumentNullException.ThrowIfNull(channelPool);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        r_channelPool = channelPool;
        r_scopeFactory = scopeFactory;
        r_connection = connection;
        r_catalog = catalog;
        r_options = options.Value;
        r_logger = logger;

        if (r_options.MaxAttempts < 1)
        {
            throw new InvalidOperationException(
                $"RabbitMqPublisherOptions.MaxAttempts must be >= 1; "
                + $"was {r_options.MaxAttempts}.");
        }
    }

    /// <inheritdoc />
    public async ValueTask<D2Result> PublishAsync<TMessage>(
        TMessage message,
        PublisherOptions? options = null,
        CancellationToken ct = default)
        where TMessage : class
    {
        // Defensive null check: a null message returns a structured
        // ValidationFailed instead of a NullReferenceException deep inside
        // compose.
        if ((object?)message is null)
            return MessagingFailures.Required(nameof(message));

        // Resolve the message type's publisher contract from the registered
        // catalog via [MqPub]. Throws InvalidOperationException for missing
        // attribute / unknown constant / type-name mismatch. These are
        // programmer errors and not runtime conditions, so they surface
        // without a retry.
        var descriptor = MessageWireResolver.Resolve(r_catalog, typeof(TMessage));
        var route = ResolveRoute(descriptor, options);

        // Build a transient scope per publish. The bus is a singleton, so a
        // scope is needed to (a) resolve the keyed IPayloadCrypto and (b)
        // snapshot any IRequestContext registered by the caller's scope. The
        // scope costs microseconds, and the bus creates it whether or not a
        // scope is already active.
        await using var scope = r_scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // Compose the body once; the body bytes are reused across retry
        // attempts.
        var (body, kid) = EncryptedBodyComposer.Compose(message, descriptor, sp);

        // Stable per-publish message id, generated once so retry attempts
        // share it. With a new id per attempt, a retry of a publish that the
        // broker received but did not confirm would reach the consumer with a
        // fresh id and bypass the IMessageIdempotencyStore dedup window.
        var messageId = Guid.CreateVersion7().ToString();

        // Snapshot the propagated subset of IRequestContext (RequestId /
        // RequestPath / fingerprints / WhoIs hash) once. It is encoded as a
        // single base64url-of-JSON header value, so the same shape works on
        // every transport. Identity (UserId / OrgId / Scopes) is not
        // included; it is rebuilt at every hop from the JWT.
        var propagatedHeader = BuildPropagatedHeader(sp);

        var maxAttempts = options?.MaxAttempts ?? r_options.MaxAttempts;
        var waitForConfirm = options?.WaitForConfirm ?? r_options.WaitForConfirm;
        var confirmTimeout = options?.ConfirmTimeout ?? r_options.ConfirmTimeout;

        if (maxAttempts < 1)
        {
            return D2Result.ValidationFailed(messages: [TK.Common.Errors.UNKNOWN]);
        }

        using var activity = MessagingTelemetry.SR_ActivitySource.StartActivity(
            $"publish {route.Exchange}/{route.RoutingKey}",
            ActivityKind.Producer);
        activity?.SetTag(MessagingActivityTags.MESSAGING_SYSTEM, "rabbitmq");
        activity?.SetTag(MessagingActivityTags.MESSAGING_DESTINATION_NAME, route.Exchange);
        activity?.SetTag(MessagingActivityTags.MESSAGING_RABBITMQ_ROUTING_KEY, route.RoutingKey);
        activity?.SetTag(MessagingActivityTags.D2_MESSAGE_TYPE, typeof(TMessage).FullName);
        activity?.SetTag(MessagingActivityTags.D2_ENCRYPTION_KID, kid);
        activity?.SetTag(MessagingActivityTags.MESSAGING_MESSAGE_ID, messageId);
        activity?.SetTag(MessagingActivityTags.MESSAGING_OPERATION_TYPE, "publish");

        var stopwatch = Stopwatch.StartNew();
        var attemptsObserved = 0;

        try
        {
            await RetryHelper.RetryAsync(
                async (attempt, t) =>
                {
                    attemptsObserved = attempt;
                    MessagingTelemetry.SR_PublishesCounter.Add(1);

                    if (attempt > 1) MessagingTelemetry.SR_PublishRetriesCounter.Add(1);

                    MessagingLog.PublishAttempt(
                        r_logger,
                        route.Exchange,
                        route.RoutingKey,
                        typeof(TMessage).FullName ?? "<unknown>",
                        attempt,
                        maxAttempts,
                        kid);

                    try
                    {
                        await PublishOnceAsync(
                            route,
                            body,
                            kid,
                            messageId,
                            propagatedHeader,
                            typeof(TMessage),
                            waitForConfirm,
                            confirmTimeout,
                            t);
                    }
                    catch (PublishException pex) when (pex.IsReturn)
                    {
                        // Counted and logged here rather than in the transient
                        // predicate, because RetryHelper only consults that
                        // predicate while another attempt remains - the last
                        // returned attempt would go uncounted.
                        MessagingTelemetry.SR_PublishReturnsCounter.Add(1);
                        var returned = pex as PublishReturnException;
                        MessagingLog.PublishReturned(
                            r_logger,
                            route.Exchange,
                            route.RoutingKey,
                            attempt,
                            maxAttempts,
                            returned?.ReplyCode ?? 0,
                            returned?.ReplyText ?? string.Empty);
                        throw;
                    }

                    return true;
                },
                new RetryOptions<bool>
                {
                    MaxAttempts = maxAttempts,
                    BaseDelayMs = (int)r_options.BaseRetryDelay.TotalMilliseconds,
                    BackoffMultiplier = 2.0,
                    MaxDelayMs = (int)r_options.MaxRetryDelay.TotalMilliseconds,
                    IsTransient = ex =>
                    {
                        var transient = TransientPublishClassifier.IsTransient(ex);
                        if (transient && attemptsObserved < maxAttempts)
                        {
                            MessagingLog.PublishTransientFailure(
                                r_logger,
                                route.Exchange,
                                route.RoutingKey,
                                attemptsObserved,
                                maxAttempts,
                                ex.GetType().FullName ?? ex.GetType().Name);
                        }

                        return transient;
                    },
                },
                ct);

            stopwatch.Stop();
            MessagingTelemetry.SR_PublishDurationHistogram.Record(
                stopwatch.Elapsed.TotalMilliseconds);
            MessagingLog.PublishSucceeded(
                r_logger,
                route.Exchange,
                route.RoutingKey,
                typeof(TMessage).FullName ?? "<unknown>",
                attemptsObserved,
                stopwatch.Elapsed.TotalMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return D2Result.Ok();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, "canceled");
            return D2Result.Canceled();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // An unroutable publish and an unreachable broker are different
            // problems with different fixes, so this catch block returns
            // PublishUnroutable for the first and ServiceUnavailable for the
            // second.
            var unroutable = ex is PublishException { IsReturn: true };

            MessagingTelemetry.SR_PublishFailuresCounter.Add(
                1,
                new KeyValuePair<string, object?>(
                    MessagingMetricTags.OUTCOME,
                    unroutable
                        ? MessagingMetricTags.PUBLISH_FAIL_UNROUTABLE
                        : MessagingMetricTags.PUBLISH_FAIL_TRANSPORT));
            MessagingTelemetry.SR_PublishDurationHistogram.Record(
                stopwatch.Elapsed.TotalMilliseconds);

            if (unroutable)
            {
                MessagingLog.PublishUnroutableTerminal(
                    r_logger,
                    route.Exchange,
                    route.RoutingKey,
                    typeof(TMessage).FullName ?? "<unknown>",
                    attemptsObserved);
                activity?.SetStatus(ActivityStatusCode.Error, "unroutable");
                return MessagingTransportFailures.PublishUnroutable();
            }

            MessagingLog.PublishTerminalFailure(
                r_logger,
                route.Exchange,
                route.RoutingKey,
                typeof(TMessage).FullName ?? "<unknown>",
                attemptsObserved,
                ex.GetType().FullName ?? ex.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            return D2Result.ServiceUnavailable();
        }
    }

    /// <inheritdoc />
    public Task WaitForReadyAsync(CancellationToken ct = default) =>
        r_connection.ReadyTask.WaitAsync(ct);

    private static ExchangeRoute ResolveRoute(
        MqMessageDescriptor descriptor, PublisherOptions? perCall)
    {
        var exchange = perCall?.Exchange.ToNullIfEmpty() ?? descriptor.Exchange;
        var routingKey = perCall?.RoutingKey.ToNullIfEmpty()
            ?? descriptor.DefaultRoutingKey
            ?? string.Empty;
        return new ExchangeRoute(exchange, routingKey);
    }

    private static string? BuildPropagatedHeader(IServiceProvider sp)
    {
        // Resolve from the per-publish scope. If no IRequestContext is
        // registered (e.g. a process with no request-pipeline at all),
        // nothing to send.
        var ctx = sp.GetService<IRequestContext>();
        if (ctx is null) return null;

        var propagated = ctx.ToPropagatedContext();
        return propagated.HasAnyField ? PropagatedContextSerializer.Encode(propagated) : null;
    }

    private async ValueTask PublishOnceAsync(
        ExchangeRoute route,
        byte[] body,
        string? kid,
        string messageId,
        string? propagatedHeader,
        Type messageType,
        bool waitForConfirm,
        TimeSpan confirmTimeout,
        CancellationToken ct)
    {
        await using var lease = await r_channelPool.AcquireAsync(ct);

        var props = new BasicProperties
        {
            ContentType = "application/octet-stream",
            MessageId = messageId,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            DeliveryMode = DeliveryModes.Persistent,
            Headers = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [AmqpHeaders.PROTO_TYPE] = messageType.FullName,
            },
        };

        if (kid.Truthy()) props.Headers[AmqpHeaders.ENCRYPTION_KID] = kid;

        // Write the full W3C traceparent, not just the trace id, so the
        // receiving side can start a child span whose parent is this publish
        // span. Format:
        // {version}-{traceId}-{spanId}-{flags} per
        // https://www.w3.org/TR/trace-context.
        var current = Activity.Current;
        if (current is not null)
        {
            var flagsByte = (int)current.ActivityTraceFlags;
            props.Headers[AmqpHeaders.TRACEPARENT] =
                $"00-{current.TraceId}-{current.SpanId}-{flagsByte:x2}";
            var traceState = current.TraceStateString;
            if (traceState.Truthy())
                props.Headers[AmqpHeaders.TRACESTATE] = traceState;
        }

        if (propagatedHeader.Truthy())
            props.Headers[AmqpHeaders.PROPAGATED_CONTEXT] = propagatedHeader;

        // mandatory: the broker returns a message it cannot route instead of
        // dropping and confirming it, and confirmation tracking matches that
        // return back to this publish by its own sequence number, so it
        // surfaces below as a PublishException. See the README's
        // "Routing is not optional".
        if (!waitForConfirm)
        {
            await lease.Channel.BasicPublishAsync(
                exchange: route.Exchange,
                routingKey: route.RoutingKey,
                mandatory: true,
                basicProperties: props,
                body: body,
                cancellationToken: ct);
            return;
        }

        // BasicPublishAsync on a publisher-confirms-enabled channel returns
        // when the broker acks. Wrap the call with a linked
        // CancellationTokenSource for the confirm timeout, so a slow or hung
        // broker produces a TimeoutException (transient).
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(confirmTimeout);
        try
        {
            await lease.Channel.BasicPublishAsync(
                exchange: route.Exchange,
                routingKey: route.RoutingKey,
                mandatory: true,
                basicProperties: props,
                body: body,
                cancellationToken: linked.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            MessagingLog.PublishConfirmTimeout(
                r_logger,
                confirmTimeout.TotalMilliseconds,
                route.Exchange,
                route.RoutingKey);
            throw new TimeoutException(
                $"Publish confirm timed out after "
                + $"{confirmTimeout.TotalSeconds:F1}s.");
        }
    }
}
