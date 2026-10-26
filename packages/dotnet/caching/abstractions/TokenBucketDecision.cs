// -----------------------------------------------------------------------
// <copyright file="TokenBucketDecision.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching;

/// <summary>
/// The decision one <see cref="ICacheAtomic.ConsumeTokenAsync"/> returned.
/// </summary>
/// <param name="Admitted">
/// Whether the bucket held the cost. The cost was consumed when it did, and
/// nothing was consumed when it did not.
/// </param>
/// <param name="RetryAfter">
/// How long the bucket needs to refill to the cost the caller asked for.
/// <see cref="TimeSpan.Zero"/> on an admitted request.
/// </param>
public readonly record struct TokenBucketDecision(bool Admitted, TimeSpan RetryAfter);
