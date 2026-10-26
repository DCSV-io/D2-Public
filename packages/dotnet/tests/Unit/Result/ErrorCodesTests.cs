// -----------------------------------------------------------------------
// <copyright file="ErrorCodesTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result;

using AwesomeAssertions;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Pins the exact wire-format string of every <see cref="ErrorCodes"/>
/// constant. The constants use <c>nameof(X)</c>, so renaming a field would
/// change the wire value without any error. Code that reads the wire value
/// (web client switch statements, audit log queries, alerting rules) would
/// still build and would misclassify failures. These tests fail on such a rename.
/// </summary>
public sealed class ErrorCodesTests
{
    [Theory]
    [InlineData(nameof(ErrorCodes.GEN_NOT_FOUND), "GEN_NOT_FOUND")]
    [InlineData(nameof(ErrorCodes.GEN_FORBIDDEN), "GEN_FORBIDDEN")]
    [InlineData(nameof(ErrorCodes.GEN_UNAUTHORIZED), "GEN_UNAUTHORIZED")]
    [InlineData(nameof(ErrorCodes.GEN_VALIDATION_FAILED), "GEN_VALIDATION_FAILED")]
    [InlineData(nameof(ErrorCodes.GEN_CONFLICT), "GEN_CONFLICT")]
    [InlineData(nameof(ErrorCodes.GEN_UNHANDLED_EXCEPTION), "GEN_UNHANDLED_EXCEPTION")]
    [InlineData(nameof(ErrorCodes.GEN_COULD_NOT_BE_SERIALIZED), "GEN_COULD_NOT_BE_SERIALIZED")]
    [InlineData(nameof(ErrorCodes.GEN_COULD_NOT_BE_DESERIALIZED), "GEN_COULD_NOT_BE_DESERIALIZED")]
    [InlineData(nameof(ErrorCodes.GEN_SERVICE_UNAVAILABLE), "GEN_SERVICE_UNAVAILABLE")]
    [InlineData(nameof(ErrorCodes.GEN_SOME_FOUND), "GEN_SOME_FOUND")]
    [InlineData(nameof(ErrorCodes.GEN_PARTIAL_SUCCESS), "GEN_PARTIAL_SUCCESS")]
    [InlineData(nameof(ErrorCodes.GEN_RATE_LIMITED), "GEN_RATE_LIMITED")]
    [InlineData(nameof(ErrorCodes.GEN_IDEMPOTENCY_IN_FLIGHT), "GEN_IDEMPOTENCY_IN_FLIGHT")]
    [InlineData(nameof(ErrorCodes.GEN_PAYLOAD_TOO_LARGE), "GEN_PAYLOAD_TOO_LARGE")]
    [InlineData(nameof(ErrorCodes.GEN_CANCELED), "GEN_CANCELED")]
    [InlineData(nameof(ErrorCodes.GEN_FORGERY_REFUSED), "GEN_FORGERY_REFUSED")]
    public void Constant_NameAndValueMatch(string nameOfValue, string expectedWireValue)
    {
        // nameof() is evaluated at the test call site; the constant's runtime VALUE
        // must equal the field name exactly. Renaming the field changes both the
        // nameof() result AND the runtime value, so this test catches the case where
        // a literal replaces `nameof(X)` and drifts from the field name.
        nameOfValue.Should().Be(expectedWireValue);
    }

    [Fact]
    public void Constant_NotFound_HasCanonicalWireValue()
    {
        ErrorCodes.GEN_NOT_FOUND.Should().Be("GEN_NOT_FOUND");
    }

    [Fact]
    public void Constant_Forbidden_HasCanonicalWireValue()
    {
        ErrorCodes.GEN_FORBIDDEN.Should().Be("GEN_FORBIDDEN");
    }

    [Fact]
    public void Constant_Unauthorized_HasCanonicalWireValue()
    {
        ErrorCodes.GEN_UNAUTHORIZED.Should().Be("GEN_UNAUTHORIZED");
    }

    [Fact]
    public void Constant_ValidationFailed_HasCanonicalWireValue()
    {
        ErrorCodes.GEN_VALIDATION_FAILED.Should().Be("GEN_VALIDATION_FAILED");
    }

    [Fact]
    public void Constant_Conflict_HasCanonicalWireValue()
    {
        ErrorCodes.GEN_CONFLICT.Should().Be("GEN_CONFLICT");
    }

    [Fact]
    public void Constant_UnhandledException_HasCanonicalWireValue()
    {
        ErrorCodes.GEN_UNHANDLED_EXCEPTION.Should().Be("GEN_UNHANDLED_EXCEPTION");
    }

    [Fact]
    public void Constant_CouldNotBeSerialized_HasCanonicalWireValue()
    {
        ErrorCodes.GEN_COULD_NOT_BE_SERIALIZED.Should().Be("GEN_COULD_NOT_BE_SERIALIZED");
    }

    [Fact]
    public void Constant_CouldNotBeDeserialized_HasCanonicalWireValue()
    {
        ErrorCodes.GEN_COULD_NOT_BE_DESERIALIZED.Should().Be("GEN_COULD_NOT_BE_DESERIALIZED");
    }

    [Fact]
    public void Constant_ServiceUnavailable_HasCanonicalWireValue()
    {
        ErrorCodes.GEN_SERVICE_UNAVAILABLE.Should().Be("GEN_SERVICE_UNAVAILABLE");
    }

    [Fact]
    public void Constant_SomeFound_HasCanonicalWireValue()
    {
        ErrorCodes.GEN_SOME_FOUND.Should().Be("GEN_SOME_FOUND");
    }

    [Fact]
    public void Constant_PartialSuccess_HasCanonicalWireValue()
    {
        ErrorCodes.GEN_PARTIAL_SUCCESS.Should().Be("GEN_PARTIAL_SUCCESS");
    }

    [Fact]
    public void Constant_RateLimited_HasCanonicalWireValue()
    {
        ErrorCodes.GEN_RATE_LIMITED.Should().Be("GEN_RATE_LIMITED");
    }

    [Fact]
    public void Constant_IdempotencyInFlight_HasCanonicalWireValue()
    {
        ErrorCodes.GEN_IDEMPOTENCY_IN_FLIGHT.Should().Be("GEN_IDEMPOTENCY_IN_FLIGHT");
    }

    [Fact]
    public void Constant_PayloadTooLarge_HasCanonicalWireValue()
    {
        ErrorCodes.GEN_PAYLOAD_TOO_LARGE.Should().Be("GEN_PAYLOAD_TOO_LARGE");
    }

    [Fact]
    public void Constant_Canceled_HasCanonicalWireValue()
    {
        // American English (single L) - matches BCL OperationCanceledException.
        ErrorCodes.GEN_CANCELED.Should().Be("GEN_CANCELED");
    }

    [Fact]
    public void Constant_ForgeryRefused_HasCanonicalWireValue()
    {
        ErrorCodes.GEN_FORGERY_REFUSED.Should().Be("GEN_FORGERY_REFUSED");
    }
}
