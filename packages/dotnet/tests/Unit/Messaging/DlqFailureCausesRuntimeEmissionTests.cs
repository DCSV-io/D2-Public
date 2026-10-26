// -----------------------------------------------------------------------
// <copyright file="DlqFailureCausesRuntimeEmissionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Messaging.RabbitMq.Subscribing;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Runtime-emission pin tests for the DlqFailureCauses closed set of values.
/// The DlqFailureMetadata source generator takes the cause names from the
/// spec, which prevents drift in the names. This suite prevents drift in the
/// values: each cause is emitted through the production
/// DlqFailureHeaderBuilder paths and checked, byte for byte, in the serialized
/// header. A change that breaks an emit path, such as dropping the
/// DECRYPT_FAILURE wiring on the body-decode boundary, fails no test that
/// checks names only, but it breaks DLQ operations tooling that filters on the
/// cause value.
/// </summary>
public sealed class DlqFailureCausesRuntimeEmissionTests
{
    [Fact]
    public void HandlerResultFailure_CauseValueLandsInSerializedHeader()
    {
        // Production emit path: SubscriberChannel dispatches a handler that
        // returns a non-Ok D2Result -> DlqFailureHeaderBuilder.FromResult.
        var bytes = DlqFailureHeaderBuilder.FromResult(D2Result.NotFound());
        ReadCauseFromHeader(bytes)
            .Should().Be("HANDLER_RESULT_FAILURE");
    }

    [Fact]
    public void HandlerException_CauseValueLandsInSerializedHeader()
    {
        // Production emit path: SubscriberChannel catches an unhandled
        // exception from the handler -> DlqFailureHeaderBuilder.FromException.
        var bytes = DlqFailureHeaderBuilder.FromException(
            new InvalidOperationException("ignored"));
        ReadCauseFromHeader(bytes)
            .Should().Be("HANDLER_EXCEPTION");
    }

    [Fact]
    public void DecryptFailure_CauseValueLandsInSerializedHeader()
    {
        // Production emit path: SubscriberChannel's body-decode boundary
        // wraps a decrypt exception -> DlqFailureHeaderBuilder.FromBoundary
        // with cause=DECRYPT_FAILURE.
        var bytes = DlqFailureHeaderBuilder.FromBoundary(
            DlqFailureCauses.DECRYPT_FAILURE,
            new InvalidOperationException("bad frame"));
        ReadCauseFromHeader(bytes)
            .Should().Be("DECRYPT_FAILURE");
    }

    [Fact]
    public void DeserializeFailure_CauseValueLandsInSerializedHeader()
    {
        // Production emit path: SubscriberChannel's body-decode boundary
        // wraps a deserialize exception -> DlqFailureHeaderBuilder.FromBoundary
        // with cause=DESERIALIZE_FAILURE.
        var bytes = DlqFailureHeaderBuilder.FromBoundary(
            DlqFailureCauses.DESERIALIZE_FAILURE,
            new JsonException("malformed"));
        ReadCauseFromHeader(bytes)
            .Should().Be("DESERIALIZE_FAILURE");
    }

    [Fact]
    public void RetriesExhausted_CauseValueLandsInSerializedHeader()
    {
        // Production emit path: SubscriberChannel detects x-death attempts
        // that exceed MaxAttempts before it invokes the handler ->
        // DlqFailureHeaderBuilder.FromRetriesExhausted.
        var bytes = DlqFailureHeaderBuilder.FromRetriesExhausted(attemptCount: 5);
        ReadCauseFromHeader(bytes)
            .Should().Be("RETRIES_EXHAUSTED");
    }

    [Theory]
    [InlineData("HANDLER_RESULT_FAILURE")]
    [InlineData("HANDLER_EXCEPTION")]
    [InlineData("DECRYPT_FAILURE")]
    [InlineData("DESERIALIZE_FAILURE")]
    [InlineData("RETRIES_EXHAUSTED")]
    public void EveryCatalogValue_HasMatchingConstant(string expectedValue)
    {
        // Catalog completeness: each closed-set value listed in
        // contracts/dlq-failure-metadata/dlq-failure-metadata.spec.json has a
        // matching DlqFailureCauses constant whose value is the literal wire
        // string. A difference here would break operations dashboards that
        // filter on the cause value.
        var actualValue = expectedValue switch
        {
            "HANDLER_RESULT_FAILURE" => DlqFailureCauses.HANDLER_RESULT_FAILURE,
            "HANDLER_EXCEPTION" => DlqFailureCauses.HANDLER_EXCEPTION,
            "DECRYPT_FAILURE" => DlqFailureCauses.DECRYPT_FAILURE,
            "DESERIALIZE_FAILURE" => DlqFailureCauses.DESERIALIZE_FAILURE,
            "RETRIES_EXHAUSTED" => DlqFailureCauses.RETRIES_EXHAUSTED,
            _ => throw new InvalidOperationException("unmapped"),
        };
        actualValue.Should().Be(expectedValue);
    }

    private static string ReadCauseFromHeader(byte[] bytes)
    {
        var json = Encoding.UTF8.GetString(bytes);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("cause").GetString()
            ?? throw new InvalidOperationException("cause was null");
    }
}
