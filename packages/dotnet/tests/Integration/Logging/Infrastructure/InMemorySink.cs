// -----------------------------------------------------------------------
// <copyright file="InMemorySink.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Logging.Infrastructure;

using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;

/// <summary>
/// Hand-rolled <see cref="ILogEventSink"/> that captures every emitted
/// <see cref="LogEvent"/> into an in-memory <see cref="ConcurrentBag{T}"/>.
/// Lets tests assert what the Serilog pipeline actually emits, without a
/// dependency on the <c>Serilog.Sinks.InMemory</c> NuGet package (the
/// sink is small and the project keeps its test dependencies few).
/// </summary>
internal sealed class InMemorySink : ILogEventSink
{
    private readonly ConcurrentBag<LogEvent> _events = new();
    private readonly ITextFormatter sr_formatter = new CompactJsonFormatter();

    /// <summary>
    /// Gets a snapshot of every <see cref="LogEvent"/> captured since the
    /// last <see cref="Reset"/>.
    /// </summary>
    public IReadOnlyCollection<LogEvent> Events => _events.ToArray();

    /// <inheritdoc />
    public void Emit(LogEvent logEvent) => _events.Add(logEvent);

    /// <summary>
    /// Discards every captured event so a single test method can re-use the
    /// sink across multiple HTTP calls without cross-talk.
    /// </summary>
    public void Reset()
    {
        // Drain all pending events; TryTake return is consumed by the loop condition.
        while (_events.TryTake(out _))
        {
        }
    }

    /// <summary>
    /// Renders <paramref name="logEvent"/> through the canonical
    /// <see cref="CompactJsonFormatter"/> so tests can assert on the actual
    /// JSON output shape (the formatter output is what the library promises,
    /// so asserting on the JSON shape catches formatter drift).
    /// </summary>
    /// <param name="logEvent">The captured event to render.</param>
    /// <returns>The compact-JSON-formatted log line.</returns>
    public string Render(LogEvent logEvent)
    {
        using var writer = new System.IO.StringWriter();
        sr_formatter.Format(logEvent, writer);
        return writer.ToString();
    }
}
