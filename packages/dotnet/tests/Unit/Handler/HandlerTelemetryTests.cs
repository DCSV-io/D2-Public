// -----------------------------------------------------------------------
// <copyright file="HandlerTelemetryTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Handler;

using System.Collections.Generic;
using System.Diagnostics.Metrics;
using AwesomeAssertions;
using DcsvIo.D2.Handler;
using Xunit;

// Same serialization as BaseHandlerTests - both subscribe to the static
// HandlerTelemetry telemetry surfaces.
[Collection("HandlerTelemetrySerial")]
public sealed class HandlerTelemetryTests
{
    [Fact]
    public void SourceName_MatchesAssemblyIdentifier()
    {
        HandlerTelemetry.SourceName.Should().Be("DcsvIo.D2.Handler");
    }

    [Fact]
    public void ActivitySource_HasMatchingName()
    {
        HandlerTelemetry.SR_ActivitySource.Name.Should().Be(HandlerTelemetry.SourceName);
    }

    [Fact]
    public void Meter_HasMatchingName()
    {
        HandlerTelemetry.SR_Meter.Name.Should().Be(HandlerTelemetry.SourceName);
    }

    // ----------------------------------------------------------------------
    // Instrument identity: name, unit and description. These strings are the
    // public contract, because dashboards and alerts key on them, and a typo
    // in one is a silent production incident. Each instrument has its own test.
    // ----------------------------------------------------------------------

    [Fact]
    public void Invoked_NameAndUnitAndDescription_Match()
    {
        HandlerTelemetry.SR_Invoked.Name.Should().Be("d2.handler.invoked");
        HandlerTelemetry.SR_Invoked.Unit.Should().Be("{calls}");
        HandlerTelemetry.SR_Invoked.Description.Should().Be("Handler invocations attempted.");
    }

    [Fact]
    public void Succeeded_NameAndUnitAndDescription_Match()
    {
        HandlerTelemetry.SR_Succeeded.Name.Should().Be("d2.handler.succeeded");
        HandlerTelemetry.SR_Succeeded.Unit.Should().Be("{calls}");
        HandlerTelemetry.SR_Succeeded.Description.Should()
            .Be("Handler invocations that returned a successful D2Result.");
    }

    [Fact]
    public void Failed_NameAndUnitAndDescription_Match()
    {
        HandlerTelemetry.SR_Failed.Name.Should().Be("d2.handler.failed");
        HandlerTelemetry.SR_Failed.Unit.Should().Be("{calls}");
        HandlerTelemetry.SR_Failed.Description.Should()
            .Be("Handler invocations that returned a failed D2Result or threw.");
    }

    [Fact]
    public void Duration_NameAndUnitAndDescription_Match()
    {
        HandlerTelemetry.SR_Duration.Name.Should().Be("d2.handler.duration");
        HandlerTelemetry.SR_Duration.Unit.Should().Be("ms");
        HandlerTelemetry.SR_Duration.Description.Should()
            .Be("Handler invocation wall-clock duration in milliseconds.");
    }

    // ----------------------------------------------------------------------
    // Listener wiring: the activity source, the SR_Invoked counter and the
    // SR_Duration histogram are observable through the standard
    // System.Diagnostics listeners and not only through this library's own code.
    // ----------------------------------------------------------------------

    [Fact]
    public void ActivityListener_ReceivesActivitiesStartedOnSource()
    {
        using var collector = new TestActivityCollector();

        // ReSharper disable once ExplicitCallerInfoArgument - explicit name is the test contract
        using (var activity = HandlerTelemetry.SR_ActivitySource.StartActivity("TestActivity"))
        {
            activity.Should().NotBeNull();
        }

        collector.Activities.Should().NotBeEmpty();
        collector.Last!.OperationName.Should().Be("TestActivity");
    }

    [Fact]
    public void ActivityListener_ActivitiesStoppedOnParallelThreads_CapturesEveryOne()
    {
        // ActivityStopped runs on whichever thread stopped the activity, so the
        // collector's own store has to tolerate concurrent writers. A List loses
        // entries or throws from inside the callback.
        const int activity_count = 64;
        const string operation_name = "ParallelTestActivity";
        using var collector = new TestActivityCollector();

        Parallel.For(0, activity_count, _ =>
        {
            // ReSharper disable once ExplicitCallerInfoArgument -- the name is the test contract
            using var activity = HandlerTelemetry.SR_ActivitySource.StartActivity(operation_name);
            activity.Should().NotBeNull();
        });

        collector.Activities.Count(a => a.OperationName == operation_name)
            .Should().Be(activity_count);
    }

    [Fact]
    public void MeterListener_ReceivesCounterIncrement()
    {
        using var collector = new TestMetricCollector();

        HandlerTelemetry.SR_Invoked.Add(1);

        collector.CountFor("d2.handler.invoked").Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void MeterListener_ReceivesHistogramRecord()
    {
        using var collector = new TestMetricCollector();

        HandlerTelemetry.SR_Duration.Record(42.0);

        collector.ValuesFor("d2.handler.duration").Should().Contain(42.0);
    }

    [Fact]
    public void TagKeyConstants_CarryTheEmittedKeyStrings()
    {
        // Dashboards and log queries key on these strings, and each is written
        // at a span-tag site and again at a log-scope site in BaseHandler.
        HandlerTelemetry.Tags.HANDLER_NAME.Should().Be("d2.handler.name");
        HandlerTelemetry.Tags.TRACE_ID.Should().Be("d2.trace_id");
        HandlerTelemetry.Tags.USER_ID.Should().Be("d2.user_id");
        HandlerTelemetry.Tags.ORG_ID.Should().Be("d2.org_id");
        HandlerTelemetry.Tags.ORG_TYPE.Should().Be("d2.org_type");
        HandlerTelemetry.Tags.ORG_ROLE.Should().Be("d2.org_role");
        HandlerTelemetry.Tags.IMPERSONATING.Should().Be("d2.impersonating");
        HandlerTelemetry.Tags.IMPERSONATION_KIND.Should().Be("d2.impersonation_kind");
        HandlerTelemetry.Tags.IMPERSONATOR_ID.Should().Be("d2.impersonator_id");
        HandlerTelemetry.Tags.IMPERSONATOR_ORG_ID.Should().Be("d2.impersonator_org_id");
        HandlerTelemetry.Tags.IMPERSONATOR_ORG_TYPE.Should().Be("d2.impersonator_org_type");
        HandlerTelemetry.Tags.IMPERSONATOR_ORG_ROLE.Should().Be("d2.impersonator_org_role");
    }

    [Fact]
    public void Meter_PublishesTheInstrumentFieldInstances()
    {
        // A MeterListener sees the instruments the handler meter created. Each one
        // with a field's name must be the very instance that field holds, because
        // an instrument created again at run time would not be the one a listener
        // subscribed to. The meter is public, so the test counts no other
        // instrument on it.
        Instrument[] fields =
        [
            HandlerTelemetry.SR_Invoked,
            HandlerTelemetry.SR_Succeeded,
            HandlerTelemetry.SR_Failed,
            HandlerTelemetry.SR_Duration,
        ];

        var published = new List<Instrument>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (ReferenceEquals(instrument.Meter, HandlerTelemetry.SR_Meter))
                published.Add(instrument);
        };

        listener.Start();

        foreach (var field in fields)
        {
            published.Where(i => string.Equals(i.Name, field.Name, StringComparison.Ordinal))
                .Should().ContainSingle()
                .Which.Should().BeSameAs(field);
        }
    }
}
