// -----------------------------------------------------------------------
// <copyright file="TestActivityCollector.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Handler;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DcsvIo.D2.Handler;

/// <summary>
/// Captures every <see cref="Activity"/> emitted by the
/// <see cref="HandlerTelemetry.SR_ActivitySource"/> during the lifetime of the
/// collector. Disposes the listener on <see cref="Dispose"/>. Uses a
/// <see cref="ConcurrentQueue{T}"/> because the <c>ActivityStopped</c> callback
/// runs on whichever thread stopped the activity, and because
/// <see cref="Last"/> means the most recently stopped one - which an unordered
/// concurrent collection could not answer.
/// </summary>
internal sealed class TestActivityCollector : IDisposable
{
    private readonly ActivityListener r_listener;
    private readonly ConcurrentQueue<Activity> r_activities = new();

    public TestActivityCollector()
    {
        r_listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == HandlerTelemetry.SourceName,
            Sample = (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = r_activities.Enqueue,
        };
        ActivitySource.AddActivityListener(r_listener);
    }

    public IReadOnlyCollection<Activity> Activities => r_activities;

    public Activity? Last => r_activities.LastOrDefault();

    public void Dispose() => r_listener.Dispose();
}
