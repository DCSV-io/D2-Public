// -----------------------------------------------------------------------
// <copyright file="AdvancingClockFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Caching.Local.Fixtures;

/// <summary>
/// A clock that moves one step forward on every read.
/// </summary>
/// <remarks>
/// <c>FakeTimeProvider</c> hands every caller in a parallel run the same
/// instant, so a member that reads the clock and then updates shared state
/// cannot be observed applying two instants out of order. This one gives each
/// read its own instant, and <see cref="Span"/> reports how far the whole run
/// moved, so a test can compare what a bucket admitted against what that span
/// could have added.
/// <para>
/// The read yields its thread before returning. A caller that reads the clock
/// and then takes a lock is very likely to be overtaken between the two, so the
/// instants are applied in a different order from the one they were read in. A
/// caller that reads inside the lock it already holds is unaffected: a yield
/// waits for no other thread, so the lock is released on the same path either
/// way.
/// </para>
/// </remarks>
/// <param name="step">How far one read moves the clock.</param>
internal sealed class AdvancingClockFixture(TimeSpan step) : TimeProvider
{
    private static readonly DateTimeOffset sr_start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private long _reads;

    /// <summary>Gets the span between the first read and the latest one.</summary>
    internal TimeSpan Span => step * Math.Max(0, Volatile.Read(ref _reads) - 1);

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        var at = sr_start + (step * (Interlocked.Increment(ref _reads) - 1));

        Thread.Yield();

        return at;
    }
}
