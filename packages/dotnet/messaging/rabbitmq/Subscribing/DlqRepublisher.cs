// -----------------------------------------------------------------------
// <copyright file="DlqRepublisher.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Subscribing;

using DcsvIo.D2.Messaging.RabbitMq.Connection;
using Microsoft.Extensions.Logging;
using NodaTime;

/// <summary>Serializes bounded dead-letter operations on an owned transport.</summary>
internal sealed class DlqRepublisher : IAsyncDisposable
{
    private readonly Duration r_timeout;
    private readonly Duration r_cleanupTimeout;
    private readonly int r_capacity;
    private readonly TimeProvider r_time;
    private readonly Func<long, DlqTransportGeneration> r_generationFactory;
    private readonly SemaphoreSlim r_operationGate = new(1, 1);
    private readonly CancellationTokenSource r_shutdown = new();
    private readonly CancellationToken r_shutdownToken;
    private readonly TaskCompletionSource r_drained =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private DlqRepublisherState _state = new DlqRepublisherState.Empty();
    private int _admissions;
    private long _generationId;
    private long _operationId;
    private Task? _closeWork;
    private Task? _resourceCleanup;

    /// <summary>Initializes immutable configuration before any socket is allocated.</summary>
    /// <param name="connection">Connection configuration copied for dedicated transport.</param>
    /// <param name="options">Independent subscriber duration configuration.</param>
    /// <param name="prefetch">Positive delivery admission bound.</param>
    /// <param name="logger">Sanitized transport diagnostics.</param>
    /// <param name="time">Optional deterministic monotonic timer source.</param>
    /// <param name="generationFactory">Optional controlled generation allocation for lifetime tests.</param>
    internal DlqRepublisher(
        RabbitMqConnectionOptions connection,
        RabbitMqSubscriberOptions options,
        int prefetch,
        ILogger logger,
        TimeProvider? time = null,
        Func<long, DlqTransportGeneration>? generationFactory = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(prefetch);
        if (!RabbitMqSubscriberOptions.IsValidDuration(options.DlqRepublishTimeout)
            || !RabbitMqSubscriberOptions.IsValidDuration(options.DlqCleanupTimeout))
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        var connectionUri = connection.RevealConnectionUri();
        var clientName = connection.ClientProvidedName;
        r_timeout = options.DlqRepublishTimeout;
        r_cleanupTimeout = options.DlqCleanupTimeout;
        r_capacity = Math.Min(prefetch, ushort.MaxValue);
        r_time = time ?? TimeProvider.System;
        r_shutdownToken = r_shutdown.Token;
        r_generationFactory = generationFactory ?? (id => new DlqTransportGeneration(
            id, connectionUri, clientName, logger));
    }

    /// <summary>Gets the current closed-set lifecycle state.</summary>
    internal DlqRepublisherState State => Volatile.Read(ref _state);

    /// <summary>Gets the retained bounded-close worker.</summary>
    internal Task? CloseWork => Volatile.Read(ref _closeWork);

    /// <summary>Gets the retained resource-drain worker after closure begins.</summary>
    internal Task? ResourceCleanup => Volatile.Read(ref _resourceCleanup);

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        while (true)
        {
            var state = State;
            if (state is DlqRepublisherState.Closed closed)
                return new ValueTask(closed.BoundedWait);

            var generation = GetGeneration(state);
            var bounded = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var resources = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var replacement = new DlqRepublisherState.Closed(
                generation, bounded.Task, resources.Task);
            if (!ReferenceEquals(
                    Interlocked.CompareExchange(ref _state, replacement, state), state))
                continue;

            if (Interlocked.Or(ref _admissions, int.MinValue) == 0) r_drained.TrySetResult();

            r_shutdown.Cancel();
            generation?.Terminate();
            Volatile.Write(ref _resourceCleanup, DrainResourcesAsync(generation, resources));
            Volatile.Write(ref _closeWork, CompleteBoundedCloseAsync(resources.Task, bounded));
            return new ValueTask(bounded.Task);
        }
    }

    /// <summary>Publishes an original delivery within its admission-to-confirm deadline.</summary>
    /// <param name="request">Original body and transport metadata.</param>
    /// <param name="cancellationToken">Delivery cancellation.</param>
    /// <returns>The terminal delivery outcome.</returns>
    internal async ValueTask<DlqRepublishOutcome> RepublishAsync(
        DlqRepublishRequest request, CancellationToken cancellationToken)
    {
        if (!TryAdmit())
        {
            return Volatile.Read(ref _admissions) < 0
                ? DlqRepublishOutcome.Stopping
                : DlqRepublishOutcome.TransportFailure;
        }

        try
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
                r_shutdownToken, cancellationToken);
            var operation = new DlqRepublishOperation(
                Interlocked.Increment(ref _operationId),
                request,
                r_timeout,
                r_time,
                cancellation.Token,
                Retire);
            var acquired = false;

            try
            {
                await r_operationGate.WaitAsync(operation.WaitToken);
                acquired = true;
                var generation = AcquireGeneration(operation);

                if (generation is not null)
                {
                    operation.Own(generation);
                    if (!operation.IsTerminal
                        && !generation.Start(operation, () => MarkReady(generation)))
                        operation.Complete(DlqRepublishOutcome.Stopping);
                }

                var outcome = await operation.Completion;
                if (generation is not null)
                {
                    if (outcome == DlqRepublishOutcome.Confirmed)
                        await generation.WorkCompletion;
                    else
                        await WaitForCleanupAsync(generation.ResourceCompletion);
                }

                return outcome;
            }
            catch (OperationCanceledException) when (operation.IsTerminal)
            {
                return await operation.Completion;
            }
            finally
            {
                await operation.DisposeAsync();
                if (acquired) r_operationGate.Release();
            }
        }
        finally
        {
            if (Interlocked.Decrement(ref _admissions) == int.MinValue) r_drained.TrySetResult();
        }
    }

    private static DlqTransportGeneration? GetGeneration(DlqRepublisherState state) => state switch
    {
        DlqRepublisherState.Opening opening => opening.Generation,
        DlqRepublisherState.Ready ready => ready.Generation,
        DlqRepublisherState.Retiring retiring => retiring.Generation,
        DlqRepublisherState.Closed closed => closed.Generation,
        _ => null,
    };

    private bool TryAdmit()
    {
        while (true)
        {
            var admissions = Volatile.Read(ref _admissions);
            if (admissions < 0 || admissions >= r_capacity) return false;

            if (Interlocked.CompareExchange(
                    ref _admissions, admissions + 1, admissions) == admissions)
                return true;
        }
    }

    private DlqTransportGeneration? AcquireGeneration(DlqRepublishOperation operation)
    {
        while (!operation.IsTerminal)
        {
            var state = State;

            switch (state)
            {
                case DlqRepublisherState.Closed:
                    operation.Complete(DlqRepublishOutcome.Stopping);
                    return null;
                case DlqRepublisherState.Retiring retiring:
                    if (!retiring.Generation.ResourceCompletion.IsCompleted)
                    {
                        operation.Complete(DlqRepublishOutcome.CleanupPending);
                        return null;
                    }

                    Interlocked.CompareExchange(ref _state, new DlqRepublisherState.Empty(), state);
                    break;
                case DlqRepublisherState.Ready ready:
                    return ready.Generation;
                case DlqRepublisherState.Opening opening:
                    return opening.Generation;
                case DlqRepublisherState.Empty:
                    DlqTransportGeneration generation;
                    try
                    {
                        generation = r_generationFactory(Interlocked.Increment(ref _generationId));
                    }
                    catch (Exception)
                    {
                        operation.Complete(DlqRepublishOutcome.TransportFailure);
                        return null;
                    }

                    var replacement = new DlqRepublisherState.Opening(generation);
                    if (ReferenceEquals(
                            Interlocked.CompareExchange(ref _state, replacement, state), state))
                        return generation;

                    generation.Terminate();
                    break;
            }
        }

        return null;
    }

    private void MarkReady(DlqTransportGeneration generation)
    {
        var state = State;
        if (state is DlqRepublisherState.Opening opening
            && ReferenceEquals(opening.Generation, generation) && !generation.IsRetired)
        {
            Interlocked.CompareExchange(
                ref _state, new DlqRepublisherState.Ready(generation), state);
        }
    }

    private void Retire(DlqTransportGeneration generation)
    {
        generation.Terminate();

        while (true)
        {
            var state = State;
            if (state is DlqRepublisherState.Closed or DlqRepublisherState.Retiring
                || !ReferenceEquals(GetGeneration(state), generation)) return;

            var replacement = new DlqRepublisherState.Retiring(generation);
            if (ReferenceEquals(Interlocked.CompareExchange(ref _state, replacement, state), state))
                return;
        }
    }

    private async Task WaitForCleanupAsync(Task resources)
    {
        try
        {
            await resources.WaitAsync(r_cleanupTimeout.ToTimeSpan(), r_time);
        }
        catch (TimeoutException)
        {
            // Retiring retains the generation; another delivery cannot allocate a replacement.
        }
    }

    private async Task DrainResourcesAsync(
        DlqTransportGeneration? generation, TaskCompletionSource completion)
    {
        try
        {
            await r_drained.Task;
            if (generation is not null) await generation.DisposeAsync();

            r_operationGate.Dispose();
            r_shutdown.Dispose();
            completion.TrySetResult();
        }
        catch (Exception ex)
        {
            completion.TrySetException(ex);
        }
    }

    private async Task CompleteBoundedCloseAsync(Task resources, TaskCompletionSource bounded)
    {
        try
        {
            await WaitForCleanupAsync(resources);
            bounded.TrySetResult();
        }
        catch (Exception ex)
        {
            bounded.TrySetException(ex);
        }
    }
}
