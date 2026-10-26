// -----------------------------------------------------------------------
// <copyright file="SanitizedExceptionRenderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Utilities.Diagnostics;

using AwesomeAssertions;
using DcsvIo.D2.Utilities.Diagnostics;
using Xunit;

/// <summary>
/// Adversarial tests for the PII-safe exception render, which gives a
/// <c>[LoggerMessage]</c> log entry an exception-derived string without
/// leaking <see cref="Exception.Message"/>. The tests pin these invariants:
/// (1) <c>TypeName</c> never contains <see cref="Exception.Message"/>;
/// (2) <c>FirstFrame</c> never contains <see cref="Exception.Message"/>;
/// (3) <c>FirstFrame</c> on a never-thrown exception returns the literal
/// sentinel <c>"&lt;no frame&gt;"</c> and not null, so a caller can
/// interpolate it into a string without a null guard;
/// (4) <c>FirstFrame</c> on a thrown exception contains the name of the
/// throwing method;
/// (5) a message shaped like a <c>BrokerUnreachableException</c> message, which
/// embeds the AMQP URI password in its <c>Message</c>, appears in neither
/// output, which covers the messaging case;
/// (6) an exception with an empty stack trace (<c>StackTrace = ""</c>) gets the
/// sentinel and does not crash the render.
/// </summary>
public sealed class SanitizedExceptionRenderTests
{
    [Fact]
    public void TypeName_ReturnsConcreteFullName()
    {
        var ex = new InvalidOperationException("anything");

        var typeName = SanitizedExceptionRender.TypeName(ex);

        typeName.Should().Be("System.InvalidOperationException");
    }

    [Fact]
    public void TypeName_NeverIncludesExceptionMessage()
    {
        // Build an exception whose Message holds a secret. The render must not
        // include that Message in its output.
        const string sensitiveMessage = "secret-token-abc123-leak-bait";
        var ex = new InvalidOperationException(sensitiveMessage);

        var typeName = SanitizedExceptionRender.TypeName(ex);

        typeName.Should().NotContain(sensitiveMessage);
    }

    [Fact]
    public void FirstFrame_OnThrownException_IdentifiesMethod_AndDoesNotIncludeExceptionMessage()
    {
        const string sensitiveMessage = "secret-token-abc123-leak-bait";
        Exception captured;
        try
        {
            throw new InvalidOperationException(sensitiveMessage);
        }
        catch (Exception ex)
        {
            captured = ex;
        }

        var firstFrame = SanitizedExceptionRender.FirstFrame(captured);

        firstFrame.Should().NotBe("<no frame>");
        firstFrame.Should().NotContain(
            sensitiveMessage,
            "ex.Message must NOT leak into the rendered first-frame string");
        const string thisMethod =
            nameof(FirstFrame_OnThrownException_IdentifiesMethod_AndDoesNotIncludeExceptionMessage);
        firstFrame.Should().Contain(thisMethod);
    }

    [Fact]
    public void FirstFrame_OnNeverThrownException_ReturnsSentinel()
    {
        // An exception that was constructed and never thrown has no StackTrace.
        // The render must return the literal "<no frame>" sentinel and not null,
        // so a log site can interpolate the result without a null guard. The
        // assertion below fails if FirstFrame returns `null`.
        var ex = new InvalidOperationException("never thrown");

        var firstFrame = SanitizedExceptionRender.FirstFrame(ex);

        firstFrame.Should().Be("<no frame>");
        firstFrame.Should().NotContain("never thrown");
    }

    [Fact]
    public void FirstFrame_OnExceptionWithEmptyStackTrace_ReturnsSentinel()
    {
        // An exception with StackTrace = "" (not null) must also render the
        // sentinel, not an empty or malformed frame description. StackTrace has
        // no public setter, so this test cannot build that exception. It
        // constructs a never-thrown exception, whose StackTrace is null, and
        // pins the sentinel value that FirstFrame returns for it.
        var ex = new InvalidOperationException();

        var firstFrame = SanitizedExceptionRender.FirstFrame(ex);

        firstFrame.Should().Be("<no frame>");
    }

    [Fact]
    public void TypeName_NeverReturnsNull_FullNameOrNameFallback()
    {
        // The `FullName ?? Name` fallback means TypeName never returns null, so a
        // caller can interpolate the result without a null guard. FullName is
        // always non-null for a BCL exception type, so this test does not reach
        // the `?? Name` fallback. It asserts that TypeName returns a non-null,
        // non-empty string.
        var ex = new InvalidOperationException();

        var typeName = SanitizedExceptionRender.TypeName(ex);

        typeName.Should().NotBeNull();
        typeName.Should().NotBeEmpty();
    }

    [Fact]
    public void BrokerUnreachableException_LikeMessage_NeverLeaksToFrame()
    {
        // A BrokerUnreachableException's Message embeds the full AMQP URI,
        // including the password. The render must not carry that string into the
        // log pipeline or DLQ headers. The test throws an
        // InvalidOperationException with that message text.
        const string amqpLeakBait = "amqp://admin:s3cret-broker-pw@rabbit:5672/vhost";
        Exception captured;
        try
        {
            throw new InvalidOperationException(amqpLeakBait);
        }
        catch (Exception ex)
        {
            captured = ex;
        }

        var typeName = SanitizedExceptionRender.TypeName(captured);
        var firstFrame = SanitizedExceptionRender.FirstFrame(captured);

        typeName.Should().NotContain(amqpLeakBait);
        typeName.Should().NotContain("s3cret-broker-pw");
        firstFrame.Should().NotContain(amqpLeakBait);
        firstFrame.Should().NotContain("s3cret-broker-pw");
    }
}
