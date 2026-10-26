// -----------------------------------------------------------------------
// <copyright file="DlqRepublishOperation.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Subscribing;

using NodaTime;

/// <summary>Arbitrates one publication outcome against its original monotonic deadline.</summary>
internal sealed class DlqRepublishOperation : IAsyncDisposable
{
    private readonly TimeProvider r_time;
    private readonly long r_deadline;
    private readonly TaskCompletionSource<DlqRepublishOutcome> r_completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly CancellationTokenSource r_wait = new();
    private readonly ITimer r_timer;
    private readonly CancellationTokenRegistration r_cancellation;
    private readonly Action<DlqTransportGeneration> r_retire;
    private DlqTransportGeneration? _generation;
    private OperationState _state = new Pending(OperationPhase.Admission, null);

    /// <summary>Initializes the deadline before any admission wait.</summary>
    /// <param name="id">The operation identifier.</param>
    /// <param name="request">The original delivery snapshot.</param>
    /// <param name="timeout">The validated operation duration.</param>
    /// <param name="time">Monotonic timestamp and timer source.</param>
    /// <param name="cancellationToken">Subscriber or caller cancellation.</param>
    /// <param name="retire">Retires owned transport after a failure; it must not throw.</param>
    internal DlqRepublishOperation(
        long id,
        DlqRepublishRequest request,
        Duration timeout,
        TimeProvider time,
        CancellationToken cancellationToken,
        Action<DlqTransportGeneration> retire)
    {
        Id = id;
        Request = request;
        r_time = time;
        r_retire = retire;
        r_deadline = checked(time.GetTimestamp()
            + (long)Math.Ceiling(timeout.TotalSeconds * time.TimestampFrequency));
        r_timer = time.CreateTimer(
            _ => Complete(DlqRepublishOutcome.DeadlineExceeded),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
        r_cancellation = cancellationToken.Register(
            () => Complete(DlqRepublishOutcome.Stopping));
        r_timer.Change(timeout.ToTimeSpan(), Timeout.InfiniteTimeSpan);
    }

    /// <summary>The pending phases supported by the .NET confirmed channel.</summary>
    internal enum OperationPhase
    {
        /// <summary>Waiting for the operation gate.</summary>
        Admission,

        /// <summary>Acquiring the dedicated connection and confirmed channel.</summary>
        Acquisition,

        /// <summary>Waiting for mandatory publication confirmation.</summary>
        Publication,
    }

    /// <summary>Gets the non-reused operation identifier.</summary>
    internal long Id { get; }

    /// <summary>Gets the original delivery snapshot.</summary>
    internal DlqRepublishRequest Request { get; }

    /// <summary>Gets the single terminal completion.</summary>
    internal Task<DlqRepublishOutcome> Completion => r_completion.Task;

    /// <summary>Gets cancellation of this operation's admission wait.</summary>
    internal CancellationToken WaitToken => r_wait.Token;

    /// <summary>Gets a value indicating whether a terminal outcome has won.</summary>
    internal bool IsTerminal => State is Terminal;

    /// <summary>Gets the operation's closed-set state.</summary>
    internal OperationState State => Volatile.Read(ref _state);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await r_timer.DisposeAsync();
        await r_cancellation.DisposeAsync();
        r_wait.Dispose();
    }

    /// <summary>Associates this admitted operation with its retained generation.</summary>
    /// <param name="generation">The transport admitted under the serialization gate.</param>
    internal void Own(DlqTransportGeneration generation)
    {
        Volatile.Write(ref _generation, generation);
        Advance(OperationPhase.Acquisition, generation.Id);
        if (State is Terminal { Outcome: not DlqRepublishOutcome.Confirmed }) r_retire(generation);
    }

    /// <summary>Advances pending work without changing a terminal result.</summary>
    /// <param name="phase">The next pending phase.</param>
    /// <param name="generationId">The owning generation identifier.</param>
    internal void Advance(OperationPhase phase, long generationId)
    {
        while (State is Pending pending)
        {
            var replacement = new Pending(phase, generationId);
            if (ReferenceEquals(
                    Interlocked.CompareExchange(ref _state, replacement, pending), pending))
                return;
        }
    }

    /// <summary>Accepts confirmation only before the original monotonic deadline.</summary>
    internal void Confirm()
    {
        var generation = Volatile.Read(ref _generation);
        Complete(r_time.GetTimestamp() >= r_deadline
            ? DlqRepublishOutcome.DeadlineExceeded
            : generation is { IsRetired: false }
                ? DlqRepublishOutcome.Confirmed
                : DlqRepublishOutcome.Stopping);
    }

    /// <summary>Chooses a terminal outcome and removes failed transport capability.</summary>
    /// <param name="outcome">The proposed terminal outcome.</param>
    internal void Complete(DlqRepublishOutcome outcome)
    {
        var replacement = new Terminal(outcome);

        while (true)
        {
            var state = State;
            if (state is Terminal) return;

            if (ReferenceEquals(Interlocked.CompareExchange(ref _state, replacement, state), state))
                break;
        }

        if (outcome != DlqRepublishOutcome.Confirmed)
        {
            var generation = Volatile.Read(ref _generation);
            if (generation is not null) r_retire(generation);
        }

        r_wait.Cancel();
        r_completion.TrySetResult(outcome);
    }

    /// <summary>Closed-set operation state.</summary>
    internal abstract record OperationState;

    /// <summary>An operation that has not selected a terminal outcome.</summary>
    /// <param name="Phase">The current phase.</param>
    /// <param name="GenerationId">The admitted generation, absent while waiting.</param>
    internal sealed record Pending(OperationPhase Phase, long? GenerationId) : OperationState;

    /// <summary>The only terminal transition of an operation.</summary>
    /// <param name="Outcome">The selected result.</param>
    internal sealed record Terminal(DlqRepublishOutcome Outcome) : OperationState;
}
