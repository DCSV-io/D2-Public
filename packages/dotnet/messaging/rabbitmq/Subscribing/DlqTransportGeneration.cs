// -----------------------------------------------------------------------
// <copyright file="DlqTransportGeneration.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Subscribing;

using DcsvIo.D2.Messaging.RabbitMq.Connection;
using global::RabbitMQ.Client;
using Microsoft.Extensions.Logging;

/// <summary>Owns dedicated sockets, acquired resources and pending client operations.</summary>
internal sealed class DlqTransportGeneration : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<IConnection>> r_acquireConnection;
    private readonly OwnedSocketSet r_sockets;
    private readonly ILogger r_logger;
    private readonly CancellationTokenSource r_lifetime = new();
    private readonly TaskCompletionSource r_resources =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    // r_gate guards work publication and retirement; client calls run outside it.
    private readonly Lock r_gate = new();
    private Task _work = Task.CompletedTask;
    private Task? _cleanup;
    private bool _retired;
    private IConnection? _connection;
    private IChannel? _channel;

    /// <summary>Initializes a generation with copied connection configuration.</summary>
    /// <param name="id">The non-reused generation identifier.</param>
    /// <param name="connectionUri">The secret connection URI, never logged.</param>
    /// <param name="clientName">The client name reported to the broker.</param>
    /// <param name="logger">Sanitized transport diagnostics.</param>
    /// <param name="acquireConnection">Optional controlled acquisition for late-completion tests.</param>
    internal DlqTransportGeneration(
        long id,
        string connectionUri,
        string clientName,
        ILogger logger,
        Func<CancellationToken, Task<IConnection>>? acquireConnection = null)
    {
        Id = id;
        r_logger = logger;
        var factory = new ConnectionFactory
        {
            Uri = new Uri(connectionUri),
            ClientProvidedName = clientName,
            AutomaticRecoveryEnabled = false,
            TopologyRecoveryEnabled = false,
        };
        r_sockets = new OwnedSocketSet(factory.SocketFactory);
        factory.SocketFactory = r_sockets.Create;
        r_acquireConnection = acquireConnection ?? (token => factory.CreateConnectionAsync(token));
    }

    /// <summary>Gets the generation identifier.</summary>
    internal long Id { get; }

    /// <summary>Gets completion of all retained work and resource disposal.</summary>
    internal Task ResourceCompletion => r_resources.Task;

    /// <summary>Gets the retained cleanup worker after retirement begins.</summary>
    internal Task? CleanupWork => Volatile.Read(ref _cleanup);

    /// <summary>Gets the currently retained publication work.</summary>
    internal Task WorkCompletion
    {
        get
        {
            lock (r_gate) return _work;
        }
    }

    /// <summary>Gets the retained connection-acquisition task, if started.</summary>
    internal Task<IConnection>? ConnectionAcquisition { get; private set; }

    /// <summary>Gets the retained acquisition task, if channel acquisition started.</summary>
    internal Task<IChannel>? ChannelAcquisition { get; private set; }

    /// <summary>Gets the retained mandatory publication task, if publication started.</summary>
    internal Task? Send { get; private set; }

    /// <summary>Gets a value indicating whether transport admission is closed.</summary>
    internal bool IsRetired
    {
        get
        {
            lock (r_gate) return _retired;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Terminate();
        return new ValueTask(ResourceCompletion);
    }

    /// <summary>Publishes work before scheduling so retirement retains unstarted work.</summary>
    /// <param name="operation">The operation owning this generation's current publication.</param>
    /// <param name="ready">Marks successful channel acquisition before publication.</param>
    /// <returns>Whether the generation accepted the operation.</returns>
    internal bool Start(DlqRepublishOperation operation, Action ready)
    {
        var launch = new Task<Task>(
            () => PublishCoreAsync(operation, ready),
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach);
        var work = launch.Unwrap();
        bool accepted;

        lock (r_gate)
        {
            accepted = !_retired;
            if (accepted) _work = work;
        }

        if (!accepted)
        {
            return false;
        }

        launch.Start(TaskScheduler.Default);
        return true;
    }

    /// <summary>Removes network capability before retained asynchronous cleanup.</summary>
    internal void Terminate()
    {
        Task work;

        lock (r_gate)
        {
            if (_retired) return;

            _retired = true;
            work = _work;
        }

        r_sockets.Terminate();
        Volatile.Write(ref _cleanup, CleanupAsync(work));
    }

    private async Task PublishCoreAsync(DlqRepublishOperation operation, Action ready)
    {
        try
        {
            r_lifetime.Token.ThrowIfCancellationRequested();
            if (IsRetired || operation.IsTerminal) return;

            if (_connection is null)
            {
                ConnectionAcquisition = r_acquireConnection(r_lifetime.Token);
                _connection = await ConnectionAcquisition;
            }

            if (IsRetired || operation.IsTerminal) return;

            if (_channel is null)
            {
                ChannelAcquisition = _connection.CreateChannelAsync(
                    new CreateChannelOptions(true, true), r_lifetime.Token);
                _channel = await ChannelAcquisition;
            }

            if (IsRetired || operation.IsTerminal) return;

            ready();
            operation.Advance(DlqRepublishOperation.OperationPhase.Publication, Id);
            var request = operation.Request;
            var properties = new BasicProperties
            {
                MessageId = request.MessageId,
                ContentType = request.ContentType,
                ContentEncoding = request.ContentEncoding,
                CorrelationId = request.CorrelationId,
                Headers = new Dictionary<string, object?>(request.Headers),
                DeliveryMode = DeliveryModes.Persistent,
            };

            Send = _channel.BasicPublishAsync(
                request.DlxName,
                string.Empty,
                mandatory: true,
                properties,
                request.Body,
                r_lifetime.Token).AsTask();
            await Send;
            Send = null;
            operation.Confirm();
        }
        catch (global::RabbitMQ.Client.Exceptions.PublishException ex) when (ex.IsReturn)
        {
            operation.Complete(DlqRepublishOutcome.Unroutable);
        }
        catch (Exception)
        {
            operation.Complete(DlqRepublishOutcome.TransportFailure);
        }
    }

    private async Task CleanupAsync(Task work)
    {
        try
        {
            try
            {
                await r_lifetime.CancelAsync();
            }
            catch (Exception ex)
            {
                LogCleanupFailure(ex);
            }

            try
            {
                await work;
            }
            finally
            {
                try
                {
                    if (_channel is not null) await _channel.DisposeAsync();
                }
                finally
                {
                    if (_connection is not null) await _connection.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            LogCleanupFailure(ex);
        }
        finally
        {
            r_sockets.Dispose();
            r_lifetime.Dispose();
            r_resources.TrySetResult();
        }
    }

    private void LogCleanupFailure(Exception exception)
    {
        try
        {
            RabbitMqConnectionLog.ConnectionCloseFailed(r_logger, exception.GetType().Name);
        }
        catch (Exception)
        {
            // Diagnostic failure must not interrupt retained resource cleanup.
        }
    }
}
