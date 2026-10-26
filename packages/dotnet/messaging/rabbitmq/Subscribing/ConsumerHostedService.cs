// -----------------------------------------------------------------------
// <copyright file="ConsumerHostedService.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Subscribing;

using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Topology;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Owns one <see cref="SubscriberChannel"/> per registered subscriber.
/// Awaits connection ready, then opens all channels in parallel; on shutdown
/// closes them in parallel. If no subscribers are registered, this hosted
/// service does nothing and adds no cost.
/// </summary>
[MustDisposeResource(false)]
internal sealed class ConsumerHostedService : IHostedService, IAsyncDisposable
{
    private readonly ID2Connection r_connection;
    private readonly SubscriberRegistry r_registry;
    private readonly IServiceScopeFactory r_scopeFactory;
    private readonly HandlerDispatcherFactory r_dispatcherFactory;
    private readonly MessagingCatalog r_catalog;
    private readonly ITopologyDeclarer r_topology;
    private readonly ILoggerFactory r_loggerFactory;
    private readonly ILogger<ConsumerHostedService> r_logger;
    private readonly RabbitMqConnectionOptions r_connectionOptions;
    private readonly RabbitMqSubscriberOptions r_subscriberOptions;
    private readonly List<SubscriberChannel> r_channels = [];
    private readonly CancellationTokenSource r_cts = new();
    private readonly CancellationToken r_shutdownToken;
    private readonly TaskCompletionSource r_readyTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Guards r_channels, _startTask, _shutdownTask and _channelsDrained.
    // StopAsync and DisposeAsync run the same cancel-wait-dispose sequence, so
    // the first call publishes _shutdownTask under this gate and runs it after
    // releasing the gate, and every later call awaits that same task instead
    // of running a second pass.
    private readonly Lock r_shutdownGate = new();

    private Task? _startTask;
    private Task? _shutdownTask;
    private bool _channelsDrained;
    private int _disposed;

    /// <summary>Initializes the hosted service.</summary>
    /// <param name="connection">Connection wrapper.</param>
    /// <param name="registry">Subscriber registry.</param>
    /// <param name="scopeFactory">Per-message DI scope factory.</param>
    /// <param name="dispatcherFactory">Closed-generic dispatcher per subscriber.</param>
    /// <param name="catalog">The message descriptors this host consumes against.</param>
    /// <param name="topology">Topology declarer. It is awaited before
    /// <c>BasicConsume</c> so that consumers do not race the queue existence
    /// check, and it joins a declaration that is already running instead of
    /// sending a second one.</param>
    /// <param name="loggerFactory">Logger factory (per-channel logger).</param>
    /// <param name="logger">Hosted-service logger.</param>
    /// <param name="connectionOptions">Configuration copied for dedicated DLQ transport.</param>
    /// <param name="subscriberOptions">Independent subscriber operation and cleanup bounds.</param>
    [MustDisposeResource(false)]
    public ConsumerHostedService(
        ID2Connection connection,
        SubscriberRegistry registry,
        IServiceScopeFactory scopeFactory,
        HandlerDispatcherFactory dispatcherFactory,
        MessagingCatalog catalog,
        ITopologyDeclarer topology,
        ILoggerFactory loggerFactory,
        ILogger<ConsumerHostedService> logger,
        IOptions<RabbitMqConnectionOptions> connectionOptions,
        IOptions<RabbitMqSubscriberOptions> subscriberOptions)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(dispatcherFactory);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(logger);
        r_connection = connection;
        r_registry = registry;
        r_scopeFactory = scopeFactory;
        r_dispatcherFactory = dispatcherFactory;
        r_catalog = catalog;
        r_topology = topology;
        r_loggerFactory = loggerFactory;
        r_logger = logger;
        r_shutdownToken = r_cts.Token;
        ArgumentNullException.ThrowIfNull(connectionOptions);
        ArgumentNullException.ThrowIfNull(subscriberOptions);
        r_connectionOptions = connectionOptions.Value.CopyConnectionSettings();
        r_subscriberOptions = new RabbitMqSubscriberOptions(
            subscriberOptions.Value.DlqRepublishTimeout,
            subscriberOptions.Value.DlqCleanupTimeout);
    }

    /// <summary>Gets a task that completes when every registered subscriber
    /// channel has finished <c>BasicConsume</c> (or when the registry is
    /// empty). On a fresh broker the queue may not exist yet at the moment
    /// of the first publish; awaiting this task waits until the queues are
    /// declared and consumed.</summary>
    public Task ReadyTask => r_readyTcs.Task;

    /// <summary>Gets the background start task, which a test can await to
    /// observe its completion.</summary>
    internal Task? StartTaskForTesting => _startTask;

    /// <summary>Gets or sets an observer after startup publication and before scheduling.</summary>
    internal Action? StartupPublishedForTesting { get; set; }

    /// <summary>Gets the number of subscriber channels this service currently
    /// owns and is responsible for disposing.</summary>
    private int ChannelCount
    {
        get
        {
            lock (r_shutdownGate)
                return r_channels.Count;
        }
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (r_registry.All.Count == 0)
        {
            // With no subscribers there is nothing to start, so ReadyTask
            // completes immediately.
            r_readyTcs.TrySetResult();
            return Task.CompletedTask;
        }

        // Publish before scheduling so shutdown can await startup. The launcher
        // always runs; its body observes cancellation and completes readiness.
        var launch = new Task<Task>(() => StartChannelsAsync(r_shutdownToken));
        var startTask = launch.Unwrap();

        lock (r_shutdownGate)
        {
            if (_shutdownTask is not null)
            {
                r_readyTcs.TrySetCanceled(CancellationToken.None);

                return Task.CompletedTask;
            }

            if (_startTask is not null) return Task.CompletedTask;

            _startTask = startTask;
        }

        StartupPublishedForTesting?.Invoke();
        launch.Start(TaskScheduler.Default);

        // Follow TopologyHostedService's fault-log pattern: a faulted
        // background start would otherwise go to
        // TaskScheduler.UnobservedTaskException unless something explicitly
        // awaits ReadyTask. Log the fault as a structured entry.
        startTask.ContinueWith(
            t => SubscriberLog.HostStartupFaulted(r_logger, t.Exception!),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await ShutdownAsync(cancellationToken);

        Task? start;
        lock (r_shutdownGate)
            start = _startTask;

        // A faulted background start is rethrown from StopAsync after every
        // channel has been disposed, so surfacing it costs no cleanup.
        if (start is { IsFaulted: true })
            await start;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        // DisposeAsync can run without a prior StopAsync. It owns the
        // channels and the background start, so it runs the same shutdown that
        // StopAsync would have run. When StopAsync already ran, ShutdownAsync
        // returns that completed sequence instead of repeating it.
        await ShutdownAsync(CancellationToken.None);
        r_cts.Dispose();
    }

    /// <summary>Returns the one shutdown sequence, starting it if this is the
    /// first caller. Every later caller - a second StopAsync, a DisposeAsync
    /// racing the host - awaits the same task rather than running a second
    /// cancel-wait-dispose pass over the same state.</summary>
    /// <param name="cancellationToken">Bounds the wait for the background
    /// start task. Only the caller that starts the sequence supplies it; a
    /// caller that joins an in-flight sequence waits for it to finish.</param>
    private Task ShutdownAsync(CancellationToken cancellationToken)
    {
        // The sequence is built unstarted before the gate, published under it,
        // and run by the caller that published it after releasing the gate.
        // Started inside the lock, the sequence would reach the channels'
        // broker calls with the gate held whenever nothing before them has to
        // wait. A caller that finds a sequence already published drops its own
        // unstarted one.
        var launch = new Task<Task>(() => ShutdownCoreAsync(cancellationToken));
        var candidate = launch.Unwrap();
        Task shutdown;

        lock (r_shutdownGate)
            shutdown = _shutdownTask ??= candidate;

        if (ReferenceEquals(shutdown, candidate))
            launch.RunSynchronously(TaskScheduler.Default);

        return shutdown;
    }

    private async Task ShutdownCoreAsync(CancellationToken cancellationToken)
    {
        await r_cts.CancelAsync();

        Task? start;
        lock (r_shutdownGate)
            start = _startTask;

        if (start is not null)
        {
            try
            {
                await start.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
            catch (Exception)
            {
                // A faulted start is already reported by the fault-log
                // continuation in StartAsync, and StopAsync re-observes it
                // below. Rethrowing here would abandon every channel the start
                // opened before it failed.
            }
        }

        // The snapshot transfers tracked channels to this shutdown. If the wait
        // was canceled, TryTrackChannel prevents a late channel from entering it.
        SubscriberChannel[] channels;
        lock (r_shutdownGate)
        {
            _channelsDrained = true;
            channels = [.. r_channels];
            r_channels.Clear();
        }

        // Close channels in parallel - independent operations.
        await Task.WhenAll(channels.Select(ch => ch.DisposeAsync().AsTask()));
    }

    /// <summary>Takes ownership of a freshly started channel, or refuses it
    /// because the shutdown snapshot has already been taken.</summary>
    /// <param name="channel">The started channel.</param>
    /// <returns><see langword="true"/> when the service now owns the channel
    /// and will dispose it.</returns>
    private bool TryTrackChannel(SubscriberChannel channel)
    {
        lock (r_shutdownGate)
        {
            if (_channelsDrained) return false;

            r_channels.Add(channel);
            return true;
        }
    }

    private async Task StartChannelsAsync(CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            // Wait for the first connection to be ready. If the broker is
            // already up, ReadyTask is already completed; otherwise this
            // awaits it.
            await r_connection.ReadyTask.WaitAsync(ct);

            // Declare topology before BasicConsume so every queue, exchange and
            // binding exists before a consumer attaches. The declarer declares once
            // per instance, so when TopologyHostedService got here first this awaits
            // that declaration rather than sending a second set of declare and bind
            // commands - two concurrent binds for one binding let bind-ok return
            // before the exchange routes to the queue.
            await r_topology.DeclareAsync(ct);

            foreach (var reg in r_registry.All)
            {
                ct.ThrowIfCancellationRequested();
                var ch = new SubscriberChannel(
                    r_connection,
                    r_scopeFactory,
                    r_dispatcherFactory,
                    r_catalog,
                    reg,
                    r_loggerFactory.CreateLogger<SubscriberChannel>(),
                    r_connectionOptions,
                    r_subscriberOptions);

                try
                {
                    await ch.StartAsync(ct);
                }
                catch
                {
                    await ch.DisposeAsync();
                    throw;
                }

                if (!TryTrackChannel(ch))
                {
                    // Shutdown took its snapshot while this channel was opening.
                    // Nothing else holds a reference to it, so it is disposed
                    // here rather than left without an owner.
                    await ch.DisposeAsync();
                    throw new OperationCanceledException(ct);
                }
            }

            SubscriberLog.HostStarted(r_logger, ChannelCount);
            r_readyTcs.TrySetResult();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            r_readyTcs.TrySetCanceled(ct);
            throw;
        }
        catch (Exception ex)
        {
            r_readyTcs.TrySetException(ex);
            throw;
        }
    }
}
