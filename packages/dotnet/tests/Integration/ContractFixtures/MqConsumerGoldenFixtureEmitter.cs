// -----------------------------------------------------------------------
// <copyright file="MqConsumerGoldenFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Headers.Amqp;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Tests.Integration.ContractFixtures.Fixtures;
using Xunit;

/// <summary>
/// Emits the golden wire messages (body bytes and AMQP headers) that the
/// TypeScript RabbitMQ consumer integration suite replays through a real broker.
/// The plaintext body is serialized with <see cref="MessagingJsonOptions.Options"/>
/// and the <c>x-d2-context</c> header is produced by
/// <see cref="PropagatedContextSerializer.Encode(PropagatedContext)"/>, so the
/// TypeScript consumer decodes .NET wire output. Bodies are base64-encoded in the
/// fixture JSON.
/// </summary>
public sealed class MqConsumerGoldenFixtureEmitter
{
    /// <summary>The fixture catalog this emitter writes.</summary>
    internal const string CATALOG = "mq-consumer-golden";

    private const string _PRODUCER_TRACE_ID = "4bf92f3577b34da6a3ce929d0e0e4736";
    private const string _TRACEPARENT =
        "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

    private const string _EXCHANGE = "example.orders.placed";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_PlaintextMessage()
    {
        var message = new SampleOrderPlaced(
            OrderId: "ord-golden-0001",
            CustomerRef: "cust-golden-0001",
            AmountMinor: 12500,
            PlacedAt: "2026-07-01T12:00:00Z");

        var body = JsonSerializer.SerializeToUtf8Bytes(message, MessagingJsonOptions.Options);

        JsonSerializer.Deserialize<SampleOrderPlaced>(body, MessagingJsonOptions.Options)
            .Should().Be(message, "the golden body must decode to the message it was built from");

        var context = new PropagatedContext
        {
            RequestId = "req-golden-1",
            RequestPath = "/v1/orders/place",
            WhoIsHashId = "whois-golden-0001",
        };

        var headers = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            [AmqpHeaders.CONTENT_TYPE] = "application/octet-stream",
            [AmqpHeaders.PROTO_TYPE] = typeof(SampleOrderPlaced).FullName,
            [AmqpHeaders.MESSAGE_ID] = "0192f8c1-1111-7000-8000-0000000000aa",
            [AmqpHeaders.TRACEPARENT] = _TRACEPARENT,
            [AmqpHeaders.PROPAGATED_CONTEXT] = PropagatedContextSerializer.Encode(context),
        };

        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["exchange"] = _EXCHANGE,
            ["exchangeType"] = "fanout",
            ["routingKey"] = string.Empty,
            ["bodyBase64"] = Convert.ToBase64String(body),
            ["headers"] = headers,
            ["expectedDecoded"] = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["orderId"] = message.OrderId,
                ["customerRef"] = message.CustomerRef,
                ["amountMinor"] = message.AmountMinor,
                ["placedAt"] = message.PlacedAt,
            },
            ["producerTraceId"] = _PRODUCER_TRACE_ID,
            ["expectedRequestId"] = context.RequestId,
            ["expectedRequestPath"] = context.RequestPath,
        };

        FixturePathHelpers.WriteFixture(CATALOG, "plaintext-message", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_EncryptedFrame()
    {
        // A version-1 symmetric encryption frame. The TypeScript consumer selects
        // its DECRYPT_FAILURE dead-letter path by this version byte, so the
        // replay needs no keyring.
        var frame = new byte[40];
        frame[0] = 1; // EncryptionFrame.CURRENT_VERSION
        frame[1] = 4; // kid length
        frame[2] = (byte)'k';
        frame[3] = (byte)'1';
        frame[4] = (byte)'-';
        frame[5] = (byte)'a';

        for (var i = 6; i < frame.Length; i++)
            frame[i] = (byte)((i * 7) % 251);

        var headers = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            [AmqpHeaders.CONTENT_TYPE] = "application/octet-stream",
            [AmqpHeaders.PROTO_TYPE] = "DcsvIo.D2.Sample.EncryptedFixtureEvent",
            [AmqpHeaders.MESSAGE_ID] = "0192f8c1-2222-7000-8000-0000000000bb",
            [AmqpHeaders.ENCRYPTION_KID] = "k1-a",
        };

        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["bodyBase64"] = Convert.ToBase64String(frame),
            ["headers"] = headers,
        };

        FixturePathHelpers.WriteFixture(CATALOG, "encrypted-frame", data);
    }
}
