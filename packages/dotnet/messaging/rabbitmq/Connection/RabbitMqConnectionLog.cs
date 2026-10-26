// -----------------------------------------------------------------------
// <copyright file="RabbitMqConnectionLog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Connection;

using Microsoft.Extensions.Logging;

/// <summary><c>LoggerMessage</c> delegates for the connection layer.</summary>
internal static partial class RabbitMqConnectionLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "RabbitMQ connection opened to {Host}:{Port} (attempt {Attempt}).")]
    public static partial void ConnectionOpened(
        ILogger logger, string host, int port, int attempt);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "RabbitMQ reconnect attempt {Attempt} failed (exType={ExType}); "
            + "next try in {Delay}. Publishers return ServiceUnavailable, "
            + "consumers idle until reconnect succeeds.")]
    public static partial void ReconnectAttemptFailed(
        ILogger logger, int attempt, TimeSpan delay, string exType);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "RabbitMQ connection close raised an error (exType={ExType}); "
            + "ignoring on shutdown.")]
    public static partial void ConnectionCloseFailed(ILogger logger, string exType);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "RabbitMQ ConnectionStartupHostedService starting "
            + "background reconnect loop (host stays up while connection "
            + "establishes).")]
    public static partial void StartupOpening(ILogger logger);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "RabbitMQ connection has been degraded for {Down}. "
            + "RabbitMQ.Client automatic recovery should restore it; if it "
            + "stays down, restart this replica to force reconnection. "
            + "Publishers return ServiceUnavailable; consumers idle.")]
    public static partial void ConnectionDegraded(ILogger logger, TimeSpan down);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "RabbitMQ connection recovered after {Down} of degradation.")]
    public static partial void ConnectionRecovered(ILogger logger, TimeSpan down);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Warning,
        Message = "RabbitMQ connection shut down (initiator={Initiator}, "
            + "replyCode={ReplyCode}, replyText={ReplyText}). Automatic recovery will "
            + "re-open the connection and re-declare topology; publishes issued before "
            + "the queue bindings are back can reach no queue.")]
    public static partial void ConnectionShutdown(
        ILogger logger, string initiator, ushort replyCode, string replyText);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Warning,
        Message = "RabbitMQ automatic recovery succeeded; the client re-opened the "
            + "connection and re-declared the topology it owns.")]
    public static partial void RecoverySucceeded(ILogger logger);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Error,
        Message = "RabbitMQ automatic recovery failed (exType={ExType}); the client "
            + "will retry. Consumers stay detached until it succeeds.")]
    public static partial void RecoveryFailed(ILogger logger, string exType);

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Error,
        Message = "RabbitMQ connection callback raised (exType={ExType}, "
            + "context={Context}).")]
    public static partial void ConnectionCallbackFailed(
        ILogger logger, string exType, string context);

    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Warning,
        Message = "RabbitMQ is re-attaching consumer {ConsumerTag} after recovery.")]
    public static partial void RecoveringConsumer(ILogger logger, string consumerTag);

    [LoggerMessage(
        EventId = 12,
        Level = LogLevel.Warning,
        Message = "RabbitMQ renamed queue {OldName} to {NewName} during recovery.")]
    public static partial void QueueNameChangedAfterRecovery(
        ILogger logger, string oldName, string newName);

    [LoggerMessage(
        EventId = 13,
        Level = LogLevel.Warning,
        Message = "RabbitMQ blocked the connection (reason={Reason}); publishes stall "
            + "until the broker unblocks it.")]
    public static partial void ConnectionBlocked(ILogger logger, string reason);

    [LoggerMessage(
        EventId = 14,
        Level = LogLevel.Information,
        Message = "RabbitMQ unblocked the connection.")]
    public static partial void ConnectionUnblocked(ILogger logger);
}
