// -----------------------------------------------------------------------
// <copyright file="MessagingTelemetryInstrumentTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Telemetry;

using AwesomeAssertions;
using DcsvIo.D2.Messaging.RabbitMq.Telemetry;
using Xunit;

/// <summary>
/// Pins the wire identity of the returned-publish counter. A dashboard or an
/// alert is written against the instrument name and the meter name, so either
/// one moving is a break for whoever reads the metric.
/// </summary>
public sealed class MessagingTelemetryInstrumentTests
{
    [Fact]
    public void PublishReturnsCounter_Name_IsPublishReturns()
    {
        MessagingTelemetry.SR_PublishReturnsCounter.Name
            .Should().Be("d2.messaging.rabbitmq.publish_returns");
    }

    [Fact]
    public void PublishReturnsCounter_Unit_IsPublish()
    {
        MessagingTelemetry.SR_PublishReturnsCounter.Unit.Should().Be("{publish}");
    }

    [Fact]
    public void PublishReturnsCounter_RidesTheLibrarysOwnMeter()
    {
        // The aggregation registration in DcsvIo.D2.Telemetry subscribes by
        // meter name, so a counter on any other meter is never exported.
        MessagingTelemetry.SR_PublishReturnsCounter.Meter.Name
            .Should().Be(MessagingTelemetry.SOURCE_NAME);
    }
}
