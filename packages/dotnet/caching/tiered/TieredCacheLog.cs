// -----------------------------------------------------------------------
// <copyright file="TieredCacheLog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching.Tiered;

using Microsoft.Extensions.Logging;

/// <summary>
/// <c>LoggerMessage</c> delegates for the tiered cache. No delegate accepts
/// <see cref="Exception"/> - each takes only result-shaped strings
/// (error codes, key, etc.), so an Exception parameter would be both
/// unnecessary and a leak vector.
/// </summary>
/// <remarks>
/// The delegates that take a key write it verbatim, because an operator with a
/// failed invalidation needs the key to find the entry. The key is composed
/// outside this package, so a key containing a personal value would put that
/// value in every log sink. The abstractions package states the constraint that
/// follows: a cache key identifies an entry and contains no personal value.
/// </remarks>
internal static partial class TieredCacheLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Tiered cache L1 invalidation handler failed for key {Key} "
            + "(errorCode {ErrorCode}).")]
    public static partial void L1InvalidationFailed(
        ILogger logger,
        string key,
        string errorCode);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Tiered cache L1 {Operation} failed for key/scope {KeyOrCount} "
            + "(errorCode {ErrorCode}); L2 write succeeded - operation reported "
            + "as Ok and L1 will rebuild on next read.")]
    public static partial void L1WriteFailedAfterL2Success(
        ILogger logger,
        string operation,
        string keyOrCount,
        string errorCode);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Tiered cache L1 drop failed for key {Key} after {Operation} "
            + "(errorCode {ErrorCode}); this node may answer a local copy that "
            + "no longer matches the shared value until that copy expires.")]
    public static partial void L1DropFailed(
        ILogger logger,
        string operation,
        string key,
        string errorCode);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Tiered cache invalidation publish failed for key {Key} after "
            + "{Operation} (errorCode {ErrorCode}); the other instances keep their "
            + "local copy of the superseded value until it expires.")]
    public static partial void InvalidationPublishFailed(
        ILogger logger,
        string operation,
        string key,
        string errorCode);
}
