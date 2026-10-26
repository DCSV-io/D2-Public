// -----------------------------------------------------------------------
// <copyright file="ForgeryConstantsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AspNetCore.Forgery;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore.Forgery;
using Xunit;

/// <summary>
/// Pins the wire value of every closed-set constant the forgery check emits
/// or reads. A posture, a header name, a refusal reason and a metric tag are
/// each read by something outside this assembly - a generated marker, a
/// browser, an operator dashboard - so renaming a field must not change
/// the value without failing a test.
/// </summary>
public sealed class ForgeryConstantsTests
{
    [Theory]
    [InlineData(D2ForgeryPostures.REQUIRED, "required")]
    [InlineData(D2ForgeryPostures.METADATA, "metadata")]
    [InlineData(D2ForgeryPostures.EXEMPT, "exempt")]
    public void Posture_HasItsCanonicalWireValue(string actual, string expected) =>
        actual.Should().Be(expected);

    [Theory]
    [InlineData(D2ForgeryHeaderNames.SEC_FETCH_SITE, "Sec-Fetch-Site")]
    [InlineData(D2ForgeryHeaderNames.ORIGIN, "Origin")]
    [InlineData(D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_ORIGIN, "same-origin")]
    [InlineData(D2ForgeryHeaderNames.SEC_FETCH_SITE_NONE, "none")]
    [InlineData(D2ForgeryHeaderNames.SEC_FETCH_SITE_SAME_SITE, "same-site")]
    [InlineData(D2ForgeryHeaderNames.SEC_FETCH_SITE_CROSS_SITE, "cross-site")]
    public void HeaderName_HasItsCanonicalWireValue(string actual, string expected) =>
        actual.Should().Be(expected);

    [Theory]
    [InlineData(D2ForgeryRefusalReasons.METADATA_CROSS_SITE, "metadata-cross-site")]
    [InlineData(D2ForgeryRefusalReasons.METADATA_UNKNOWN_VALUE, "metadata-unknown-value")]
    [InlineData(D2ForgeryRefusalReasons.ORIGIN_MISMATCH, "origin-mismatch")]
    [InlineData(D2ForgeryRefusalReasons.HEADERS_ABSENT, "headers-absent")]
    [InlineData(D2ForgeryRefusalReasons.TOKEN_ABSENT, "token-absent")]
    [InlineData(D2ForgeryRefusalReasons.TOKEN_STALE, "token-stale")]
    [InlineData(D2ForgeryRefusalReasons.TOKEN_FORGED, "token-forged")]
    [InlineData(D2ForgeryRefusalReasons.TOKEN_UNVERIFIABLE, "token-unverifiable")]
    [InlineData(D2ForgeryRefusalReasons.VERIFIER_ABSENT, "verifier-absent")]
    public void RefusalReason_HasItsCanonicalWireValue(string actual, string expected) =>
        actual.Should().Be(expected);

    [Theory]
    [InlineData(D2ForgeryTokenVerdict.NoSession, 0)]
    [InlineData(D2ForgeryTokenVerdict.Bound, 1)]
    [InlineData(D2ForgeryTokenVerdict.Absent, 2)]
    [InlineData(D2ForgeryTokenVerdict.Stale, 3)]
    [InlineData(D2ForgeryTokenVerdict.Forged, 4)]
    [InlineData(D2ForgeryTokenVerdict.Unverifiable, 5)]
    public void Verdict_HasItsPinnedOrdinal(D2ForgeryTokenVerdict verdict, int expected) =>
        ((int)verdict).Should().Be(expected);

    [Fact]
    public void Verdict_ZeroMember_IsNoSession() =>
        default(D2ForgeryTokenVerdict).Should().Be(D2ForgeryTokenVerdict.NoSession);

    [Fact]
    public void Telemetry_MeterName_HasItsCanonicalValue() =>
        ForgeryProtectionTelemetry.METER_NAME.Should().Be("DcsvIo.D2.AspNetCore.Forgery");

    [Fact]
    public void Telemetry_OutcomeTagKey_HasItsCanonicalValue() =>
        ForgeryProtectionTelemetry.TAG_OUTCOME.Should().Be("outcome");

    [Fact]
    public void Telemetry_Meter_IsNamedForTheMeterNameConstant() =>
        ForgeryProtectionTelemetry.SR_Meter.Name
            .Should().Be(ForgeryProtectionTelemetry.METER_NAME);

    [Fact]
    public void Telemetry_RefusalCounter_CarriesItsPinnedNameAndUnit()
    {
        ForgeryProtectionTelemetry.SR_Refusals.Name
            .Should().Be("d2.aspnetcore.forgery.refusals");
        ForgeryProtectionTelemetry.SR_Refusals.Unit.Should().Be("{refusal}");
    }

    [Fact]
    public void Telemetry_TokenHalfSkippedCounter_CarriesItsPinnedNameAndUnit()
    {
        ForgeryProtectionTelemetry.SR_TokenHalfSkipped.Name
            .Should().Be("d2.aspnetcore.forgery.token_half_skipped");
        ForgeryProtectionTelemetry.SR_TokenHalfSkipped.Unit.Should().Be("{request}");
    }
}
