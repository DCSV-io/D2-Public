// -----------------------------------------------------------------------
// <copyright file="ErrorCodesRuntimeEmissionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result;

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Runtime-emission pin tests for the <see cref="ErrorCodes"/> closed-set
/// value catalog. The ErrorCodes source generator and the
/// <see cref="ErrorCodesTests"/> name-pin tests cover the error-code NAMES,
/// which closes the name-level drift surface; this suite closes the value-level
/// drift surface. Each enumerated code is built through a production
/// <see cref="D2Result"/> factory (or raw <c>Fail</c> for codes without a
/// semantic factory), and the test verifies that it appears byte-for-byte in the
/// <c>errorCode</c> field of the serialized D2Result envelope. A refactor that
/// wired <see cref="D2Result.Conflict"/> to a different literal would NOT fail any
/// name-only test, but it would break a client's switch on the code, audit-log
/// queries, and alerting rules that filter on the wire <c>errorCode</c> value.
/// Complements the partial coverage in <see cref="D2ResultJsonShapeTests"/>
/// (NOT_FOUND + VALIDATION_FAILED) by covering the catalog values that
/// production factories emit without a dedicated wire-value runtime test.
/// </summary>
public sealed class ErrorCodesRuntimeEmissionTests
{
    [Fact]
    public void Forbidden_ErrorCodeLandsInSerializedEnvelope()
    {
        var result = D2Result.Forbidden();

        ReadErrorCode(result).Should().Be("GEN_FORBIDDEN");
    }

    [Fact]
    public void Unauthorized_ErrorCodeLandsInSerializedEnvelope()
    {
        var result = D2Result.Unauthorized();

        ReadErrorCode(result).Should().Be("GEN_UNAUTHORIZED");
    }

    [Fact]
    public void Conflict_ErrorCodeLandsInSerializedEnvelope()
    {
        var result = D2Result.Conflict();

        ReadErrorCode(result).Should().Be("GEN_CONFLICT");
    }

    [Fact]
    public void UnhandledException_ErrorCodeLandsInSerializedEnvelope()
    {
        var result = D2Result.UnhandledException();

        ReadErrorCode(result).Should().Be("GEN_UNHANDLED_EXCEPTION");
    }

    [Fact]
    public void ServiceUnavailable_ErrorCodeLandsInSerializedEnvelope()
    {
        var result = D2Result.ServiceUnavailable();

        ReadErrorCode(result).Should().Be("GEN_SERVICE_UNAVAILABLE");
    }

    [Fact]
    public void SomeFound_ErrorCodeLandsInSerializedEnvelope()
    {
        var result = D2Result.SomeFound();

        ReadErrorCode(result).Should().Be("GEN_SOME_FOUND");
    }

    [Fact]
    public void PartialSuccess_ErrorCodeLandsInSerializedEnvelope()
    {
        // PartialSuccess exists only on the generic type: the semantic factory is on
        // D2Result<TData>. It reports failure; code that accepts a partial outcome
        // reads the error code.
        var result = D2Result<string>.PartialSuccess(data: "partial");

        ReadErrorCode(result).Should().Be("GEN_PARTIAL_SUCCESS");
    }

    [Fact]
    public void RateLimited_ErrorCodeLandsInSerializedEnvelope()
    {
        // TooManyRequests is the semantic factory; RATE_LIMITED is the
        // default error-code value it emits (overridable for per-domain
        // discrimination).
        var result = D2Result.TooManyRequests();

        ReadErrorCode(result).Should().Be("GEN_RATE_LIMITED");
    }

    [Fact]
    public void PayloadTooLarge_ErrorCodeLandsInSerializedEnvelope()
    {
        var result = D2Result.PayloadTooLarge();

        ReadErrorCode(result).Should().Be("GEN_PAYLOAD_TOO_LARGE");
    }

    [Fact]
    public void Canceled_ErrorCodeLandsInSerializedEnvelope()
    {
        var result = D2Result.Canceled();

        ReadErrorCode(result).Should().Be("GEN_CANCELED");
    }

    [Fact]
    public void CouldNotBeSerialized_ErrorCodeLandsInSerializedEnvelope()
    {
        // No semantic factory for COULD_NOT_BE_SERIALIZED - production
        // emit paths use raw Fail with the catalog constant. This test uses the
        // same emission path, so the wire value is verified at runtime.
        var result = D2Result.Fail(
            statusCode: HttpStatusCode.InternalServerError,
            errorCode: ErrorCodes.GEN_COULD_NOT_BE_SERIALIZED);

        ReadErrorCode(result).Should().Be("GEN_COULD_NOT_BE_SERIALIZED");
    }

    [Fact]
    public void CouldNotBeDeserialized_ErrorCodeLandsInSerializedEnvelope()
    {
        // No semantic factory for COULD_NOT_BE_DESERIALIZED - production
        // emit paths use raw Fail with the catalog constant.
        var result = D2Result.Fail(
            statusCode: HttpStatusCode.InternalServerError,
            errorCode: ErrorCodes.GEN_COULD_NOT_BE_DESERIALIZED);

        ReadErrorCode(result).Should().Be("GEN_COULD_NOT_BE_DESERIALIZED");
    }

    [Fact]
    public void IdempotencyInFlight_ErrorCodeLandsInSerializedEnvelope()
    {
        // No semantic factory for IDEMPOTENCY_IN_FLIGHT - the result is built
        // with raw Fail and the catalog constant (the 409 dedup-collision case).
        var result = D2Result.Fail(
            statusCode: HttpStatusCode.Conflict,
            errorCode: ErrorCodes.GEN_IDEMPOTENCY_IN_FLIGHT);

        ReadErrorCode(result).Should().Be("GEN_IDEMPOTENCY_IN_FLIGHT");
    }

    [Theory]
    [InlineData("GEN_NOT_FOUND")]
    [InlineData("GEN_FORBIDDEN")]
    [InlineData("GEN_UNAUTHORIZED")]
    [InlineData("GEN_VALIDATION_FAILED")]
    [InlineData("GEN_CONFLICT")]
    [InlineData("GEN_UNHANDLED_EXCEPTION")]
    [InlineData("GEN_COULD_NOT_BE_SERIALIZED")]
    [InlineData("GEN_COULD_NOT_BE_DESERIALIZED")]
    [InlineData("GEN_SERVICE_UNAVAILABLE")]
    [InlineData("GEN_SOME_FOUND")]
    [InlineData("GEN_PARTIAL_SUCCESS")]
    [InlineData("GEN_RATE_LIMITED")]
    [InlineData("GEN_IDEMPOTENCY_IN_FLIGHT")]
    [InlineData("GEN_PAYLOAD_TOO_LARGE")]
    [InlineData("GEN_CANCELED")]
    [InlineData("GEN_FORGERY_REFUSED")]
    public void EveryCatalogValue_RoundTripsThroughEnvelopeUnchanged(string catalogValue)
    {
        // Catalog completeness: every value enumerated in
        // contracts/error-codes/error-codes.spec.json round-trips through
        // D2Result.Fail -> JsonSerializer.Serialize -> JSON parse -> wire
        // value, byte-for-byte. A drifted value would break client switch
        // statements, audit-log queries and alerting rules that filter on
        // the wire errorCode value.
        var result = D2Result.Fail(errorCode: catalogValue);

        ReadErrorCode(result).Should().Be(catalogValue);
    }

    private static string ReadErrorCode(D2Result result)
    {
        var json = JsonSerializer.Serialize<object>(result);
        var obj = JsonNode.Parse(json)!.AsObject();
        return obj[D2ResultEnvelopeFieldNames.ERROR_CODE]!.GetValue<string>();
    }

    private static string ReadErrorCode<TData>(D2Result<TData> result)
    {
        var json = JsonSerializer.Serialize<object>(result);
        var obj = JsonNode.Parse(json)!.AsObject();
        return obj[D2ResultEnvelopeFieldNames.ERROR_CODE]!.GetValue<string>();
    }
}
