// -----------------------------------------------------------------------
// <copyright file="D2ResultGuardTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result;

using System.Net;
using AwesomeAssertions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using Xunit;

public sealed class D2ResultGuardTests
{
    // ----------------------------------------------------------------------
    // BubbleOnFailure - happy path (success returns false, data populated)
    // ----------------------------------------------------------------------

    [Fact]
    public void BubbleOnFailure_OnOk_ReturnsFalseAndPopulatesData()
    {
        var inner = D2Result<int>.Ok(42);

        var failed = inner.BubbleOnFailure<int, string>(out _, out var data);

        failed.Should().BeFalse();
        data.Should().Be(42);
    }

    [Fact]
    public void BubbleOnFailure_OnOkWithNullData_ReturnsFalseDataIsDefault()
    {
        // Adversarial: Ok with no data -> the success path still returns false (the
        // caller should NOT bail), and data is whatever Ok wrapped (the default for a
        // reference type).
        var inner = D2Result<string>.Ok();

        var failed = inner.BubbleOnFailure<string, int>(out _, out var data);

        failed.Should().BeFalse();
        data.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // BubbleOnFailure - failure path (returns true, bubbled populated correctly)
    // ----------------------------------------------------------------------

    [Fact]
    public void BubbleOnFailure_OnFailure_ReturnsTrueAndBubblesMetadata()
    {
        var inner = D2Result<int>.NotFound(messages: [TK.Common.Errors.UNKNOWN], traceId: "t");

        var failed = inner.BubbleOnFailure<int, string>(out var bubbled, out _);

        failed.Should().BeTrue();
        bubbled.Should().NotBeNull();
        bubbled.Success.Should().BeFalse();
        bubbled.ErrorCode.Should().Be(ErrorCodes.GEN_NOT_FOUND);
        bubbled.StatusCode.Should().Be(HttpStatusCode.NotFound);
        bubbled.Messages.Should().Equal(TK.Common.Errors.UNKNOWN);
        bubbled.TraceId.Should().Be("t");
    }

    [Fact]
    public void BubbleOnFailure_OnFailure_DataIsDefault()
    {
        var inner = D2Result<int>.Conflict();

        inner.BubbleOnFailure<int, string>(out _, out var data);

        data.Should().Be(0); // default(int)
    }

    [Fact]
    public void BubbleOnFailure_OnFailure_TInnerAndTOuterAreIndependent()
    {
        // Adversarial: TInner (upstream payload) and TOuter (caller's outer return
        // payload) need not be the same. The bubbled result is shaped to TOuter while
        // metadata is preserved.
        var inner = D2Result<int>.Forbidden(messages: [TK.Common.Errors.UNKNOWN]);

        var failed = inner.BubbleOnFailure<int, string>(out var bubbled, out _);

        failed.Should().BeTrue();
        bubbled.Should().BeOfType<D2Result<string?>>();
        bubbled.ErrorCode.Should().Be(ErrorCodes.GEN_FORBIDDEN);
        bubbled.Messages.Should().Equal(TK.Common.Errors.UNKNOWN);
    }

    [Fact]
    public void BubbleOnFailure_OnFailure_PreservesInputErrors()
    {
        var inner = D2Result<int>.ValidationFailed(
            inputErrors: [new InputError("email", [TK.Common.Validation.EMAIL_INVALID])]);

        inner.BubbleOnFailure<int, string>(out var bubbled, out _);

        bubbled.InputErrors.Should().HaveCount(1);
        bubbled.InputErrors[0].Field.Should().Be("email");
        bubbled.InputErrors[0].Errors.Should().Equal(TK.Common.Validation.EMAIL_INVALID);
    }

    // ----------------------------------------------------------------------
    // SomeFound - the guard treats a partial success as a failure
    // ----------------------------------------------------------------------

    [Fact]
    public void BubbleOnFailure_OnSomeFound_ReturnsTrueAndBubblesPartialSuccess()
    {
        // Adversarial: SomeFound is a partial-success result with Success=false,
        // so the guard treats it as a failure and bubbles. Code that handles
        // SomeFound differently must inspect the result BEFORE calling the guard.
        var inner = D2Result<int>.SomeFound(99);

        var failed = inner.BubbleOnFailure<int, string>(out var bubbled, out _);

        failed.Should().BeTrue();
        bubbled.ErrorCode.Should().Be(ErrorCodes.GEN_SOME_FOUND);
        bubbled.StatusCode.Should().Be(HttpStatusCode.PartialContent);
    }

    // ----------------------------------------------------------------------
    // Call-site pattern - guard, then continue with locals
    // ----------------------------------------------------------------------

    [Fact]
    public void BubbleOnFailure_HandlerPatternUsage_OnSuccess_ContinuesWithLocal()
    {
        // Follows the handler pattern:
        //   if (upstream.BubbleOnFailure<_, OutputType>(out var b, out var data)) return b;
        //   // continue with `data` as a local
        var upstream = D2Result<int>.Ok(15);

        var output = ExampleHandler(upstream);

        output.Success.Should().BeTrue();
        output.Data.Should().Be("doubled=30");
    }

    [Fact]
    public void BubbleOnFailure_HandlerPatternUsage_OnFailure_BubblesToOuter()
    {
        var upstream = D2Result<int>.NotFound(messages: [TK.Common.Errors.UNKNOWN]);

        var output = ExampleHandler(upstream);

        output.Success.Should().BeFalse();
        output.Data.Should().BeNull();
        output.ErrorCode.Should().Be(ErrorCodes.GEN_NOT_FOUND);
        output.Messages.Should().Equal(TK.Common.Errors.UNKNOWN);
    }

    // Handler-shaped helper that calls BubbleOnFailure the way the handler pattern
    // does, so the tests use a realistic call site and not a synthetic one.
    private static D2Result<string?> ExampleHandler(D2Result<int> upstream)
    {
        if (upstream.BubbleOnFailure<int, string>(out var bubbled, out var data))
            return bubbled;

        return D2Result<string?>.Ok($"doubled={data * 2}");
    }
}
