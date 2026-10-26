// -----------------------------------------------------------------------
// <copyright file="DlqCleanupLoggerFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Subscribing.Fixtures;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

/// <summary>Records cleanup diagnostics and optionally rejects their emission.</summary>
public sealed class DlqCleanupLoggerFixture(bool throwOnLog) : ILogger
{
    private readonly ConcurrentQueue<string> r_messages = new();

    public string[] Messages => r_messages.ToArray();

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        r_messages.Enqueue(formatter(state, exception));
        if (throwOnLog) throw new InvalidOperationException("Diagnostic emission failed.");
    }
}
