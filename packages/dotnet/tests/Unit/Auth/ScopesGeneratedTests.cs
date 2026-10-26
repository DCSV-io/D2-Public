// -----------------------------------------------------------------------
// <copyright file="ScopesGeneratedTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using Xunit;

/// <summary>
/// End-to-end smoke tests for the codegen-emitted <c>Scopes.g.cs</c> static
/// partial class, which contains the Anon and Self scopes only. The scope
/// trees outside Anon and Self are not emitted on this class and are not
/// pinned here.
/// </summary>
public sealed class ScopesGeneratedTests
{
    [Fact]
    public void Scopes_TypeExists()
    {
        var scopesType = typeof(Scopes);

        scopesType.Should().NotBeNull();
        scopesType.IsAbstract.Should().BeTrue("static classes are abstract+sealed at IL");
        scopesType.IsSealed.Should().BeTrue();
    }

    [Theory]
    [InlineData("Anon")]
    [InlineData("Self")]
    public void Scopes_HasNestedNamespaceClass(string nestedName)
    {
        // Adversarial: the nested-class structure is how a caller discovers
        // scopes. A flat list would remove that.
        var nested = typeof(Scopes).GetNestedType(nestedName, BindingFlags.Public);

        nested.Should().NotBeNull(nestedName + " nested class must be emitted");
        nested.IsAbstract.Should().BeTrue();
        nested.IsSealed.Should().BeTrue();
    }

    [Fact]
    public void Scopes_NestedClasses_AreExactlyThePublicSpecNamespaces()
    {
        typeof(Scopes).GetNestedTypes(BindingFlags.Public).Select(t => t.Name)
            .Should().BeEquivalentTo(["Anon", "Self"]);
    }

    [Fact]
    public void Scopes_SpecificConstants_HaveExpectedStringValues()
    {
        Scopes.Self.Read.Should().Be("self.read");
        Scopes.Self.Write.Should().Be("self.write");
        Scopes.Anon.Public.Health.Should().Be("anon.public.health");
    }

    // ----------------------------------------------------------------------
    // Helper: IsImpersonationBlocked
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("self.read", false)]
    [InlineData("self.write", false)]
    [InlineData("anon.public.health", false)]
    public void IsImpersonationBlocked_PublicScopes_MatchSpec(string scope, bool expected)
    {
        Scopes.IsImpersonationBlocked(scope).Should().Be(expected);
    }

    [Fact]
    public void IsImpersonationBlocked_UnknownScope_ReturnsFalse()
    {
        Scopes.IsImpersonationBlocked("not.a.real.scope").Should().BeFalse();
        Scopes.IsImpersonationBlocked("orders.refund.force").Should().BeFalse(
            "a scope the public spec does not declare is not in the public catalog");
    }

    // ----------------------------------------------------------------------
    // Helper: IsAnonymous
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("anon.public.health", true)]
    [InlineData("self.read", false)]
    [InlineData("self.write", false)]
    public void IsAnonymous_PrefixCheck(string scope, bool expected)
    {
        Scopes.IsAnonymous(scope).Should().Be(expected);
    }

    [Fact]
    public void IsAnonymous_CaseSensitive()
    {
        // Adversarial: scope names are case-sensitive (RFC 6749 section 3.3).
        Scopes.IsAnonymous("ANON.public.health").Should().BeFalse();
    }

    [Fact]
    public void IsAnonymous_EmptyAndGarbage_ReturnsFalse()
    {
        Scopes.IsAnonymous(string.Empty).Should().BeFalse();
        Scopes.IsAnonymous("anonymous").Should().BeFalse(
            "anonymous must NOT match - only the literal anon. prefix counts");
    }

    // ----------------------------------------------------------------------
    // Helper: IsKnown
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("self.read", true)]
    [InlineData("self.write", true)]
    [InlineData("anon.public.health", true)]
    [InlineData("orders.payment.capture", false)]
    [InlineData("internal.example.sign", false)]
    [InlineData("not.a.real.scope", false)]
    [InlineData("self.READ", false)] // case-sensitive
    public void IsKnown_OnlyMatchesPublicSpecScopes(string scope, bool expected)
    {
        Scopes.IsKnown(scope).Should().Be(expected);
    }

    [Fact]
    public void IsKnown_EmptyString_ReturnsFalse()
    {
        Scopes.IsKnown(string.Empty).Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // Helper: IsGrantedTo
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(OrgType.Customer, Role.Owner, "self.read", true)]
    [InlineData(OrgType.Customer, Role.Agent, "self.write", true)]
    [InlineData(OrgType.Admin, Role.Owner, "self.read", true)]
    [InlineData(OrgType.ThirdParty, Role.Owner, "self.write", true)]
    [InlineData(OrgType.Customer, Role.Owner, "orders.payment.capture", false)]
    [InlineData(OrgType.Admin, Role.Owner, "orders.refund.force", false)]
    public void IsGrantedTo_MatchesPublicSpec(
        OrgType orgType, Role role, string scope, bool expected)
    {
        Scopes.IsGrantedTo(scope, orgType, role).Should().Be(expected);
    }

    [Fact]
    public void IsGrantedTo_UnknownScope_ReturnsFalse()
    {
        Scopes.IsGrantedTo("not.a.real.scope", OrgType.Admin, Role.Owner).Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // AllScopes / AllAnonymousScopes / AllImpersonationBlockedScopes
    // ----------------------------------------------------------------------

    [Fact]
    public void AllScopes_ContainsEveryPublicSpecEntry()
    {
        Scopes.AllScopes.Should().BeEquivalentTo(
            ["anon.public.health", "self.read", "self.write"]);
    }

    [Fact]
    public void AllAnonymousScopes_ContainsOnlyAnonPrefixedScopes()
    {
        Scopes.AllAnonymousScopes.Should().NotBeEmpty();
        Scopes.AllAnonymousScopes.Should().AllSatisfy(s =>
            s.Should().StartWith("anon.", "AllAnonymousScopes must contain only anon.* entries"));
        Scopes.AllAnonymousScopes.Should().BeEquivalentTo(["anon.public.health"]);
    }

    [Fact]
    public void AllImpersonationBlockedScopes_IsEmptyOnPublicCatalog()
    {
        // This Scopes class holds no impersonation-blocked scope; the blocked
        // scopes belong to the scope trees the generator leaves out of it.
        Scopes.AllImpersonationBlockedScopes.Should().BeEmpty();
    }

    [Fact]
    public void AllScopes_IsSuperSetOfAnonAndBlockedAndGranted()
    {
        // Sanity: every anon, every blocked, and every granted scope must be
        // a known scope. A failure shows an inconsistency in the generator.
        Scopes.AllAnonymousScopes.Should().BeSubsetOf(Scopes.AllScopes);
        Scopes.AllImpersonationBlockedScopes.Should().BeSubsetOf(Scopes.AllScopes);

        var allGranted = Scopes.GrantedScopes.Values.SelectMany(s => s).ToHashSet();
        allGranted.Should().BeSubsetOf(Scopes.AllScopes);
    }

    // ----------------------------------------------------------------------
    // GrantedScopes - wildcard expansion
    // ----------------------------------------------------------------------

    [Fact]
    public void GrantedScopes_HasEntryForEveryOrgTypeRoleCombination()
    {
        // Adversarial: the spec uses `{ "*": ["*"] }` for self.* scopes, so the
        // emitter must expand wildcards against the full OrgType x Role
        // cross product. A missing pair would silently deny scopes for some
        // (orgType, role) combinations.
        var expectedCount = Enum.GetValues<OrgType>().Length
            * Enum.GetValues<Role>().Length;

        Scopes.GrantedScopes.Should().HaveCount(expectedCount);
    }

    [Fact]
    public void GrantedScopes_EveryOrgTypeRolePair_HasSelfReadWrite()
    {
        // Adversarial: self.* uses { "*": ["*"] } in the spec, so every pair
        // must end up with self.read and self.write. This test fails when the
        // wildcard expansion drops a tuple.
        foreach (var orgType in Enum.GetValues<OrgType>())
        {
            foreach (var role in Enum.GetValues<Role>())
            {
                var key = (orgType, role);
                Scopes.GrantedScopes.Should().ContainKey(key);
                Scopes.GrantedScopes[key].Should().Contain(
                    ["self.read", "self.write"],
                    $"({orgType},{role}) must inherit wildcard self.* grants");
            }
        }
    }

    [Fact]
    public void GrantedScopes_HoldExactlyThePublicSpecGrants()
    {
        // The public spec grants self.read and self.write to every pair and
        // grants no anon.* scope, so the union of every grant is those two.
        var allGranted = Scopes.GrantedScopes.Values.SelectMany(s => s)
            .ToHashSet(StringComparer.Ordinal);

        allGranted.Should().BeEquivalentTo(["self.read", "self.write"]);
    }

    [Fact]
    public void GrantedScopes_DictionaryIsReadOnly()
    {
        // Adversarial: callers must not be able to change the granted-scopes
        // dictionary at runtime. The exposed view is IReadOnlyDictionary and
        // the value sets are IReadOnlySet. An editable surface would be a
        // security hole, because one rogue handler could grant itself any scope.
        var view = Scopes.GrantedScopes;

        view.Should().BeAssignableTo<IReadOnlyDictionary<(OrgType, Role), IReadOnlySet<string>>>();

        var first = view.First();
        first.Value.Should().BeAssignableTo<IReadOnlySet<string>>();
    }
}
