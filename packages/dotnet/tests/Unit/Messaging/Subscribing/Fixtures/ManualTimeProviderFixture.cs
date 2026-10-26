// -----------------------------------------------------------------------
// <copyright file="ManualTimeProviderFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Subscribing.Fixtures;

/// <summary>Advances monotonic timestamps independently from delivery of timer callbacks.</summary>
internal sealed class ManualTimeProviderFixture : TimeProvider
{
    private readonly List<ManualTimerFixture> r_timers = [];
    private long _timestamp;

    public override long TimestampFrequency => 1000;

    public override long GetTimestamp() => _timestamp;

    public override ITimer CreateTimer(
        TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimerFixture(callback, state);
        timer.Change(dueTime, period);
        r_timers.Add(timer);
        return timer;
    }

    internal void AdvanceWithoutTimers(long milliseconds) => _timestamp += milliseconds;

    internal void FireTimers()
    {
        foreach (var timer in r_timers.ToArray()) timer.Fire();
    }

    private sealed class ManualTimerFixture(TimerCallback callback, object? state) : ITimer
    {
        private bool _enabled;
        private bool _disposed;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _enabled = dueTime != Timeout.InfiniteTimeSpan;
            return true;
        }

        public void Dispose() => _disposed = true;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        internal void Fire()
        {
            if (_enabled && !_disposed) callback(state);
        }
    }
}
