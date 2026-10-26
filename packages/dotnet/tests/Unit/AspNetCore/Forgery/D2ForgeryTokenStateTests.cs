// -----------------------------------------------------------------------
// <copyright file="D2ForgeryTokenStateTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AspNetCore.Forgery;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore.Forgery;
using DcsvIo.D2.Utilities.Attributes;
using Xunit;

/// <summary>
/// The request-scoped state the middleware writes and the host's token issuer
/// reads, and the issue record the issuer returns.
/// </summary>
public sealed class D2ForgeryTokenStateTests
{
    [Fact]
    public void FreshState_HasNotBeenChecked() =>
        new D2ForgeryTokenState().Checked.Should().BeFalse();

    [Fact]
    public void FreshState_CarriesTheNoSessionVerdict() =>
        new D2ForgeryTokenState().Verdict.Should().Be(D2ForgeryTokenVerdict.NoSession);

    [Fact]
    public void Issue_CarriesItsValueAndExpiry()
    {
        var expires = new DateTimeOffset(2027, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var issue = new D2ForgeryTokenIssue("sealed-value", expires);

        issue.Value.Should().Be("sealed-value");
        issue.ExpiresAt.Should().Be(expires);
    }

    [Fact]
    public void Issue_WithNoExpiry_CarriesNone() =>
        new D2ForgeryTokenIssue("sealed-value", null).ExpiresAt.Should().BeNull();

    [Fact]
    public void TwoIssuesWithTheSameValueAndExpiry_AreEqual() =>
        new D2ForgeryTokenIssue("v", null).Should().Be(new D2ForgeryTokenIssue("v", null));

    [Fact]
    public void Issue_RedactionPlaceholder_HasItsCanonicalValue() =>
        D2ForgeryTokenIssue.REDACTION_PLACEHOLDER.Should().Be("[redacted-forgery-token]");

    [Fact]
    public void Issue_ToString_RendersThePlaceholderRatherThanTheToken()
    {
        var expires = new DateTimeOffset(2027, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var issue = new D2ForgeryTokenIssue("sealed-value", expires);

        issue.ToString().Should().NotContain("sealed-value");
        issue.ToString().Should().Contain(D2ForgeryTokenIssue.REDACTION_PLACEHOLDER);

        // The interpolation path is the one a log call takes by accident.
        $"{issue}".Should().NotContain("sealed-value");
    }

    [Fact]
    public void Issue_ToString_KeepsTheExpiryReadable()
    {
        var expires = new DateTimeOffset(2027, 1, 2, 3, 4, 5, TimeSpan.Zero);

        new D2ForgeryTokenIssue("sealed-value", expires).ToString()
            .Should().Contain("2027");
    }

    [Fact]
    public void Issue_Value_CarriesTheRedactionMarker() =>
        typeof(D2ForgeryTokenIssue)
            .GetProperty(nameof(D2ForgeryTokenIssue.Value))!
            .GetCustomAttributes(typeof(RedactDataAttribute), inherit: true)
            .Should().ContainSingle();

    [Fact]
    public void Issue_ExpiresAt_CarriesNoRedactionMarker() =>
        typeof(D2ForgeryTokenIssue)
            .GetProperty(nameof(D2ForgeryTokenIssue.ExpiresAt))!
            .GetCustomAttributes(typeof(RedactDataAttribute), inherit: true)
            .Should().BeEmpty();
}
