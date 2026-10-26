// -----------------------------------------------------------------------
// <copyright file="InputFailures.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching;

using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Pre-built <see cref="D2Result"/> input-failure responses for cache implementations.
/// Keeps the cache surface pure errors-as-values instead of mixing in
/// <see cref="ArgumentException"/> throws for caller mistakes.
/// </summary>
/// <remarks>
/// Constructors still throw - DI / construction failure is a different
/// lifecycle concern from per-call input validation.
/// </remarks>
public static class InputFailures
{
    /// <summary>
    /// Builds a <see cref="D2Result{TData}"/> input failure for a missing
    /// (null or empty) required parameter.
    /// </summary>
    /// <typeparam name="T">Wrapped result type.</typeparam>
    /// <param name="paramName">Parameter name (use <c>nameof</c>).</param>
    public static D2Result<T> Required<T>(string paramName)
        => D2Result<T>.ValidationFailed(
            inputErrors: [new InputError(paramName, [TK.Common.Errors.NOT_NULL_VIOLATION])]);

    /// <summary>
    /// Builds a non-generic <see cref="D2Result"/> input failure for a
    /// missing (null or empty) required parameter.
    /// </summary>
    /// <param name="paramName">Parameter name (use <c>nameof</c>).</param>
    public static D2Result Required(string paramName)
        => D2Result.ValidationFailed(
            inputErrors: [new InputError(paramName, [TK.Common.Errors.NOT_NULL_VIOLATION])]);

    /// <summary>
    /// Builds a <see cref="D2Result{TData}"/> input failure for a present but
    /// invalid parameter value (range, non-positive, etc.). Does not use
    /// <see cref="TK.Common.Errors.NOT_NULL_VIOLATION"/>.
    /// </summary>
    /// <typeparam name="T">Wrapped result type.</typeparam>
    /// <param name="paramName">Parameter name (use <c>nameof</c>).</param>
    public static D2Result<T> Invalid<T>(string paramName)
        => D2Result<T>.ValidationFailed(
            inputErrors: [new InputError(paramName, [TK.Common.Errors.VALIDATION_FAILED])]);

    /// <summary>
    /// Builds a non-generic <see cref="D2Result"/> input failure for a present
    /// but invalid parameter value (range, non-positive, etc.).
    /// </summary>
    /// <param name="paramName">Parameter name (use <c>nameof</c>).</param>
    public static D2Result Invalid(string paramName)
        => D2Result.ValidationFailed(
            inputErrors: [new InputError(paramName, [TK.Common.Errors.VALIDATION_FAILED])]);

    /// <summary>
    /// Builds the input failure that <see cref="ICacheAtomic.ConsumeTokenAsync"/>
    /// must return for these arguments, or <c>null</c> when every one of them is
    /// good.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every flavor returns the same result for the same argument, so the checks
    /// live here rather than once per implementation. A cost larger than the burst
    /// is rejected because the bucket can never hold it, so the member would
    /// return a wait that waiting cannot satisfy.
    /// </para>
    /// <para>
    /// The interval is rejected unless it is a whole number of milliseconds, at
    /// least one. Every flavor measures it in whole milliseconds - the
    /// in-process one casts it, the Redis one passes the cast to the script - so
    /// an interval under one would divide by zero and a fractional one would
    /// refill the bucket at a rate the caller did not ask for. The accepted
    /// range and the usable range are therefore the same one, and the TypeScript
    /// twin rejects at the same bound: its own interval is a millisecond count
    /// and it requires a safe integer.
    /// </para>
    /// </remarks>
    /// <param name="key">The bucket key.</param>
    /// <param name="refillTokens">Tokens added per interval.</param>
    /// <param name="refillInterval">The interval those tokens are added over.</param>
    /// <param name="burst">The bucket's capacity.</param>
    /// <param name="cost">Tokens this request consumes.</param>
    /// <returns>The failure, or <c>null</c> when the arguments are good.</returns>
    public static D2Result<TokenBucketDecision>? TokenBucket(
        string key, long refillTokens, TimeSpan refillInterval, long burst, long cost)
    {
        if (key.Falsey()) return Required<TokenBucketDecision>(nameof(key));
        if (refillTokens < 1) return Invalid<TokenBucketDecision>(nameof(refillTokens));

        if (refillInterval < TimeSpan.FromMilliseconds(1)
            || refillInterval.Ticks % TimeSpan.TicksPerMillisecond != 0)
        {
            return Invalid<TokenBucketDecision>(nameof(refillInterval));
        }

        if (burst < 1) return Invalid<TokenBucketDecision>(nameof(burst));
        if (cost < 1 || cost > burst) return Invalid<TokenBucketDecision>(nameof(cost));

        return null;
    }
}
