// -----------------------------------------------------------------------
// <copyright file="InputFailuresTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Caching.Abstractions;

using AwesomeAssertions;
using DcsvIo.D2.Caching;
using DcsvIo.D2.I18n;
using Xunit;

/// <summary>
/// Pins the wire shape of <see cref="InputFailures.Required{T}(string)"/> and
/// <see cref="InputFailures.Required(string)"/>. Cache implementations depend on
/// the exact shape (<c>IsValidationFailed</c>, a single <c>InputError</c> with the
/// param name and <c>TK.Common.Errors.NOT_NULL_VIOLATION</c>). A refactor that
/// moves "required" failures elsewhere must keep this contract, or these tests
/// fail and every cache implementation breaks loudly.
/// </summary>
public sealed class InputFailuresTests
{
    [Fact]
    public void RequiredGeneric_ReturnsValidationFailedWithSingleInputError()
    {
        var result = InputFailures.Required<string>("key");

        result.Success.Should().BeFalse();
        result.IsValidationFailed.Should().BeTrue();
        result.InputErrors.Should().HaveCount(1);
        result.InputErrors[0].Field.Should().Be("key");
        result.InputErrors[0].Errors.Should().Equal(TK.Common.Errors.NOT_NULL_VIOLATION);
    }

    [Fact]
    public void RequiredGeneric_DataIsDefault()
    {
        var result = InputFailures.Required<string>("key");

        result.Data.Should().BeNull();
    }

    [Fact]
    public void RequiredGeneric_ValueTypeDataIsDefault()
    {
        var result = InputFailures.Required<int>("count");

        result.Data.Should().Be(0);
    }

    [Fact]
    public void RequiredNonGeneric_ReturnsValidationFailedWithSingleInputError()
    {
        var result = InputFailures.Required("paramName");

        result.Success.Should().BeFalse();
        result.IsValidationFailed.Should().BeTrue();
        result.InputErrors.Should().HaveCount(1);
        result.InputErrors[0].Field.Should().Be("paramName");
        result.InputErrors[0].Errors.Should().Equal(TK.Common.Errors.NOT_NULL_VIOLATION);
    }

    [Theory]
    [InlineData("key")]
    [InlineData("keys")]
    [InlineData("entries")]
    [InlineData("paramName")]
    [InlineData("anyArbitraryParameterName")]
    public void Required_PropagatesParamNameVerbatimIntoInputError(string paramName)
    {
        // Pin: paramName passes through unchanged. Cache implementations pass
        // nameof(...), which appears in client-side error displays, so a change
        // here would silently alter the user-visible error of every cache
        // implementation.
        var generic = InputFailures.Required<int>(paramName);
        var nonGeneric = InputFailures.Required(paramName);

        generic.InputErrors[0].Field.Should().Be(paramName);
        nonGeneric.InputErrors[0].Field.Should().Be(paramName);
    }

    [Fact]
    public void InvalidGeneric_ReturnsValidationFailedWithValidationFailedTk()
    {
        var result = InputFailures.Invalid<long>("amount");

        result.Success.Should().BeFalse();
        result.IsValidationFailed.Should().BeTrue();
        result.InputErrors.Should().HaveCount(1);
        result.InputErrors[0].Field.Should().Be("amount");
        result.InputErrors[0].Errors.Should().Equal(TK.Common.Errors.VALIDATION_FAILED);
    }

    [Fact]
    public void InvalidNonGeneric_ReturnsValidationFailedWithValidationFailedTk()
    {
        var result = InputFailures.Invalid("expiration");

        result.Success.Should().BeFalse();
        result.IsValidationFailed.Should().BeTrue();
        result.InputErrors.Should().HaveCount(1);
        result.InputErrors[0].Field.Should().Be("expiration");
        result.InputErrors[0].Errors.Should().Equal(TK.Common.Errors.VALIDATION_FAILED);
    }
}
