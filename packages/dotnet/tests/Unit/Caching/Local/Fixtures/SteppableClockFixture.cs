// -----------------------------------------------------------------------
// <copyright file="SteppableClockFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Caching.Local.Fixtures;

/// <summary>
/// A clock a test moves in either direction.
/// </summary>
/// <remarks>
/// <c>FakeTimeProvider</c> cannot move backwards, so a guard written for a
/// host whose clock was stepped back cannot be observed through it. This one
/// can be set to any instant, so a test can drive the clock backwards and
/// observe the guard directly.
/// </remarks>
internal sealed class SteppableClockFixture : TimeProvider
{
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>Moves the clock forward by a span.</summary>
    /// <param name="span">How far forward.</param>
    internal void Advance(TimeSpan span) => _now += span;

    /// <summary>Moves the clock backwards by a span.</summary>
    /// <param name="span">How far back.</param>
    internal void StepBack(TimeSpan span) => _now -= span;
}
