// -----------------------------------------------------------------------
// <copyright file="D2ForgeryTokenCookieTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AspNetCore.Forgery;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore.Forgery;
using global::Microsoft.AspNetCore.Http;
using Xunit;

/// <summary>
/// The attributes of the forgery-token cookie, and the expiry that the
/// factory passes through to the cookie options.
/// </summary>
public sealed class D2ForgeryTokenCookieTests
{
    [Fact]
    public void Path_IsRootAsTheHostPrefixRequires() =>
        D2ForgeryTokenCookie.PATH.Should().Be("/");

    [Fact]
    public void Secure_IsTrueAsTheHostPrefixRequires() =>
        D2ForgeryTokenCookie.SECURE.Should().BeTrue();

    [Fact]
    public void HttpOnly_IsFalseSoPageScriptCanReadTheToken() =>
        D2ForgeryTokenCookie.HTTP_ONLY.Should().BeFalse();

    [Fact]
    public void SameSite_IsLax() =>
        D2ForgeryTokenCookie.SAME_SITE.Should().Be(SameSiteMode.Lax);

    [Fact]
    public void Options_CarryTheDeclaredAttributes()
    {
        var options = D2ForgeryTokenCookie.Options(null);

        options.Path.Should().Be(D2ForgeryTokenCookie.PATH);
        options.Secure.Should().Be(D2ForgeryTokenCookie.SECURE);
        options.HttpOnly.Should().Be(D2ForgeryTokenCookie.HTTP_ONLY);
        options.SameSite.Should().Be(D2ForgeryTokenCookie.SAME_SITE);
        options.Domain.Should().BeNull();
    }

    [Fact]
    public void Options_WithNoExpiry_LeaveTheCookieAsASessionCookie() =>
        D2ForgeryTokenCookie.Options(null).Expires.Should().BeNull();

    [Fact]
    public void Options_WithAnExpiry_CarryItThrough()
    {
        var expires = new DateTimeOffset(2027, 3, 4, 5, 6, 7, TimeSpan.Zero);

        D2ForgeryTokenCookie.Options(expires).Expires.Should().Be(expires);
    }
}
