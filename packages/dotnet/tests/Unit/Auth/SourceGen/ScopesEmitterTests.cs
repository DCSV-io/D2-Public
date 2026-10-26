// -----------------------------------------------------------------------
// <copyright file="ScopesEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Scopes.SourceGen;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="ScopesEmitter.Emit"/>. Drives the
/// per-scope semantic validation (D2SCP002-008) and asserts the emitted source
/// shape (constants, helper methods, wildcard expansion).
/// </summary>
public sealed class ScopesEmitterTests
{
    private static readonly IReadOnlyList<string> sr_orgTypes =
        ["Admin", "Support", "Customer", "ThirdParty", "Affiliate"];

    private static readonly IReadOnlyList<string> sr_roles =
        ["Auditor", "Agent", "Officer", "Owner"];

    // ----------------------------------------------------------------------
    // Happy-path snapshots
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_SingleAnonScope_NoGrantedToBlock()
    {
        var spec = SpecOf(Scope("anon.public.health", anonOk: true));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();

        // Constant was emitted.
        result.GeneratedSource.Should().Contain(
            "public const string Health = \"anon.public.health\";");

        // AnonymousScopes set carries the entry.
        result.GeneratedSource.Should().Contain("sr_anonymousScopes")
            .And.Contain("\"anon.public.health\",");

        // No granted-to entries in the dictionary backing for this anon scope.
        result.GeneratedSource.Should().NotContain("OrgType.Admin, Role.Owner");
    }

    [Fact]
    public void Emit_ADeprecatedScope_MarksTheConstantObsoleteWithTheReasonAndTheSuccessor()
    {
        var spec = SpecOf(
            Scope(
                "anon.public.old",
                anonOk: true,
                deprecated: true,
                deprecatedReason: "The surface it guarded is retired.",
                replacedBy: "anon.public.health"),
            Scope("anon.public.health", anonOk: true));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain(
            "[Obsolete(\"The surface it guarded is retired. "
            + "Replaced by 'anon.public.health'.\")]");
    }

    [Fact]
    public void Emit_AScopeThatIsNotDeprecated_MarksTheConstantWithNothing()
    {
        // The paired case, over the same call site: without it the assertion
        // above would pass on an emitter that marked every constant.
        var spec = SpecOf(Scope("anon.public.health", anonOk: true));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.GeneratedSource.Should().NotContain("[Obsolete(");
    }

    [Fact]
    public void Emit_ADeprecatedScopeNamingNoReason_StillMarksTheConstant()
    {
        var spec = SpecOf(Scope("anon.public.old", anonOk: true, deprecated: true));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.GeneratedSource.Should().Contain(
            "[Obsolete(\"Scope 'anon.public.old' is deprecated.\")]");
    }

    [Fact]
    public void Emit_AReasonAlreadyNamingTheSuccessor_DoesNotNameItTwice()
    {
        var spec = SpecOf(
            Scope(
                "anon.public.old",
                anonOk: true,
                deprecated: true,
                deprecatedReason: "Replaced by anon.public.health.",
                replacedBy: "anon.public.health"),
            Scope("anon.public.health", anonOk: true));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.GeneratedSource.Should().Contain(
            "[Obsolete(\"Replaced by anon.public.health.\")]");
    }

    [Fact]
    public void Emit_NonAnonWithFullWildcard_ExpandsToCartesianProduct()
    {
        var spec = SpecOf(Scope("self.read", grantedTo: Granted(("*", ["*"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();

        // Every (OrgType x Role) pair must have a grantedTo entry.
        foreach (var org in sr_orgTypes)
        {
            foreach (var role in sr_roles)
                result.GeneratedSource.Should().Contain($"[(OrgType.{org}, Role.{role})]");
        }
    }

    [Fact]
    public void Emit_NonAnonWithSingleEntry_EmitsExactPair()
    {
        var spec = SpecOf(
            Scope("orders.refund.approve", grantedTo: Granted(("Customer", ["Owner"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();

        // Exactly one (OrgType.Customer, Role.Owner) entry.
        result.GeneratedSource.Should().Contain("[(OrgType.Customer, Role.Owner)]");

        // Other org types must not appear in any granted-to dictionary entry.
        result.GeneratedSource.Should().NotContain("[(OrgType.Admin,");
        result.GeneratedSource.Should().NotContain("[(OrgType.Affiliate,");
    }

    [Fact]
    public void Emit_OrgWildcardWithSpecificRole_ExpandsAllOrgs()
    {
        var spec = SpecOf(Scope("billing.read", grantedTo: Granted(("*", ["Owner"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();

        foreach (var org in sr_orgTypes)
            result.GeneratedSource.Should().Contain($"[(OrgType.{org}, Role.Owner)]");

        // Non-Owner roles must not appear paired with these orgs for this scope.
        result.GeneratedSource.Should().NotContain("[(OrgType.Admin, Role.Auditor)]");
    }

    [Fact]
    public void Emit_RoleWildcardWithSpecificOrg_ExpandsAllRoles()
    {
        var spec = SpecOf(Scope("ops.read", grantedTo: Granted(("Customer", ["*"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();

        foreach (var role in sr_roles)
            result.GeneratedSource.Should().Contain($"[(OrgType.Customer, Role.{role})]");

        // Other org types must not show up.
        result.GeneratedSource.Should().NotContain("[(OrgType.Admin, Role.Owner)]");
    }

    // ----------------------------------------------------------------------
    // Helpers / generated-API surface
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_HelperMethodsAndCollections_AllPresent()
    {
        var spec = SpecOf(
            Scope("anon.public.health", anonOk: true),
            Scope("orders.order.cancel", impBlocked: true, grantedTo: Granted(("*", ["*"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();

        // Helper functions.
        result.GeneratedSource.Should().Contain(
            "public static bool IsImpersonationBlocked(string scope)");
        result.GeneratedSource.Should().Contain("public static bool IsAnonymous(string scope)");
        result.GeneratedSource.Should().Contain("public static bool IsKnown(string scope)");
        result.GeneratedSource.Should().Contain(
            "public static bool IsGrantedTo(string scope, OrgType orgType, Role role)");

        // Public read-only set surfaces.
        result.GeneratedSource.Should().Contain("public static IReadOnlySet<string> AllScopes");
        result.GeneratedSource.Should().Contain(
            "public static IReadOnlySet<string> AllAnonymousScopes");
        result.GeneratedSource.Should().Contain(
            "public static IReadOnlySet<string> AllImpersonationBlockedScopes");
        result.GeneratedSource.Should().Contain("GrantedScopes");
    }

    // ----------------------------------------------------------------------
    // D2SCP002 - unknown enum values
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_UnknownOrgTypeInGrantedTo_EmitsD2SCP002()
    {
        var spec = SpecOf(
            Scope("self.read", grantedTo: Granted(("UnknownOrg", ["Owner"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        var diag = result.Diagnostics.Single(d => d.DescriptorId == DiagnosticIds.UnknownEnumValue);
        ((string)diag.Args[2]).Should().Be("OrgType");
        ((string)diag.Args[3]).Should().Be("UnknownOrg");
    }

    [Fact]
    public void Emit_UnknownRoleInGrantedTo_EmitsD2SCP002()
    {
        var spec = SpecOf(
            Scope("self.read", grantedTo: Granted(("Admin", ["Wizard"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        var diag = result.Diagnostics.Single(d => d.DescriptorId == DiagnosticIds.UnknownEnumValue);
        ((string)diag.Args[2]).Should().Be("Role");
        ((string)diag.Args[3]).Should().Be("Wizard");
    }

    // ----------------------------------------------------------------------
    // D2SCP003 - invalid scope name
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("UPPER.case", "lowercase letter")]
    [InlineData("foo..bar", "consecutive dots")]
    [InlineData(".leading.dot", "leading or trailing dot")]
    [InlineData("trailing.dot.", "leading or trailing dot")]
    [InlineData("1numeric.start", "lowercase letter")]
    [InlineData("foo.bar-baz", "invalid character")]
    [InlineData("foo.bar baz", "invalid character")]
    [InlineData("singlesegment", "at least 2 dot-separated segments")]
    [InlineData("foo.b@d", "invalid character")]
    public void Emit_InvalidScopeName_EmitsD2SCP003(string scopeName, string reasonContains)
    {
        var spec = SpecOf(Scope(scopeName, anonOk: true));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        var diag = result.Diagnostics.Single(d => d.DescriptorId == DiagnosticIds.InvalidScopeName);
        ((string)diag.Args[0]).Should().Be(scopeName);
        ((string)diag.Args[1]).Should().Contain(reasonContains);
    }

    // ----------------------------------------------------------------------
    // D2SCP004 - duplicate scope name
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_DuplicateScopeName_EmitsD2SCP004()
    {
        var spec = SpecOf(
            Scope("self.read", grantedTo: Granted(("*", ["*"]))),
            Scope("self.read", grantedTo: Granted(("*", ["*"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.DuplicateScope);
        var diag = result.Diagnostics.Single(d => d.DescriptorId == DiagnosticIds.DuplicateScope);
        ((string)diag.Args[0]).Should().Be("self.read");
    }

    // ----------------------------------------------------------------------
    // D2SCP005 - anon scope marked impersonationBlocked (warning)
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_AnonScopeWithImpersonationBlocked_EmitsD2SCP005Warning()
    {
        var spec = SpecOf(Scope("anon.public.health", anonOk: true, impBlocked: true));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.AnonImpersonationBlockedNoise);

        // The warning is not fatal, so the scope is still emitted.
        result.GeneratedSource.Should().Contain("\"anon.public.health\"");
    }

    // ----------------------------------------------------------------------
    // D2SCP006 - empty role array
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_EmptyRoleArray_EmitsD2SCP006AndDropsScope()
    {
        var spec = SpecOf(Scope("self.read", grantedTo: Granted(("*", []))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        var diag = result.Diagnostics.Single(d => d.DescriptorId == DiagnosticIds.EmptyRoleArray);
        ((string)diag.Args[0]).Should().Be("self.read");
        ((string)diag.Args[1]).Should().Be("*");

        // The scope is dropped, so it must not appear as a constant.
        result.GeneratedSource.Should().NotContain("public const string Read = \"self.read\";");
    }

    // ----------------------------------------------------------------------
    // D2SCP008 - non-anon scope missing grantedTo (separate from "anon" rule)
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_NonAnonMissingGrantedTo_EmitsD2SCP008()
    {
        var spec = SpecOf(Scope("self.read"));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.MissingGrantedTo);
    }

    [Fact]
    public void Emit_InternalScopeMissingGrantedTo_IsExemptFromD2SCP008_AndEmitsConstant()
    {
        // A scope declaring internal is granted by the internal transaction-token
        // mint and not by the org-role grant matrix, so omitting grantedTo is
        // legitimate (no D2SCP008). The scope is still not anonymous: no user
        // org-role can ever hold it, which is the intended reachability for a
        // service-to-service scope.
        var spec = SpecOf(Scope("internal.example.sign", impBlocked: true, isInternal: true));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().NotContain(
            d => d.DescriptorId == DiagnosticIds.MissingGrantedTo,
            "a scope declaring internal is token-granted, not org-role-granted");
        result.GeneratedSource.Should().Contain(
            "\"internal.example.sign\"",
            "the internal scope constant is still emitted despite omitting grantedTo");
    }

    // ----------------------------------------------------------------------
    // D2SCP010 - the declared internal flag disagrees with the name prefix
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_InternalPrefixWithoutTheFlag_EmitsD2SCP010()
    {
        // The runtime strips scopes by the name prefix, so a prefixed scope that
        // the catalog does not declare internal would be stripped from HTTP
        // callers while the catalog treats it as an ordinary scope that needs a
        // grant matrix.
        var spec = SpecOf(Scope("internal.example.sign", impBlocked: true));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.InternalFlagPrefixDisagreement);
    }

    [Fact]
    public void Emit_TheFlagWithoutTheInternalPrefix_EmitsD2SCP010()
    {
        // The inverse: a scope that the catalog declares internal, but whose
        // name the runtime strip does not match, would reach an HTTP caller
        // intact.
        var spec = SpecOf(Scope("self.read", isInternal: true));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.InternalFlagPrefixDisagreement);
    }

    [Fact]
    public void Emit_TheFlagAgreeingWithThePrefix_EmitsNoD2SCP010()
    {
        // The admitting case over the same call site: without it the two
        // assertions above would pass on an emitter that refused every entry.
        var spec = SpecOf(Scope("internal.example.sign", impBlocked: true, isInternal: true));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().NotContain(
            d => d.DescriptorId == DiagnosticIds.InternalFlagPrefixDisagreement);
    }

    [Fact]
    public void Emit_NeitherTheFlagNorThePrefix_EmitsNoD2SCP010()
    {
        var spec = SpecOf(Scope("self.read", grantedTo: Granted(("*", ["*"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().NotContain(
            d => d.DescriptorId == DiagnosticIds.InternalFlagPrefixDisagreement);
    }

    [Fact]
    public void Emit_InternalFlagPrefixDisagreement_DropsTheEntryFromTheOutput()
    {
        var spec = SpecOf(
            Scope("internal.example.sign", impBlocked: true),
            Scope("internal.example.keyring", impBlocked: true, isInternal: true));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.GeneratedSource.Should().NotContain("\"internal.example.sign\"");
        result.GeneratedSource.Should().Contain("\"internal.example.keyring\"");
    }

    [Fact]
    public void Emit_InternalFlagPrefixDisagreement_NamesTheScopeAndBothHalves()
    {
        var spec = SpecOf(Scope("internal.example.sign", impBlocked: true));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        var diagnostic = result.Diagnostics.Single(
            d => d.DescriptorId == DiagnosticIds.InternalFlagPrefixDisagreement);

        diagnostic.Args.Should().Equal("internal.example.sign", "false", "carries");
    }

    [Fact]
    public void Emit_FlaggedScopeOutsideThePrefix_NamesBothHalvesTheOtherWayRound()
    {
        var spec = SpecOf(Scope("self.read", isInternal: true));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        var diagnostic = result.Diagnostics.Single(
            d => d.DescriptorId == DiagnosticIds.InternalFlagPrefixDisagreement);

        diagnostic.Args.Should().Equal("self.read", "true", "does not carry");
    }

    [Fact]
    public void Emit_ScopeNamedInternalWithoutADot_IsNotTreatedAsInternal()
    {
        // The prefix is "internal." - a single segment merely starting with the
        // word is an ordinary scope.
        var spec = SpecOf(
            Scope("internalish.read", grantedTo: Granted(("*", ["*"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("\"internalish.read\"");
    }

    [Fact]
    public void Emit_ScopeNamedInternalWithoutADotDeclaringTheFlag_EmitsD2SCP010()
    {
        // The other half of the prefix boundary: the name is not under
        // "internal.", so declaring the flag on it disagrees with the runtime
        // strip exactly as it does on any other unprefixed name.
        var spec = SpecOf(Scope("internalish.read", isInternal: true));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.InternalFlagPrefixDisagreement);
    }

    [Fact]
    public void Emit_InternalScopeDeclaringGrantedTo_StillExpandsTheGrant()
    {
        // The flag exempts an entry from needing a grant matrix; it does not
        // forbid one, and an entry that declares one is still expanded.
        var spec = SpecOf(
            Scope(
                "internal.example.sign",
                impBlocked: true,
                isInternal: true,
                grantedTo: Granted(("Admin", ["Owner"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("OrgType.Admin, Role.Owner");
    }

    // ----------------------------------------------------------------------
    // D2SCP007 - tree-position collision
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_TreePositionCollision_EmitsD2SCP007AndDropsParent()
    {
        var spec = SpecOf(
            Scope("orders.refund", grantedTo: Granted(("Admin", ["Owner"]))),
            Scope("orders.refund.force", grantedTo: Granted(("Admin", ["Owner"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.TreePositionCollision);

        // Parent is dropped; child remains.
        result.GeneratedSource.Should().Contain("\"orders.refund.force\"");
        result.GeneratedSource.Should().NotContain("\"orders.refund\",");
    }

    // ----------------------------------------------------------------------
    // Adversarial - unusual scope shapes still emit
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_VeryLongScopeName_EmitsCleanly()
    {
        // The name has two segments, "foo" and a 144-character second
        // segment, so it meets the two-segment minimum.
        const string longSegment =
            "abcdefghijklmnopqrstuvwxyz0123456789abcdefghijklmnopqrstuvwxyz0123456789"
            + "abcdefghijklmnopqrstuvwxyz0123456789abcdefghijklmnopqrstuvwxyz0123456789";
        var name = $"foo.{longSegment}";

        var spec = SpecOf(Scope(name, grantedTo: Granted(("*", ["*"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain($"\"{name}\"");
    }

    [Fact]
    public void Emit_FiveSegmentScopeName_EmitsNestedClassChain()
    {
        var spec = SpecOf(Scope("a.b.c.d.e", grantedTo: Granted(("*", ["*"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public static class A");
        result.GeneratedSource.Should().Contain("public static class B");
        result.GeneratedSource.Should().Contain("public static class C");
        result.GeneratedSource.Should().Contain("public static class D");
        result.GeneratedSource.Should().Contain("public const string E = \"a.b.c.d.e\";");
    }

    [Fact]
    public void Emit_ZeroScopes_StillEmitsClassShell()
    {
        var spec = new ScopesSpec(ImmutableArray<ScopeEntry>.Empty);

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public static partial class Scopes");
    }

    [Fact]
    public void Emit_HundredScopes_DoesNotDegrade()
    {
        var entries = ImmutableArray.CreateBuilder<ScopeEntry>();
        for (var i = 0; i < 100; i++)
        {
            entries.Add(new ScopeEntry(
                Name: $"bulk.scope{i}",
                Description: $"Bulk scope {i}",
                ImpersonationBlocked: false,
                GrantedTo: new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal)
                {
                    ["*"] = ["Owner"],
                }));
        }

        var spec = new ScopesSpec(entries.ToImmutable());

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("\"bulk.scope0\"");
        result.GeneratedSource.Should().Contain("\"bulk.scope99\"");
    }

    // ----------------------------------------------------------------------
    // Determinism / cache stability - identical inputs produce identical output
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_IdenticalInputs_ProduceIdenticalSource()
    {
        // Incremental-generator caching depends on determinism: re-runs with
        // the same input must produce the same output, otherwise the cache is
        // defeated.
        var spec = SpecOf(
            Scope("self.read", grantedTo: Granted(("*", ["*"]))),
            Scope("self.write", grantedTo: Granted(("*", ["*"]))));

        var first = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);
        var second = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        Normalize(second.GeneratedSource).Should().Be(Normalize(first.GeneratedSource));
    }

    [Fact]
    public void Emit_ScopesInDifferentOrder_ProduceIdenticalSource()
    {
        // The emitted source does not depend on spec order, so a downstream cache
        // keyed on that source stays valid when the order changes.
        var forward = SpecOf(
            Scope("a.read", grantedTo: Granted(("*", ["*"]))),
            Scope("z.read", grantedTo: Granted(("*", ["*"]))));
        var reverse = SpecOf(
            Scope("z.read", grantedTo: Granted(("*", ["*"]))),
            Scope("a.read", grantedTo: Granted(("*", ["*"]))));

        var forwardResult = ScopesEmitter.Emit(forward, sr_orgTypes, sr_roles);
        var reverseResult = ScopesEmitter.Emit(reverse, sr_orgTypes, sr_roles);

        Normalize(reverseResult.GeneratedSource).Should()
            .Be(Normalize(forwardResult.GeneratedSource));
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static ScopesSpec SpecOf(params ScopeEntry[] entries) =>
        new([.. entries]);

    private static ScopeEntry Scope(
        string name,
        bool impBlocked = false,
        bool anonOk = false,
        IReadOnlyDictionary<string, ImmutableArray<string>>? grantedTo = null,
        bool deprecated = false,
        string? deprecatedReason = null,
        string? replacedBy = null,
        bool isInternal = false)
    {
        // Anonymous scopes legitimately have no grantedTo; every other scope
        // gets the supplied value, or null, which triggers D2SCP008.
        var effectiveGranted = anonOk ? null : grantedTo;
        return new ScopeEntry(
            Name: name,
            Description: null,
            ImpersonationBlocked: impBlocked,
            GrantedTo: effectiveGranted,
            Deprecated: deprecated,
            DeprecatedReason: deprecatedReason,
            ReplacedBy: replacedBy,
            Internal: isInternal);
    }

    private static IReadOnlyDictionary<string, ImmutableArray<string>> Granted(
        params (string Org, string[] Roles)[] entries)
    {
        var dict = new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal);
        foreach (var (org, roles) in entries)
            dict[org] = [.. roles];

        return dict;
    }

    private static string Normalize(string s) =>
        s.Replace("\r\n", "\n").Trim();
}
