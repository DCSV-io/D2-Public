// -----------------------------------------------------------------------
// <copyright file="D2ResultWithTraceIdTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result;

using System.Net;
using AwesomeAssertions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="D2Result.WithTraceId"/> +
/// <see cref="D2Result{TData}.WithTraceId"/>. The transform attaches a trace
/// identifier to an existing result and must leave every other field unchanged.
/// </summary>
public sealed class D2ResultWithTraceIdTests
{
    private const string _ORIGINAL_TRACE = "trace-original";
    private const string _NEW_TRACE = "trace-new";

    // ----------------------------------------------------------------------
    // Immutability - the original instance MUST be untouched
    // ----------------------------------------------------------------------

    [Fact]
    public void WithTraceId_DoesNotMutateOriginal()
    {
        // Adversarial: the implementation clones with `new D2Result(...)`.
        // If a refactor replaced the clone with a setter or other mutation,
        // the original instance would change, and it must NOT. D2Result is
        // documented as immutable, and shared references to a result stay
        // valid only while that holds.
        var original = D2Result.Ok();

        var clone = original.WithTraceId(_NEW_TRACE);

        original.TraceId.Should().BeNull();
        clone.TraceId.Should().Be(_NEW_TRACE);
        clone.Should().NotBeSameAs(original);
    }

    [Fact]
    public void WithTraceId_Generic_DoesNotMutateOriginal()
    {
        var original = D2Result<int>.Ok(42);

        var clone = original.WithTraceId(_NEW_TRACE);

        original.TraceId.Should().BeNull();
        clone.TraceId.Should().Be(_NEW_TRACE);
        clone.Should().NotBeSameAs(original);
    }

    // ----------------------------------------------------------------------
    // Field preservation - every other property must keep its value
    // ----------------------------------------------------------------------

    [Fact]
    public void WithTraceId_PreservesSuccessFlag()
    {
        D2Result.Ok().WithTraceId(_NEW_TRACE).Success.Should().BeTrue();
        D2Result.NotFound().WithTraceId(_NEW_TRACE).Success.Should().BeFalse();
    }

    [Fact]
    public void WithTraceId_PreservesStatusCode()
    {
        // Cover several factory-driven status codes.
        D2Result.Ok().WithTraceId(_NEW_TRACE).StatusCode.Should().Be(HttpStatusCode.OK);
        D2Result.Created().WithTraceId(_NEW_TRACE).StatusCode.Should().Be(HttpStatusCode.Created);
        D2Result.NotFound().WithTraceId(_NEW_TRACE).StatusCode.Should().Be(HttpStatusCode.NotFound);
        D2Result.Conflict().WithTraceId(_NEW_TRACE).StatusCode.Should().Be(HttpStatusCode.Conflict);
        D2Result.Forbidden().WithTraceId(_NEW_TRACE).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public void WithTraceId_PreservesErrorCode()
    {
        var result = D2Result.NotFound();
        var original_error_code = result.ErrorCode;

        var transformed = result.WithTraceId(_NEW_TRACE);

        transformed.ErrorCode.Should().Be(original_error_code);
        transformed.ErrorCode.Should().Be(ErrorCodes.GEN_NOT_FOUND);
    }

    [Fact]
    public void WithTraceId_PreservesMessages()
    {
        // The messages list keeps its values through the transform.
        var result = D2Result.NotFound();
        var original_message_count = result.Messages.Count;

        var transformed = result.WithTraceId(_NEW_TRACE);

        transformed.Messages.Should().HaveCount(original_message_count);
        transformed.Messages.Should().BeEquivalentTo(result.Messages);
    }

    [Fact]
    public void WithTraceId_PreservesInputErrors()
    {
        // Adversarial: ValidationFailed with InputErrors is the most
        // common failure shape that flows through Combine/aggregate paths.
        // Losing InputErrors when a TraceId is attached would drop
        // per-field error context without any error.
        var input_errors = new[]
        {
            new InputError("email", [TK.Common.Errors.UNKNOWN]),
            new InputError("phone", [TK.Common.Errors.VALIDATION_FAILED]),
        };
        var result = D2Result.ValidationFailed(inputErrors: input_errors);

        var transformed = result.WithTraceId(_NEW_TRACE);

        transformed.InputErrors.Should().HaveCount(2);
        transformed.InputErrors.Should().BeEquivalentTo(result.InputErrors);
    }

    [Fact]
    public void WithTraceId_Generic_PreservesData()
    {
        var data = new Sample();
        var result = D2Result<Sample>.Ok(data);

        var transformed = result.WithTraceId(_NEW_TRACE);

        transformed.Data.Should().BeSameAs(data);
    }

    [Fact]
    public void WithTraceId_Generic_PreservesDefaultDataOnFailure()
    {
        // Failure factories on D2Result<T> set Data = default. WithTraceId
        // must keep that value.
        var result = D2Result<Sample>.NotFound();

        var transformed = result.WithTraceId(_NEW_TRACE);

        transformed.Data.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // TraceId argument - null, empty, whitespace and replacement cases
    // ----------------------------------------------------------------------

    [Fact]
    public void WithTraceId_NullArg_ResultTraceIdBecomesNull()
    {
        // Passing null removes the TraceId, for example at a redaction or
        // privacy boundary such as before logging across tenants.
        var result = D2Result.Ok().WithTraceId(_ORIGINAL_TRACE);

        var stripped = result.WithTraceId(null);

        stripped.TraceId.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void WithTraceId_EmptyOrWhitespaceArg_PreservedAsProvided(string trace)
    {
        // D2Result.TraceId stores the string as given and does NOT convert
        // empty or whitespace values to null. This test documents that
        // behavior; code that needs "" -> null must convert the value before
        // it calls WithTraceId.
        var result = D2Result.Ok().WithTraceId(trace);

        result.TraceId.Should().Be(trace);
    }

    [Fact]
    public void WithTraceId_OverwritesExistingTraceId()
    {
        var original = D2Result.Ok().WithTraceId(_ORIGINAL_TRACE);

        var overwritten = original.WithTraceId(_NEW_TRACE);

        overwritten.TraceId.Should().Be(_NEW_TRACE);
        original.TraceId.Should().Be(_ORIGINAL_TRACE); // immutability
    }

    [Fact]
    public void WithTraceId_Idempotent_SameTraceIdProducesEqualResult()
    {
        var original = D2Result.Ok().WithTraceId(_NEW_TRACE);

        var same_trace = original.WithTraceId(_NEW_TRACE);

        same_trace.TraceId.Should().Be(_NEW_TRACE);
        same_trace.Should().NotBeSameAs(original); // new instance, but content-equivalent
        same_trace.Success.Should().Be(original.Success);
        same_trace.StatusCode.Should().Be(original.StatusCode);
        same_trace.ErrorCode.Should().Be(original.ErrorCode);
    }

    // ----------------------------------------------------------------------
    // Type narrowing - `new` keyword on D2Result<T>.WithTraceId
    // ----------------------------------------------------------------------

    [Fact]
    public void WithTraceId_GenericInstance_ReturnsGenericNotBaseType()
    {
        // Adversarial: the `new` member-hiding pattern on D2Result<T>
        // means that a call through a D2Result<T> reference returns a
        // D2Result<T>, NOT a D2Result. If the hiding member were removed
        // (or not declared `new`), the static return type would become
        // the base type, and code that chains .Data access would not compile.
        var original = D2Result<int>.Ok(7);

        var transformed = original.WithTraceId(_NEW_TRACE);

        transformed.Should().BeOfType<D2Result<int>>();
        transformed.Data.Should().Be(7);
    }

    [Fact]
    public void WithTraceId_GenericCalledViaBaseReference_ReturnsBaseType()
    {
        // Adversarial: calling WithTraceId through a D2Result base reference to a
        // generic instance uses the BASE signature (no Data). This documents the
        // `new`-hiding semantics: the compiler resolves the member by the static
        // reference type, not the runtime type. Code that needs Data must hold the
        // generic reference.
        D2Result base_ref = D2Result<int>.Ok(7);

        var transformed = base_ref.WithTraceId(_NEW_TRACE);

        // The source instance is a D2Result<int>, but the returned object is a
        // D2Result, NOT a D2Result<int>: the base implementation creates a new
        // base instance via `new D2Result(...)`.
        transformed.Should().BeOfType<D2Result>();
    }

    // ----------------------------------------------------------------------
    // Attaching a TraceId afterward equals passing it to the factory
    // ----------------------------------------------------------------------

    [Fact]
    public void WithTraceId_AfterFactory_BehavesLikeFactoryWithTraceIdArg()
    {
        // Calling .WithTraceId on a result that already came from a factory
        // must give a result indistinguishable from calling the factory with
        // the traceId argument directly.
        var via_transform = D2Result<int>.Ok(42).WithTraceId(_NEW_TRACE);
        var via_factory = D2Result<int>.Ok(42, traceId: _NEW_TRACE);

        via_transform.Success.Should().Be(via_factory.Success);
        via_transform.Data.Should().Be(via_factory.Data);
        via_transform.TraceId.Should().Be(via_factory.TraceId);
        via_transform.StatusCode.Should().Be(via_factory.StatusCode);
        via_transform.ErrorCode.Should().Be(via_factory.ErrorCode);
        via_transform.Messages.Should().BeEquivalentTo(via_factory.Messages);
        via_transform.InputErrors.Should().BeEquivalentTo(via_factory.InputErrors);
    }

    // Used only as a generic type token for D2Result<Sample>; identity
    // matters for BeSameAs assertions, no field reads needed.
    private sealed class Sample
    {
    }
}
