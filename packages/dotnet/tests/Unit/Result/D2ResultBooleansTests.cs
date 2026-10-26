// -----------------------------------------------------------------------
// <copyright file="D2ResultBooleansTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result;

using AwesomeAssertions;
using DcsvIo.D2.Result;
using Xunit;

public sealed class D2ResultBooleansTests
{
    // ----------------------------------------------------------------------
    // Per-code booleans - true on the matching factory, false on others
    // ----------------------------------------------------------------------

    [Fact]
    public void IsOk_TrueOnOk_FalseOnFailures()
    {
        D2Result.Ok().IsOk.Should().BeTrue();
        D2Result.NotFound().IsOk.Should().BeFalse();
        D2Result.Conflict().IsOk.Should().BeFalse();
        D2Result.ValidationFailed().IsOk.Should().BeFalse();
    }

    [Fact]
    public void IsCreated_TrueOnCreated_FalseOnOk()
    {
        // Adversarial: IsCreated is StatusCode-based, so plain Ok() should NOT match.
        D2Result.Created().IsCreated.Should().BeTrue();
        D2Result.Ok().IsCreated.Should().BeFalse();
        D2Result.NotFound().IsCreated.Should().BeFalse();
    }

    [Fact]
    public void IsNotFound_TrueOnNotFound_FalseOnOthers()
    {
        D2Result.NotFound().IsNotFound.Should().BeTrue();
        D2Result.Conflict().IsNotFound.Should().BeFalse();
        D2Result.SomeFound().IsNotFound.Should().BeFalse();
        D2Result.Ok().IsNotFound.Should().BeFalse();
    }

    [Fact]
    public void IsSomeFound_TrueOnSomeFound_FalseOnOthers()
    {
        D2Result.SomeFound().IsSomeFound.Should().BeTrue();
        D2Result.NotFound().IsSomeFound.Should().BeFalse();
        D2Result.Ok().IsSomeFound.Should().BeFalse();
    }

    [Fact]
    public void IsConflict_TrueOnConflict_FalseOnOthers()
    {
        D2Result.Conflict().IsConflict.Should().BeTrue();
        D2Result.NotFound().IsConflict.Should().BeFalse();
        D2Result.Ok().IsConflict.Should().BeFalse();
    }

    [Fact]
    public void IsForbidden_TrueOnForbidden_FalseOnOthers()
    {
        D2Result.Forbidden().IsForbidden.Should().BeTrue();
        D2Result.Unauthorized().IsForbidden.Should().BeFalse();
        D2Result.Ok().IsForbidden.Should().BeFalse();
    }

    [Fact]
    public void IsUnauthorized_TrueOnUnauthorized_FalseOnOthers()
    {
        D2Result.Unauthorized().IsUnauthorized.Should().BeTrue();
        D2Result.Forbidden().IsUnauthorized.Should().BeFalse();
        D2Result.Ok().IsUnauthorized.Should().BeFalse();
    }

    [Fact]
    public void IsValidationFailed_TrueOnValidationFailed_FalseOnOthers()
    {
        D2Result.ValidationFailed().IsValidationFailed.Should().BeTrue();
        D2Result.Conflict().IsValidationFailed.Should().BeFalse();
        D2Result.Ok().IsValidationFailed.Should().BeFalse();
    }

    [Fact]
    public void IsValidationFailed_FalseWhenErrorCodeOverridden()
    {
        // Adversarial: ValidationFailed accepts an errorCode override.
        // When overridden, IsValidationFailed should NOT be true (it checks errorCode equality).
        var result = D2Result.ValidationFailed(errorCode: "FILES_INVALID_CONTENT_TYPE");

        result.IsValidationFailed.Should().BeFalse();
        result.ErrorCode.Should().Be("FILES_INVALID_CONTENT_TYPE");
    }

    [Fact]
    public void IsServiceUnavailable_TrueOnServiceUnavailable_FalseOnOthers()
    {
        D2Result.ServiceUnavailable().IsServiceUnavailable.Should().BeTrue();
        D2Result.UnhandledException().IsServiceUnavailable.Should().BeFalse();
        D2Result.TooManyRequests().IsServiceUnavailable.Should().BeFalse();
    }

    [Fact]
    public void IsRateLimited_TrueOnTooManyRequests_FalseOnOthers()
    {
        D2Result.TooManyRequests().IsRateLimited.Should().BeTrue();
        D2Result.ServiceUnavailable().IsRateLimited.Should().BeFalse();
        D2Result.Ok().IsRateLimited.Should().BeFalse();
    }

    [Fact]
    public void IsUnhandledException_TrueOnUnhandledException_FalseOnOthers()
    {
        D2Result.UnhandledException().IsUnhandledException.Should().BeTrue();
        D2Result.ServiceUnavailable().IsUnhandledException.Should().BeFalse();
        D2Result.Conflict().IsUnhandledException.Should().BeFalse();
    }

    [Fact]
    public void IsPayloadTooLarge_TrueOnPayloadTooLarge_FalseOnOthers()
    {
        D2Result.PayloadTooLarge().IsPayloadTooLarge.Should().BeTrue();
        D2Result.ValidationFailed().IsPayloadTooLarge.Should().BeFalse();
    }

    [Fact]
    public void IsCanceled_TrueOnCanceled_FalseOnOthers()
    {
        D2Result.Canceled().IsCanceled.Should().BeTrue();
        D2Result.UnhandledException().IsCanceled.Should().BeFalse();
    }

    [Fact]
    public void IsIdempotencyInFlight_NoFactoryButCheckableViaCustomErrorCode()
    {
        // No semantic factory exists for IDEMPOTENCY_IN_FLIGHT. The result is built with
        // Fail(errorCode: ErrorCodes.GEN_IDEMPOTENCY_IN_FLIGHT). This test checks that
        // the boolean works for a result built that way.
        var result = D2Result.Fail(errorCode: ErrorCodes.GEN_IDEMPOTENCY_IN_FLIGHT);

        result.IsIdempotencyInFlight.Should().BeTrue();
        D2Result.Conflict().IsIdempotencyInFlight.Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // Combined helpers
    // ----------------------------------------------------------------------

    [Fact]
    public void IsPartialOrMissing_TrueOnNotFoundAndSomeFound_FalseOnOthers()
    {
        D2Result.NotFound().IsPartialOrMissing.Should().BeTrue();
        D2Result.SomeFound().IsPartialOrMissing.Should().BeTrue();

        D2Result.Ok().IsPartialOrMissing.Should().BeFalse();
        D2Result.Conflict().IsPartialOrMissing.Should().BeFalse();
        D2Result.Forbidden().IsPartialOrMissing.Should().BeFalse();
        D2Result.ValidationFailed().IsPartialOrMissing.Should().BeFalse();
    }

    [Fact]
    public void IsTransientRetryable_TrueOnServiceUnavailableAndRateLimited()
    {
        D2Result.ServiceUnavailable().IsTransientRetryable.Should().BeTrue();
        D2Result.TooManyRequests().IsTransientRetryable.Should().BeTrue();
    }

    [Fact]
    public void IsTransientRetryable_ExplicitlyFalseOnUnhandledException()
    {
        // Adversarial: UnhandledException is INTENTIONALLY excluded from
        // IsTransientRetryable. An unknown exception means an unknown system state, and
        // a retry could hide bugs or run the operation twice. Retry helpers read this
        // property, so including UnhandledException would make them retry it.
        D2Result.UnhandledException().IsTransientRetryable.Should().BeFalse();
    }

    [Fact]
    public void IsTransientRetryable_FalseOnNonTransientFailures()
    {
        D2Result.NotFound().IsTransientRetryable.Should().BeFalse();
        D2Result.Conflict().IsTransientRetryable.Should().BeFalse();
        D2Result.Forbidden().IsTransientRetryable.Should().BeFalse();
        D2Result.Unauthorized().IsTransientRetryable.Should().BeFalse();
        D2Result.ValidationFailed().IsTransientRetryable.Should().BeFalse();
        D2Result.PayloadTooLarge().IsTransientRetryable.Should().BeFalse();
        D2Result.Canceled().IsTransientRetryable.Should().BeFalse();
        D2Result.Ok().IsTransientRetryable.Should().BeFalse();
    }

    [Fact]
    public void IsTransientRetryable_FalseWhenServiceUnavailableErrorCodeOverridden()
    {
        // Adversarial: a domain override of ServiceUnavailable's errorCode makes the
        // IsServiceUnavailable check false, and IsTransientRetryable depends on that
        // check. This test documents the behavior: a result with an errorCode
        // override on a transient category is not classified as retryable by the
        // retry helpers.
        var result = D2Result.ServiceUnavailable(errorCode: "DOMAIN_RETRY_LATER");

        result.IsServiceUnavailable.Should().BeFalse();
        result.IsTransientRetryable.Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // IsPartialSuccess - the write-side partial, opted into explicitly
    // ----------------------------------------------------------------------

    [Fact]
    public void IsPartialSuccess_TrueOnPartialSuccess_FalseOnOk()
    {
        // PartialSuccess differs from SomeFound: it is the write-side partial
        // result, not the read-side one, and it has its own ErrorCode, so code
        // that accepts a partial write can check for it.
        D2Result<int>.PartialSuccess(42).IsPartialSuccess.Should().BeTrue();
        D2Result<int>.Ok(42).IsPartialSuccess.Should().BeFalse();
        D2Result<int>.Ok(42).Success.Should().BeTrue();
    }

    [Fact]
    public void IsPartialSuccess_FalseOnSomeFound_FalseOnFailures()
    {
        // SomeFound is the read-side partial; PartialSuccess is the write-side
        // partial. They must NOT alias - different ErrorCodes, different status.
        D2Result.SomeFound().IsPartialSuccess.Should().BeFalse();
        D2Result.NotFound().IsPartialSuccess.Should().BeFalse();
        D2Result.Conflict().IsPartialSuccess.Should().BeFalse();
        D2Result.ValidationFailed().IsPartialSuccess.Should().BeFalse();
    }

    [Fact]
    public void IsPartialSuccess_ReportsFailureAlongsideItsCode()
    {
        // A partial write reports failure and carries its own error code. Code
        // that checks only Failed treats the write as not done; code that
        // accepts a partial write reads the code.
        var result = D2Result<int>.PartialSuccess(42);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.GEN_PARTIAL_SUCCESS);
        result.IsPartialSuccess.Should().BeTrue();
    }

    [Fact]
    public void IsTransientRetryable_FalseOnPartialSuccess()
    {
        // A partial write shares the 503 status with ServiceUnavailable, and
        // the retry predicate is keyed on the error code rather than the
        // status, so the two do not alias into automatic retry eligibility.
        D2Result<int>.PartialSuccess(42).IsTransientRetryable.Should().BeFalse();
    }
}
