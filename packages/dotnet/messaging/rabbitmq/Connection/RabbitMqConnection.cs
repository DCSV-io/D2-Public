// -----------------------------------------------------------------------
// <copyright file="RabbitMqConnection.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Connection;

using global::RabbitMQ.Client;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Default <see cref="ID2Connection"/> implementation. Holds the shared
/// <see cref="IConnection"/> for ordinary publishing and consumption.
/// DLQ republishing owns separate connections.
/// </summary>
/// <remarks>
/// <para>
/// Opens through a background reconnect loop started at host startup. The
/// loop retries forever with exponential backoff, so a broker that is
/// temporarily unreachable does not stop the host.
/// </para>
/// <para>
/// Once a connection is established, RabbitMQ.Client 7.x's
/// <c>AutomaticRecoveryEnabled = true</c> and <c>TopologyRecoveryEnabled =
/// true</c> handle in-flight reconnection (re-declaring topology,
/// re-attaching consumers) with no action from this class. The background
/// loop opens the first connection. After that it only observes the
/// connection: it logs when the connection is degraded or has recovered, and
/// it never replaces the connection.
/// </para>
/// </remarks>
[MustDisposeResource(false)]
internal sealed class RabbitMqConnection : ID2Connection
{
    private readonly RabbitMqConnectionOptions r_options;
    private readonly ILogger<RabbitMqConnection> r_logger;
    private readonly TaskCompletionSource<bool> r_readyTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly object r_loopLock = new();
    private CancellationTokenSource? _shutdownCts;
    private Task? _reconnectLoop;
    private volatile IConnection? _connection;
    private bool _disposed;

    /// <summary>Initializes a new <see cref="RabbitMqConnection"/>.</summary>
    /// <param name="options">Connection-level options.</param>
    /// <param name="logger">Logger.</param>
    [MustDisposeResource(false)]
    public RabbitMqConnection(
        IOptions<RabbitMqConnectionOptions> options,
        ILogger<RabbitMqConnection> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        r_options = options.Value;
        r_logger = logger;
    }

    /// <inheritdoc />
    public bool IsOpen => _connection is { IsOpen: true };

    /// <inheritdoc />
    public Task ReadyTask => r_readyTcs.Task;

    /// <inheritdoc />
    public void StartReconnectLoop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!r_options.HasConnectionUri)
        {
            throw new InvalidOperationException(
                "RabbitMqConnectionOptions.ConnectionUri is required.");
        }

        lock (r_loopLock)
        {
            if (_reconnectLoop is not null) return;

            _shutdownCts = new CancellationTokenSource();
            _reconnectLoop = Task.Run(() => ReconnectLoopAsync(_shutdownCts.Token));
        }
    }

    /// <inheritdoc />
    public ValueTask<IChannel> CreateChannelAsync(
        CreateChannelOptions? options = null,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var conn = _connection;

        if (conn is null || !conn.IsOpen) throw new BrokerUnavailableException();

        return new ValueTask<IChannel>(conn.CreateChannelAsync(options, ct));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;

        // Stop the reconnect loop first so it doesn't race with disposal.
        _shutdownCts?.Cancel();
        if (_reconnectLoop is not null)
        {
            try
            {
                await _reconnectLoop;
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
        }

        var conn = _connection;
        _connection = null;
        if (conn is not null)
        {
            try
            {
                // Host-shutdown path: don't propagate the original ct (it may
                // already be canceled). Use None to let the broker round-trip
                // close cleanly.
                await conn.CloseAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                // As in ReconnectAttemptFailed, never pass the Exception
                // object: its Message can include the ConnectionUri with the
                // embedded password.
                RabbitMqConnectionLog.ConnectionCloseFailed(
                    r_logger, ex.GetType().FullName ?? ex.GetType().Name);
            }

            await conn.DisposeAsync();
        }

        _shutdownCts?.Dispose();
    }

    private async Task ReconnectLoopAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(r_options.RevealConnectionUri()),
            ClientProvidedName = r_options.ClientProvidedName,
            ConsumerDispatchConcurrency = r_options.ConsumerDispatchConcurrency,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
        };

        // Stage 1 - open the FIRST connection. Retry forever with
        // exponential backoff. Once succeeded, transition to Stage 2.
        if (!await OpenInitialConnectionAsync(factory, ct)) return;

        // Stage 2 - observe only. RabbitMQ.Client owns reconnection from
        // here on (AutomaticRecoveryEnabled and TopologyRecoveryEnabled
        // re-declare topology and re-attach consumers). This class never
        // replaces the IConnection mid-flight, because that orphans any
        // consumers registered against the old instance, which is worse than
        // waiting for an operator to restart the replica.
        await ObservationLoopAsync(ct);
    }

    private async Task<bool> OpenInitialConnectionAsync(
        ConnectionFactory factory, CancellationToken ct)
    {
        var attempt = 0;
        var delay = r_options.InitialReconnectDelay;

        while (!ct.IsCancellationRequested)
        {
            attempt++;
            try
            {
                var fresh = await factory.CreateConnectionAsync(ct);
                Observe(fresh);
                _connection = fresh;
                RabbitMqConnectionLog.ConnectionOpened(
                    r_logger, factory.HostName, factory.Port, attempt);
                r_readyTcs.TrySetResult(true);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                // Never pass the exception object: its Message includes the
                // full ConnectionUri, with the embedded password, when the
                // RabbitMQ.Client library cannot open the socket. The
                // exception type is enough for triage, and the broker's own
                // log has the full detail.
                RabbitMqConnectionLog.ReconnectAttemptFailed(
                    r_logger,
                    attempt,
                    delay,
                    ex.GetType().FullName ?? ex.GetType().Name);
                try
                {
                    await Task.Delay(delay, ct);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }

                // Exponential backoff capped at MaxReconnectDelay.
                delay = TimeSpan.FromMilliseconds(
                    Math.Min(
                        delay.TotalMilliseconds * 2,
                        r_options.MaxReconnectDelay.TotalMilliseconds));
            }
        }

        return false;
    }

    /// <summary>
    /// Attaches the client's own recovery and shutdown callbacks to the log. Automatic
    /// recovery re-opens the connection and re-declares topology on its own schedule, so
    /// without these the only symptom of a recovery is that a publish issued inside the
    /// window reaches no queue and a consumer stops receiving - both silent.
    /// </summary>
    private void Observe(IConnection connection)
    {
        connection.ConnectionShutdownAsync += (_, e) =>
        {
            // An Application initiator is this process closing its own connection on
            // shutdown, which is not a fault. Peer and Library initiators are the ones
            // that start a recovery this process did not request.
            if (e.Initiator != ShutdownInitiator.Application)
            {
                RabbitMqConnectionLog.ConnectionShutdown(
                    r_logger, e.Initiator.ToString(), e.ReplyCode, e.ReplyText);
            }

            return Task.CompletedTask;
        };

        connection.RecoverySucceededAsync += (_, _) =>
        {
            RabbitMqConnectionLog.RecoverySucceeded(r_logger);
            return Task.CompletedTask;
        };

        connection.ConnectionRecoveryErrorAsync += (_, e) =>
        {
            // Never pass the exception object: its Message can include the
            // ConnectionUri with the embedded password.
            RabbitMqConnectionLog.RecoveryFailed(
                r_logger, e.Exception.GetType().FullName ?? e.Exception.GetType().Name);
            return Task.CompletedTask;
        };

        connection.CallbackExceptionAsync += (_, e) =>
        {
            RabbitMqConnectionLog.ConnectionCallbackFailed(
                r_logger,
                e.Exception.GetType().FullName ?? e.Exception.GetType().Name,
                string.Join(",", e.Detail.Keys));
            return Task.CompletedTask;
        };

        connection.RecoveringConsumerAsync += (_, e) =>
        {
            RabbitMqConnectionLog.RecoveringConsumer(r_logger, e.ConsumerTag);
            return Task.CompletedTask;
        };

        connection.QueueNameChangedAfterRecoveryAsync += (_, e) =>
        {
            RabbitMqConnectionLog.QueueNameChangedAfterRecovery(
                r_logger,
                e.NameBefore,
                e.NameAfter);
            return Task.CompletedTask;
        };

        connection.ConnectionBlockedAsync += (_, e) =>
        {
            RabbitMqConnectionLog.ConnectionBlocked(r_logger, e.Reason);
            return Task.CompletedTask;
        };

        connection.ConnectionUnblockedAsync += (_, _) =>
        {
            RabbitMqConnectionLog.ConnectionUnblocked(r_logger);
            return Task.CompletedTask;
        };
    }

    private async Task ObservationLoopAsync(CancellationToken ct)
    {
        DateTimeOffset? degradedSince = null;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(r_options.HealthCheckInterval, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (IsOpen)
            {
                if (degradedSince is not null)
                {
                    var down = DateTimeOffset.UtcNow - (DateTimeOffset)degradedSince;
                    RabbitMqConnectionLog.ConnectionRecovered(r_logger, down);
                    degradedSince = null;
                }
            }
            else
            {
                degradedSince ??= DateTimeOffset.UtcNow;
                var down = DateTimeOffset.UtcNow - (DateTimeOffset)degradedSince;
                RabbitMqConnectionLog.ConnectionDegraded(r_logger, down);
            }
        }
    }
}
