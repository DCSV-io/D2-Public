// -----------------------------------------------------------------------
// <copyright file="TokenBucketState.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching.Local.Default;

/// <summary>
/// One token bucket's stored tokens, the instant they were counted, and the
/// accrual that has not yet amounted to a whole thousandth of a token.
/// </summary>
/// <remarks>
/// <para>
/// Thousandths keep the refill in integer arithmetic. One elapsed millisecond
/// adds a fraction of a token at most rates, and a bucket that floored that
/// fraction to zero would never refill. The Redis flavor stores the same values
/// in the same units.
/// </para>
/// <para>
/// The values are settable and the instance is built before the write lock is
/// taken, so the critical section around the refill allocates nothing. Each call
/// stores a new instance rather than editing the one it read, so a reader
/// outside the lock never sees a half-updated bucket.
/// </para>
/// </remarks>
internal sealed class TokenBucketState
{
    /// <summary>Gets or sets the tokens held, in thousandths.</summary>
    internal long TokensMilli { get; set; }

    /// <summary>Gets or sets the Unix milliseconds at which those tokens were counted.</summary>
    internal long AtMs { get; set; }

    /// <summary>
    /// Gets or sets the accrual left over from the last refill, in
    /// thousandth-token milliseconds. Always under one refill interval.
    /// </summary>
    internal long AccrualRemainder { get; set; }
}
