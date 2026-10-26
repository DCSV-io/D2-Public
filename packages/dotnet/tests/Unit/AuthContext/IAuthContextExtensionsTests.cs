// -----------------------------------------------------------------------
// <copyright file="IAuthContextExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AuthContext;

using System;
using System.Collections.Generic;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.AuthContext.Abstractions;
using Xunit;

public sealed class IAuthContextExtensionsTests
{
    // ----------------------------------------------------------------------
    // HasScope
    // ----------------------------------------------------------------------

    [Fact]
    public void HasScope_PresentInSet_ReturnsTrue()
    {
        var ctx = new TestAuthContext { Scopes = ScopeSet("self.read", "self.write") };

        ctx.HasScope("self.read").Should().BeTrue();
    }

    [Fact]
    public void HasScope_AbsentFromSet_ReturnsFalse()
    {
        var ctx = new TestAuthContext { Scopes = ScopeSet("self.read") };

        ctx.HasScope("orders.order.cancel").Should().BeFalse();
    }

    [Fact]
    public void HasScope_EmptyScopeSet_ReturnsFalse()
    {
        // Adversarial: a pre-auth context (no scopes resolved yet) must not
        // grant anything.
        var ctx = new TestAuthContext { Scopes = ScopeSet() };

        ctx.HasScope("self.read").Should().BeFalse();
    }

    [Fact]
    public void HasScope_DifferentCasing_ReturnsFalse()
    {
        // Adversarial: scope names are case-sensitive (RFC 6749 section 3.3). The
        // backing HashSet uses StringComparer.Ordinal in this test's factory
        // and in MutableRequestContext.
        var ctx = new TestAuthContext { Scopes = ScopeSet("self.read") };

        ctx.HasScope("Self.Read").Should().BeFalse();
        ctx.HasScope("SELF.READ").Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // HasAnyScope
    // ----------------------------------------------------------------------

    [Fact]
    public void HasAnyScope_NoArgs_ReturnsFalse()
    {
        // Adversarial: empty params array. `Any()` over an empty array returns
        // false, which is the right semantics: "require any of nothing" is
        // unmet.
        var ctx = new TestAuthContext { Scopes = ScopeSet("self.read") };

        ctx.HasAnyScope().Should().BeFalse();
    }

    [Fact]
    public void HasAnyScope_SingleScopeMatch_ReturnsTrue()
    {
        var ctx = new TestAuthContext { Scopes = ScopeSet("self.read") };

        ctx.HasAnyScope("self.read").Should().BeTrue();
    }

    [Fact]
    public void HasAnyScope_MultiPartialMatch_ReturnsTrue()
    {
        var ctx = new TestAuthContext { Scopes = ScopeSet("self.read") };

        ctx.HasAnyScope("orders.order.cancel", "self.read", "orders.payment.capture")
            .Should().BeTrue();
    }

    [Fact]
    public void HasAnyScope_MultiAllMatch_ReturnsTrue()
    {
        var ctx = new TestAuthContext
        {
            Scopes = ScopeSet("self.read", "self.write", "orders.order.cancel"),
        };

        ctx.HasAnyScope("self.read", "self.write").Should().BeTrue();
    }

    [Fact]
    public void HasAnyScope_NoneMatch_ReturnsFalse()
    {
        var ctx = new TestAuthContext { Scopes = ScopeSet("self.read") };

        ctx.HasAnyScope("orders.order.cancel", "orders.payment.capture").Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // HasAllScopes
    // ----------------------------------------------------------------------

    [Fact]
    public void HasAllScopes_NoArgs_ReturnsTrue()
    {
        // Adversarial: empty params. `All()` over an empty array returns true.
        // This test documents the vacuous-truth behavior: an empty array gets
        // an "all of nothing" pass, so an authorization check that must reject
        // an empty requirement needs its own guard before the call.
        var ctx = new TestAuthContext { Scopes = ScopeSet() };

        ctx.HasAllScopes().Should().BeTrue();
    }

    [Fact]
    public void HasAllScopes_SinglePresent_ReturnsTrue()
    {
        var ctx = new TestAuthContext { Scopes = ScopeSet("self.read") };

        ctx.HasAllScopes("self.read").Should().BeTrue();
    }

    [Fact]
    public void HasAllScopes_PartialMatch_ReturnsFalse()
    {
        var ctx = new TestAuthContext { Scopes = ScopeSet("self.read") };

        ctx.HasAllScopes("self.read", "orders.order.cancel").Should().BeFalse();
    }

    [Fact]
    public void HasAllScopes_AllPresent_ReturnsTrue()
    {
        var ctx = new TestAuthContext
        {
            Scopes = ScopeSet("self.read", "self.write", "orders.order.cancel"),
        };

        ctx.HasAllScopes("self.read", "self.write").Should().BeTrue();
    }

    [Fact]
    public void HasAllScopes_NoneMatch_ReturnsFalse()
    {
        var ctx = new TestAuthContext { Scopes = ScopeSet("self.read") };

        ctx.HasAllScopes("orders.order.cancel", "orders.payment.capture").Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // IsStaff
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(OrgType.Admin, true)]
    [InlineData(OrgType.Support, true)]
    [InlineData(OrgType.Customer, false)]
    [InlineData(OrgType.ThirdParty, false)]
    [InlineData(OrgType.Affiliate, false)]
    public void IsStaff_MatchesAdminAndSupport(OrgType orgType, bool expected)
    {
        var ctx = new TestAuthContext { OrgType = orgType };

        ctx.IsStaff().Should().Be(expected);
    }

    [Fact]
    public void IsStaff_NullOrgType_ReturnsFalse()
    {
        // Adversarial: a pre-auth context (OrgType not yet resolved) must not
        // be classified as staff.
        var ctx = new TestAuthContext { OrgType = null };

        ctx.IsStaff().Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // IsAdmin
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(OrgType.Admin, true)]
    [InlineData(OrgType.Support, false)]
    [InlineData(OrgType.Customer, false)]
    [InlineData(OrgType.ThirdParty, false)]
    [InlineData(OrgType.Affiliate, false)]
    public void IsAdmin_MatchesOnlyAdmin(OrgType orgType, bool expected)
    {
        var ctx = new TestAuthContext { OrgType = orgType };

        ctx.IsAdmin().Should().Be(expected);
    }

    [Fact]
    public void IsAdmin_NullOrgType_ReturnsFalse()
    {
        var ctx = new TestAuthContext { OrgType = null };

        ctx.IsAdmin().Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // IsForcedImpersonation / IsConsentImpersonation
    // ----------------------------------------------------------------------

    [Fact]
    public void IsForcedImpersonation_ImpersonationKindForce_ReturnsTrue()
    {
        var ctx = new TestAuthContext { ImpersonationKind = ImpersonationKind.Force };

        ctx.IsForcedImpersonation().Should().BeTrue();
    }

    [Fact]
    public void IsForcedImpersonation_ImpersonationKindConsent_ReturnsFalse()
    {
        // Adversarial: Consent (OTP-authorized, available to staff) must not
        // be confused with Force (silent, admin-only).
        var ctx = new TestAuthContext { ImpersonationKind = ImpersonationKind.Consent };

        ctx.IsForcedImpersonation().Should().BeFalse();
    }

    [Fact]
    public void IsForcedImpersonation_NullKind_ReturnsFalse()
    {
        var ctx = new TestAuthContext { ImpersonationKind = null };

        ctx.IsForcedImpersonation().Should().BeFalse();
    }

    [Fact]
    public void IsConsentImpersonation_ImpersonationKindConsent_ReturnsTrue()
    {
        var ctx = new TestAuthContext { ImpersonationKind = ImpersonationKind.Consent };

        ctx.IsConsentImpersonation().Should().BeTrue();
    }

    [Fact]
    public void IsConsentImpersonation_ImpersonationKindForce_ReturnsFalse()
    {
        var ctx = new TestAuthContext { ImpersonationKind = ImpersonationKind.Force };

        ctx.IsConsentImpersonation().Should().BeFalse();
    }

    [Fact]
    public void IsConsentImpersonation_NullKind_ReturnsFalse()
    {
        var ctx = new TestAuthContext { ImpersonationKind = null };

        ctx.IsConsentImpersonation().Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // IsImpersonatorStaff / IsImpersonatorAdmin
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(OrgType.Admin, true)]
    [InlineData(OrgType.Support, true)]
    [InlineData(OrgType.Customer, false)]
    [InlineData(OrgType.ThirdParty, false)]
    [InlineData(OrgType.Affiliate, false)]
    public void IsImpersonatorStaff_MatchesAdminAndSupport(OrgType orgType, bool expected)
    {
        var ctx = new TestAuthContext { ImpersonatorOrgType = orgType };

        ctx.IsImpersonatorStaff().Should().Be(expected);
    }

    [Fact]
    public void IsImpersonatorStaff_NullImpersonatorOrgType_ReturnsFalse()
    {
        // Adversarial: when no impersonation is in progress, ImpersonatorOrgType
        // is null and IsImpersonatorStaff() must return false, never true.
        var ctx = new TestAuthContext { ImpersonatorOrgType = null };

        ctx.IsImpersonatorStaff().Should().BeFalse();
    }

    [Theory]
    [InlineData(OrgType.Admin, true)]
    [InlineData(OrgType.Support, false)]
    [InlineData(OrgType.Customer, false)]
    [InlineData(OrgType.ThirdParty, false)]
    [InlineData(OrgType.Affiliate, false)]
    public void IsImpersonatorAdmin_MatchesOnlyAdmin(OrgType orgType, bool expected)
    {
        var ctx = new TestAuthContext { ImpersonatorOrgType = orgType };

        ctx.IsImpersonatorAdmin().Should().Be(expected);
    }

    [Fact]
    public void IsImpersonatorAdmin_NullImpersonatorOrgType_ReturnsFalse()
    {
        var ctx = new TestAuthContext { ImpersonatorOrgType = null };

        ctx.IsImpersonatorAdmin().Should().BeFalse();
    }

    [Fact]
    public void IsImpersonatorAdmin_AgentOrgIsAdminButImpersonatedOrgIsCustomer_StillReportsAdmin()
    {
        // Adversarial: an Admin-org agent impersonating a Customer-org user
        // must report IsImpersonatorAdmin()=true (the agent is admin) and
        // IsAdmin()=false (the operating org is customer). The two must not
        // be confused.
        var ctx = new TestAuthContext
        {
            OrgType = OrgType.Customer,
            ImpersonatorOrgType = OrgType.Admin,
        };

        ctx.IsAdmin().Should().BeFalse("operating org is Customer");
        ctx.IsImpersonatorAdmin().Should().BeTrue("agent's home org is Admin");
        ctx.IsStaff().Should().BeFalse();
        ctx.IsImpersonatorStaff().Should().BeTrue();
    }

    private static IReadOnlySet<string> ScopeSet(params string[] scopes) =>
        new HashSet<string>(scopes, StringComparer.Ordinal);
}
