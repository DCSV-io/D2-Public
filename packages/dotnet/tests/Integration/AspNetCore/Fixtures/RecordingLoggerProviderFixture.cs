// -----------------------------------------------------------------------
// <copyright file="RecordingLoggerProviderFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.AspNetCore.Fixtures;

using System.Collections.Concurrent;
using global::Microsoft.Extensions.Logging;

/// <summary>
/// An <see cref="ILoggerProvider"/> that keeps every rendered line, so a test
/// can assert what a middleware wrote and what it did not.
/// </summary>
internal sealed class RecordingLoggerProviderFixture : ILoggerProvider
{
    private readonly ConcurrentQueue<(int EventId, string Message)> r_lines = new();

    /// <summary>Gets every line written through this provider, in order.</summary>
    public IReadOnlyList<(int EventId, string Message)> Lines => [.. r_lines];

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new RecordingLoggerFixture(r_lines);

    /// <inheritdoc />
    public void Dispose()
    {
        // Nothing is held open: the queue is managed memory the test owns.
    }

    private sealed class RecordingLoggerFixture(
        ConcurrentQueue<(int EventId, string Message)> lines)
        : ILogger
    {
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
            ArgumentNullException.ThrowIfNull(formatter);

            lines.Enqueue((eventId.Id, formatter(state, exception)));
        }
    }
}
