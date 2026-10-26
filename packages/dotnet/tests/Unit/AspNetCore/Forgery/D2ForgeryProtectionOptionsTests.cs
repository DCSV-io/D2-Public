// -----------------------------------------------------------------------
// <copyright file="D2ForgeryProtectionOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AspNetCore.Forgery;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore.Forgery;
using Xunit;

/// <summary>
/// The defaults, the constructor overrides and the settable properties of
/// <see cref="D2ForgeryProtectionOptions"/>.
/// </summary>
public sealed class D2ForgeryProtectionOptionsTests
{
    [Fact]
    public void Section_HasItsCanonicalValue() =>
        D2ForgeryProtectionOptions.SECTION.Should().Be("LIB_ASPNETCORE_FORGERY");

    [Fact]
    public void DefaultCookieName_HasItsCanonicalValue() =>
        D2ForgeryProtectionOptions.DEFAULT_TOKEN_COOKIE_NAME.Should().Be("__Host-d2_csrf");

    [Fact]
    public void DefaultHeaderName_HasItsCanonicalValue() =>
        D2ForgeryProtectionOptions.DEFAULT_TOKEN_HEADER_NAME.Should().Be("X-CSRF-Token");

    [Fact]
    public void RequiredCookieNamePrefix_HasItsCanonicalValue() =>
        D2ForgeryProtectionOptions.REQUIRED_COOKIE_NAME_PREFIX.Should().Be("__Host-");

    [Fact]
    public void ParameterlessConstructor_TakesTheDocumentedDefaults()
    {
        var options = new D2ForgeryProtectionOptions();

        options.TokenCookieName.Should()
            .Be(D2ForgeryProtectionOptions.DEFAULT_TOKEN_COOKIE_NAME);
        options.TokenHeaderName.Should()
            .Be(D2ForgeryProtectionOptions.DEFAULT_TOKEN_HEADER_NAME);
    }

    [Fact]
    public void NullCookieNameArgument_TakesTheDocumentedDefault()
    {
        var options = new D2ForgeryProtectionOptions(null, "X-Explicit");

        options.TokenCookieName.Should()
            .Be(D2ForgeryProtectionOptions.DEFAULT_TOKEN_COOKIE_NAME);
    }

    [Fact]
    public void OmittedHeaderNameArgument_TakesTheDocumentedDefault()
    {
        var options = new D2ForgeryProtectionOptions("__Host-explicit");

        options.TokenHeaderName.Should()
            .Be(D2ForgeryProtectionOptions.DEFAULT_TOKEN_HEADER_NAME);
    }

    [Fact]
    public void SuppliedArguments_ReplaceTheDefaults()
    {
        var options = new D2ForgeryProtectionOptions("__Host-alt", "X-Alt");

        options.TokenCookieName.Should().Be("__Host-alt");
        options.TokenHeaderName.Should().Be("X-Alt");
    }

    [Fact]
    public void Properties_AreSettableAfterConstruction()
    {
        var options = new D2ForgeryProtectionOptions
        {
            TokenCookieName = "__Host-later",
            TokenHeaderName = "X-Later",
        };

        options.TokenCookieName.Should().Be("__Host-later");
        options.TokenHeaderName.Should().Be("X-Later");
    }

    [Fact]
    public void TwoInstancesWithTheSameNames_AreEqual() =>
        new D2ForgeryProtectionOptions().Should().Be(new D2ForgeryProtectionOptions());
}
