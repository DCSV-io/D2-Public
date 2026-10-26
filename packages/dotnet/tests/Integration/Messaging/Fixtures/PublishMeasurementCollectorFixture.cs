// -----------------------------------------------------------------------
// <copyright file="PublishMeasurementCollectorFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging.Fixtures;

using System;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using DcsvIo.D2.Messaging.RabbitMq.Telemetry;

/// <summary>
/// Captures the publisher's counter measurements with the tags each one
/// carried, and signals the first returned publish, so a wait can follow that
/// signal and not a guessed retry window.
/// </summary>
/// <remarks>
/// The meter is a process-wide static. The messaging integration tests share
/// one xunit collection and therefore run one at a time, so a collector reads
/// only its own test's measurements; every assertion is still written as a
/// delta over the span the collector was alive, not as an absolute total.
/// </remarks>
internal sealed class PublishMeasurementCollectorFixture : IDisposable
{
    /// <summary>Instrument name of the returned-publish counter.</summary>
    public const string PUBLISH_RETURNS = "d2.messaging.rabbitmq.publish_returns";

    /// <summary>Instrument name of the publish-retry counter.</summary>
    public const string PUBLISH_RETRIES = "d2.messaging.rabbitmq.publish_retries";

    /// <summary>Instrument name of the terminal publish-failure counter.</summary>
    public const string PUBLISH_FAILURES = "d2.messaging.rabbitmq.publish_failures";

    private readonly MeterListener r_listener;

    private readonly ConcurrentQueue<(string Instrument, long Value, string? Outcome)>
        r_taken = new();

    private readonly TaskCompletionSource r_firstReturn =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Initializes the collector and starts listening.</summary>
    public PublishMeasurementCollectorFixture()
    {
        r_listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == MessagingTelemetry.SOURCE_NAME)
                    listener.EnableMeasurementEvents(instrument);
            },
        };

        r_listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            string? outcome = null;
            foreach (var tag in tags)
            {
                if (tag.Key == MessagingMetricTags.OUTCOME)
                    outcome = tag.Value as string;
            }

            r_taken.Enqueue((instrument.Name, value, outcome));

            if (instrument.Name == PUBLISH_RETURNS)
                r_firstReturn.TrySetResult();
        });

        r_listener.Start();
    }

    /// <summary>
    /// Waits for the first publish the broker returns as unroutable, bounded by
    /// a poll-attempt budget. Each iteration awaits, so a slow machine takes
    /// longer rather than expiring mid-progress; a healthy run resolves in a
    /// couple of iterations. The budget bounds the wait, so a publisher that has
    /// stopped reporting returns does not leave the caller waiting forever: the
    /// caller gets <see langword="false"/> and its assertions say what is wrong.
    /// </summary>
    /// <param name="pollAttemptBudget">Maximum poll attempts.</param>
    /// <returns>
    /// <see langword="true"/> when a return was observed; <see langword="false"/>
    /// when the budget ran out first.
    /// </returns>
    public async Task<bool> WaitForFirstReturnAsync(int pollAttemptBudget = 400)
    {
        for (var attempt = 0; attempt < pollAttemptBudget; attempt++)
        {
            if (r_firstReturn.Task.IsCompleted) return true;
            await Task.Delay(25);
        }

        return false;
    }

    /// <summary>Sums one instrument's measurements.</summary>
    /// <param name="instrumentName">The instrument's wire name.</param>
    /// <returns>The total.</returns>
    public long Total(string instrumentName)
        => r_taken.Where(m => m.Instrument == instrumentName).Sum(m => m.Value);

    /// <summary>Sums one instrument's measurements carrying one outcome tag.</summary>
    /// <param name="instrumentName">The instrument's wire name.</param>
    /// <param name="outcome">The outcome tag value to filter on.</param>
    /// <returns>The total.</returns>
    public long TotalFor(string instrumentName, string outcome)
        => r_taken
            .Where(m => m.Instrument == instrumentName && m.Outcome == outcome)
            .Sum(m => m.Value);

    /// <inheritdoc/>
    public void Dispose() => r_listener.Dispose();
}
