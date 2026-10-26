// -----------------------------------------------------------------------
// <copyright file="MessagingMetricTagsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Telemetry;

using AwesomeAssertions;
using DcsvIo.D2.Messaging.RabbitMq.Telemetry;
using Xunit;

/// <summary>
/// Pins the wire value of every messaging metric tag key and tag value. A
/// dashboard or alert is written against these strings, so renaming one is a
/// break for whoever reads the metric rather than a rename inside the library.
/// </summary>
public sealed class MessagingMetricTagsTests
{
    [Fact]
    public void Outcome_WireValue_IsOutcome()
    {
        MessagingMetricTags.OUTCOME.Should().Be("outcome");
    }

    [Fact]
    public void PublishFailUnroutable_WireValue_IsPublishFailUnroutable()
    {
        MessagingMetricTags.PUBLISH_FAIL_UNROUTABLE
            .Should().Be("publish_fail_unroutable");
    }

    [Fact]
    public void PublishFailTransport_WireValue_IsPublishFailTransport()
    {
        MessagingMetricTags.PUBLISH_FAIL_TRANSPORT
            .Should().Be("publish_fail_transport");
    }

    [Fact]
    public void RepublishFailUnroutable_WireValue_IsRepublishFailUnroutable()
    {
        MessagingMetricTags.REPUBLISH_FAIL_UNROUTABLE
            .Should().Be("republish_fail_unroutable");
    }

    [Fact]
    public void RepublishFailTransport_WireValue_IsRepublishFailTransport()
    {
        MessagingMetricTags.REPUBLISH_FAIL_TRANSPORT
            .Should().Be("republish_fail_transport");
    }
}
