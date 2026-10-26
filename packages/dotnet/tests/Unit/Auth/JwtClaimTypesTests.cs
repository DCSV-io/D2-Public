// -----------------------------------------------------------------------
// <copyright file="JwtClaimTypesTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth;

using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using Xunit;

public sealed class JwtClaimTypesTests
{
    [Theory]
    [InlineData(nameof(JwtClaimTypes.SUB), "sub")]
    [InlineData(nameof(JwtClaimTypes.AUD), "aud")]
    [InlineData(nameof(JwtClaimTypes.IAT), "iat")]
    [InlineData(nameof(JwtClaimTypes.EXP), "exp")]
    [InlineData(nameof(JwtClaimTypes.AZP), "azp")]
    [InlineData(nameof(JwtClaimTypes.SCOPE), "scope")]
    [InlineData(nameof(JwtClaimTypes.ACT), "act")]
    [InlineData(nameof(JwtClaimTypes.CLIENT_ID), "client_id")]
    [InlineData(nameof(JwtClaimTypes.AMR), "amr")]
    public void StandardOAuthClaims_HaveCanonicalLowercaseNames(string fieldName, string expected)
    {
        // Adversarial: standard OAuth/OIDC claims must keep their canonical
        // names. A prefix change here breaks JWT interoperability with every
        // authorization-server library.
        var actual = (string)typeof(JwtClaimTypes)
            .GetField(fieldName, BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;

        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(nameof(JwtClaimTypes.SESSION_ID), "d2_session_id")]
    [InlineData(nameof(JwtClaimTypes.USERNAME), "d2_username")]
    [InlineData(nameof(JwtClaimTypes.FINGERPRINT), "d2_fp")]
    [InlineData(nameof(JwtClaimTypes.ORG_ID), "d2_org_id")]
    [InlineData(nameof(JwtClaimTypes.ORG_NAME), "d2_org_name")]
    [InlineData(nameof(JwtClaimTypes.ORG_TYPE), "d2_org_type")]
    [InlineData(nameof(JwtClaimTypes.ORG_ROLE), "d2_org_role")]
    [InlineData(nameof(JwtClaimTypes.ROOT_ORG_ID), "d2_root_org_id")]
    [InlineData(nameof(JwtClaimTypes.ACCESSIBLE_ORG_IDS), "d2_accessible_org_ids")]
    [InlineData(nameof(JwtClaimTypes.ACT_KIND), "d2_kind")]
    [InlineData(nameof(JwtClaimTypes.ACT_SESSION_ID), "d2_session_id")]
    [InlineData(nameof(JwtClaimTypes.STEP_UP_AT), "d2_step_up_at")]
    public void D2Claims_HaveExpectedLiteralValues(string fieldName, string expected)
    {
        var actual = (string)typeof(JwtClaimTypes)
            .GetField(fieldName, BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;

        actual.Should().Be(expected);
    }

    [Fact]
    public void EveryD2Claim_HasD2UnderscorePrefix()
    {
        // Adversarial: enumerate every constant via reflection and assert that
        // every constant outside the well-known standard set has the d2_
        // prefix. A later addition that omits the prefix fails here.
        // Standard set: RFC 7519 (sub/aud/iat/exp), RFC 7519 section 4.1.7 (azp),
        // RFC 6749 section 3.3 (scope), RFC 8693 section 2.1 (act),
        // RFC 8693 section 4.3 and RFC 9068 section 2.2 (client_id).
        // These keep their canonical names.
        string[] canonicalStandardNames =
            ["sub", "aud", "iat", "exp", "azp", "scope", "act", "client_id", "amr"];

        var d2Constants = typeof(JwtClaimTypes)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .Where(v => !canonicalStandardNames.Contains(v))
            .ToList();

        d2Constants.Should().NotBeEmpty();
        d2Constants.Should().AllSatisfy(c =>
            c.Should().StartWith("d2_", "every D2 custom claim must be d2_-namespaced"));
    }

    [Fact]
    public void NoClaimUsesColonSeparator_ColonsCollideWithScopePunctuation()
    {
        // Adversarial: scope strings (RFC 6749 section 3.3) use `:` as the punctuation
        // separator (e.g. `org:read`). Claim names must not contain it.
        var allConstants = typeof(JwtClaimTypes)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .ToList();

        allConstants.Should().AllSatisfy(c =>
            c.Should().NotContain(":", "claim names must not use colons"));
    }
}
