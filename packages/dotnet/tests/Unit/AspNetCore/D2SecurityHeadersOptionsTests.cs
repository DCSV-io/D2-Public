// -----------------------------------------------------------------------
// <copyright file="D2SecurityHeadersOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AspNetCore;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using Xunit;

public sealed class D2SecurityHeadersOptionsTests
{
    [Fact]
    public void Defaults_AllOverridesAreNull_SignalingDefaultLiteralsApply()
    {
        var opts = new D2SecurityHeadersOptions();

        opts.XContentTypeOptions.Should().BeNull();
        opts.XFrameOptions.Should().BeNull();
        opts.ReferrerPolicy.Should().BeNull();
        opts.XPermittedCrossDomainPolicies.Should().BeNull();
        opts.CrossOriginResourcePolicy.Should().BeNull();
        opts.CrossOriginOpenerPolicy.Should().BeNull();
        opts.StrictTransportSecurity.Should().BeNull();
    }

    [Fact]
    public void DefaultsConstants_PinExpectedLiterals()
    {
        D2SecurityHeadersOptions.DEFAULT_X_CONTENT_TYPE_OPTIONS.Should().Be("nosniff");
        D2SecurityHeadersOptions.DEFAULT_X_FRAME_OPTIONS.Should().Be("DENY");
        D2SecurityHeadersOptions.DEFAULT_REFERRER_POLICY
            .Should().Be("strict-origin-when-cross-origin");
        D2SecurityHeadersOptions.DEFAULT_X_PERMITTED_CROSS_DOMAIN_POLICIES.Should().Be("none");
        D2SecurityHeadersOptions.DEFAULT_CROSS_ORIGIN_RESOURCE_POLICY.Should().Be("same-origin");
        D2SecurityHeadersOptions.DEFAULT_CROSS_ORIGIN_OPENER_POLICY.Should().Be("same-origin");
        D2SecurityHeadersOptions.DEFAULT_STRICT_TRANSPORT_SECURITY
            .Should().Be("max-age=31536000; includeSubDomains");
    }

    [Fact]
    public void DEFAULT_STRICT_TRANSPORT_SECURITY_DoesNotIncludePreload()
    {
        // Submitting a domain to the HSTS preload list is hard to reverse, so
        // the default omits preload and each service decides whether to add it.
        D2SecurityHeadersOptions.DEFAULT_STRICT_TRANSPORT_SECURITY
            .Should().NotContain("preload");
    }

    [Fact]
    public void WithExpression_OverridesPropertyAndPreservesOthers()
    {
        var opts = new D2SecurityHeadersOptions { XFrameOptions = "SAMEORIGIN" };

        opts.XFrameOptions.Should().Be("SAMEORIGIN");
        opts.XContentTypeOptions.Should().BeNull();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new D2SecurityHeadersOptions { XFrameOptions = "DENY" };
        var b = new D2SecurityHeadersOptions { XFrameOptions = "DENY" };

        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        var a = new D2SecurityHeadersOptions { XFrameOptions = "DENY" };
        var b = new D2SecurityHeadersOptions { XFrameOptions = "SAMEORIGIN" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void EmptyOverride_IsAllowed_RepresentsHeaderSuppression()
    {
        // An empty string means "suppress this header". Record construction
        // must accept the value, because validation happens in the middleware
        // path and not when the record is constructed.
        var opts = new D2SecurityHeadersOptions { XFrameOptions = string.Empty };

        opts.XFrameOptions.Should().Be(string.Empty);
    }
}
