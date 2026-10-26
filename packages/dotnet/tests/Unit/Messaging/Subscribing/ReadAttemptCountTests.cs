// -----------------------------------------------------------------------
// <copyright file="ReadAttemptCountTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Subscribing;

using AwesomeAssertions;
using DcsvIo.D2.Messaging.RabbitMq.Subscribing;
using global::RabbitMQ.Client.Events;
using Xunit;

/// <summary>
/// Tests of <see cref="SubscriberChannel.ReadAttemptCount"/>, the x-death
/// header parser that decides the RETRIES_EXHAUSTED branch. The reason filter
/// must accept only <c>expired</c> and <c>rejected</c> entries (not
/// <c>maxlen</c> or <c>delivery_limit</c>). On malformed input it must fail
/// open and return zero, so that a poisoned message cannot strand a queue.
/// </summary>
public sealed class ReadAttemptCountTests
{
    [Fact]
    public void OnlyCountsExpiredAndRejected()
    {
        // The x-death entries are expired (count=2), rejected (count=1) and
        // maxlen (count=99). The maxlen entry is broker-side flow control and
        // not a consumer-side retry, so counting it would trigger
        // RETRIES_EXHAUSTED too early on a busy queue.
        var ea = BuildXDeathDelivery(
            (Reason: "expired", Count: 2L),
            (Reason: "rejected", Count: 1L),
            (Reason: "maxlen", Count: 99L));

        SubscriberChannel.ReadAttemptCount(ea).Should().Be(
            3,
            "only expired+rejected entries are retry-cycle events; "
            + "maxlen is broker-side flow control and must NOT count");
    }

    [Fact]
    public void DeliveryLimitNotCounted()
    {
        var ea = BuildXDeathDelivery(
            (Reason: "delivery_limit", Count: 5L),
            (Reason: "expired", Count: 1L));
        SubscriberChannel.ReadAttemptCount(ea).Should().Be(1);
    }

    [Fact]
    public void ReasonAsByteArray_HandledCorrectly()
    {
        // RabbitMQ.Client deserializes table values as byte[] for short-string
        // fields, so the filter must handle reason values of both string and
        // byte[] type.
        var ea = BuildXDeathDelivery(("expired", 4L, AsBytes: true));
        SubscriberChannel.ReadAttemptCount(ea).Should().Be(4);
    }

    [Fact]
    public void NoXDeathHeader_ReturnsZero()
    {
        var ea = new BasicDeliverEventArgs(
            consumerTag: "tag",
            deliveryTag: 1,
            redelivered: false,
            exchange: "ex",
            routingKey: "rk",
            properties: new global::RabbitMQ.Client.BasicProperties(),
            body: ReadOnlyMemory<byte>.Empty);
        SubscriberChannel.ReadAttemptCount(ea).Should().Be(0);
    }

    [Fact]
    public void MalformedXDeathNotAList_ReturnsZero()
    {
        var props = new global::RabbitMQ.Client.BasicProperties
        {
            Headers = new Dictionary<string, object?>
            {
                ["x-death"] = "not-a-list",
            },
        };
        var ea = new BasicDeliverEventArgs(
            consumerTag: "tag",
            deliveryTag: 1,
            redelivered: false,
            exchange: "ex",
            routingKey: "rk",
            properties: props,
            body: ReadOnlyMemory<byte>.Empty);
        SubscriberChannel.ReadAttemptCount(ea).Should().Be(0);
    }

    [Fact]
    public void CountAsString_FallsThroughToZero()
    {
        // Defensive: a broker or proxy that serializes count as a string must
        // not cause a NullReferenceException in the consumer. The method fails
        // open and returns zero, so retries continue and the message is not
        // stranded.
        var ea = BuildXDeathDelivery(("expired", Count: "3"));
        SubscriberChannel.ReadAttemptCount(ea).Should().Be(0);
    }

    [Fact]
    public void MultipleEntries_SumsCounts()
    {
        var ea = BuildXDeathDelivery(
            (Reason: "expired", Count: 1L),
            (Reason: "expired", Count: 2L),
            (Reason: "rejected", Count: 3L));
        SubscriberChannel.ReadAttemptCount(ea).Should().Be(6);
    }

    private static BasicDeliverEventArgs BuildXDeathDelivery(
        params (string Reason, long Count)[] entries)
    {
        var list = entries
            .Select<(string Reason, long Count), object?>(e =>
                new Dictionary<string, object?>
                {
                    ["reason"] = e.Reason,
                    ["count"] = e.Count,
                })
            .ToList();
        return BuildDeliveryWithXDeath(list);
    }

    private static BasicDeliverEventArgs BuildXDeathDelivery(
        params (string Reason, long Count, bool AsBytes)[] entries)
    {
        var list = entries
            .Select<(string Reason, long Count, bool AsBytes), object?>(e =>
                new Dictionary<string, object?>
                {
                    ["reason"] = e.AsBytes
                        ? System.Text.Encoding.UTF8.GetBytes(e.Reason)
                        : e.Reason,
                    ["count"] = e.Count,
                })
            .ToList();
        return BuildDeliveryWithXDeath(list);
    }

    private static BasicDeliverEventArgs BuildXDeathDelivery(
        params (string Reason, object Count)[] entries)
    {
        var list = entries
            .Select<(string Reason, object Count), object?>(e =>
                new Dictionary<string, object?>
                {
                    ["reason"] = e.Reason,
                    ["count"] = e.Count,
                })
            .ToList();
        return BuildDeliveryWithXDeath(list);
    }

    private static BasicDeliverEventArgs BuildDeliveryWithXDeath(IList<object?> entries)
    {
        var props = new global::RabbitMQ.Client.BasicProperties
        {
            Headers = new Dictionary<string, object?>
            {
                ["x-death"] = entries,
            },
        };
        return new BasicDeliverEventArgs(
            consumerTag: "tag",
            deliveryTag: 1,
            redelivered: true,
            exchange: "ex",
            routingKey: "rk",
            properties: props,
            body: ReadOnlyMemory<byte>.Empty);
    }
}
