// -----------------------------------------------------------------------
// <copyright file="OutboxLog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Outbox.EntityFrameworkCore.Postgres;

using Microsoft.Extensions.Logging;

/// <summary>
/// <c>LoggerMessage</c> delegates for the relay. No delegate accepts an
/// exception: the parameters are strings and numbers, so passing an exception
/// would be both unnecessary and a leak vector.
/// </summary>
internal static partial class OutboxLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Outbox relay started with a {TickSeconds}s tick and a batch of {BatchSize}.")]
    public static partial void RelayStarted(
        ILogger logger,
        double tickSeconds,
        int batchSize);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Outbox relay tick skipped - another instance holds the relay lock.")]
    public static partial void TickSkippedLockHeld(ILogger logger);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Outbox relay tick failed ({ExceptionType} at {FirstFrame}); "
            + "the next tick retries.")]
    public static partial void TickFailed(
        ILogger logger,
        string exceptionType,
        string firstFrame);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Outbox message {MessageId} of kind {Kind} could not be claimed for "
            + "dispatch (errorCode {ErrorCode}); it stays staged.")]
    public static partial void ClaimFailed(
        ILogger logger,
        string messageId,
        string kind,
        string errorCode);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Debug,
        Message = "Outbox message {MessageId} of kind {Kind} failed on attempt "
            + "{Attempts} (errorCode {ErrorCode}); next attempt in {BackoffSeconds}s.")]
    public static partial void DispatchFailed(
        ILogger logger,
        string messageId,
        string kind,
        int attempts,
        string errorCode,
        double backoffSeconds);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "Outbox message {MessageId} of kind {Kind} was ABANDONED after "
            + "{Attempts} attempts (lastFailureCode {LastFailureCode}). Its payload is "
            + "kept on the row; an operator must decide whether to re-stage it.")]
    public static partial void MessageAbandoned(
        ILogger logger,
        string messageId,
        string kind,
        int attempts,
        string lastFailureCode);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Warning,
        Message = "Outbox message {MessageId} of kind {Kind} moved to {Status} but the "
            + "state could not be recorded (errorCode {ErrorCode}); the message is "
            + "dispatched again, which is the at-least-once contract.")]
    public static partial void RecordFailed(
        ILogger logger,
        string messageId,
        string kind,
        string status,
        string errorCode);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Warning,
        Message = "Outbox retention sweep could not delete settled rows (errorCode "
            + "{ErrorCode}); the rows stay and the next tick tries again.")]
    public static partial void PruneFailed(ILogger logger, string errorCode);
}
