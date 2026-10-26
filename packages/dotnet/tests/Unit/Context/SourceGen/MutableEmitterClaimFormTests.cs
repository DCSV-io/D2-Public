// -----------------------------------------------------------------------
// <copyright file="MutableEmitterClaimFormTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Context.SourceGen;

using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Context.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

/// <summary>
/// The <c>claimForm</c> spec field, and the emitter branches keyed on it.
/// </summary>
/// <remarks>
/// The field exists to stop a set claim being read through the RFC 6749 scope
/// parse, which drops elements it cannot use - a silent narrowing of an
/// authority set. One case builds two specs differing only in
/// <c>claimForm</c> and compares the emitted source as parsed syntax trees; the
/// rest assert the branch markers in the source emitted for a single spec. A
/// branch that stops firing fails here rather than at the next regeneration of
/// the committed output.
/// </remarks>
public sealed class MutableEmitterClaimFormTests
{
    [Fact]
    public void Emit_VerbatimSetProperty_ReadsEveryElementFromTheJsonArray()
    {
        var verbatim = EmitWithClaimForm("verbatimSet");

        // Every element arrives unchanged; the scope parse is not involved.
        verbatim.Should().Contain("GetProperty(\"d2_accessible_org_ids\"");
        verbatim.Should().Contain("JsonValueKind.Array");
        verbatim.Should().NotContain("ScopeClaimParser.ParseJson");
    }

    [Fact]
    public void Emit_VerbatimSetProperty_LeavesThePropertyUnassignedForAnEmptyArray()
    {
        // The ClaimsPrincipal reading cannot tell an empty array from an absent
        // claim - ASP.NET expands an array claim into one Claim per element, so
        // an empty array produces no Claim at all. The JSON reading treats the
        // same input the same way, so that one path does not produce an empty
        // set while the other produces a null.
        var verbatim = EmitWithClaimForm("verbatimSet");

        verbatim.Should().Contain("if (set_AccessibleOrgIds.Count > 0)");
    }

    [Fact]
    public void Emit_DefaultSetProperty_ReadsTheScopeForm()
    {
        var byDefault = EmitWithClaimForm(claimForm: null);

        byDefault.Should().Contain("ScopeClaimParser");
    }

    [Fact]
    public void Emit_VerbatimSetProperty_DiffersFromTheDefaultForm()
    {
        // Two specs differing only in claimForm must not produce the same source,
        // compared as syntax trees.
        var verbatim = Parse(EmitWithClaimForm("verbatimSet"));
        var byDefault = Parse(EmitWithClaimForm(claimForm: null));

        verbatim.IsEquivalentTo(byDefault).Should().BeFalse();
    }

    [Fact]
    public void Emit_VerbatimSetProperty_ReadsEveryPrincipalClaimRatherThanTheFirst()
    {
        // A JSON array claim arrives as one Claim per element, so the principal
        // reading must be FindAll - FindFirst would silently keep one.
        var verbatim = EmitWithClaimForm("verbatimSet");

        verbatim.Should().Contain("FindAll(\"d2_accessible_org_ids\")");
    }

    [Fact]
    public void Emit_SameSpecTwice_IsByteStable()
    {
        EmitWithClaimForm("verbatimSet").Should().Be(EmitWithClaimForm("verbatimSet"));
    }

    [Fact]
    public void Emit_ClaimFormTheEmitterDoesNotImplement_IsABuildError()
    {
        // Without this the loader accepts any string and the emitter falls
        // through to the scope reading, so a typo in the spec produces a
        // silently narrowed authority set.
        var (auth, _) = SpecsWithSetProperty("verbatimSett");

        var result = InterfaceEmitter.Emit(auth);

        result.Diagnostics.Select(d => d.DescriptorId)
            .Should().Contain(DiagnosticIds.UnknownClaimForm);
    }

    [Fact]
    public void Emit_ImplementedClaimForm_RaisesNoDiagnostic()
    {
        var (auth, _) = SpecsWithSetProperty("verbatimSet");

        InterfaceEmitter.Emit(auth).Diagnostics
            .Select(d => d.DescriptorId)
            .Should().NotContain(DiagnosticIds.UnknownClaimForm);
    }

    [Fact]
    public void Emit_NoClaimFormAtAll_RaisesNoDiagnostic()
    {
        var (auth, _) = SpecsWithSetProperty(claimForm: null);

        InterfaceEmitter.Emit(auth).Diagnostics
            .Select(d => d.DescriptorId)
            .Should().NotContain(DiagnosticIds.UnknownClaimForm);
    }

    [Fact]
    public void Emit_SpaceJoinedStringProperty_JoinsTheJsonArray()
    {
        var joined = EmitStringWithClaimForm("spaceJoined");

        joined.Should().Contain("el_AuthMethod.ValueKind == JsonValueKind.Array");
        joined.Should().Contain("string.Join(");
    }

    [Fact]
    public void Emit_SpaceJoinedStringProperty_ReadsEveryPrincipalClaimRatherThanTheFirst()
    {
        var joined = EmitStringWithClaimForm("spaceJoined");

        joined.Should().Contain("FindAll(\"amr\")");
        joined.Should().NotContain("FindFirst(\"amr\")");
    }

    [Fact]
    public void Emit_DefaultStringProperty_ReadsTheFirstPrincipalClaim()
    {
        var byDefault = EmitStringWithClaimForm(claimForm: null);

        byDefault.Should().Contain("FindFirst(\"amr\")");
    }

    [Fact]
    public void Emit_SpaceJoinedStringProperty_DiffersFromTheDefaultForm()
    {
        var joined = Parse(EmitStringWithClaimForm("spaceJoined"));
        var byDefault = Parse(EmitStringWithClaimForm(claimForm: null));

        joined.IsEquivalentTo(byDefault).Should().BeFalse();
    }

    [Fact]
    public void Emit_SpaceJoinedClaimForm_RaisesNoDiagnostic()
    {
        var (auth, _) = SpecsWithStringProperty("spaceJoined");

        InterfaceEmitter.Emit(auth).Diagnostics
            .Select(d => d.DescriptorId)
            .Should().NotContain(DiagnosticIds.UnknownClaimForm);
    }

    [Fact]
    public void Emit_SpaceJoinedOnASetProperty_IsABuildError()
    {
        var (auth, _) = SpecsWithSetProperty("spaceJoined");

        InterfaceEmitter.Emit(auth).Diagnostics
            .Select(d => d.DescriptorId)
            .Should().Contain(DiagnosticIds.UnknownClaimForm);
    }

    [Fact]
    public void Emit_VerbatimSetOnAStringProperty_IsABuildError()
    {
        var (auth, _) = SpecsWithStringProperty("verbatimSet");

        InterfaceEmitter.Emit(auth).Diagnostics
            .Select(d => d.DescriptorId)
            .Should().Contain(DiagnosticIds.UnknownClaimForm);
    }

    private static string EmitStringWithClaimForm(string? claimForm)
    {
        var (auth, request) = SpecsWithStringProperty(claimForm);

        return MutableEmitter.Emit(auth, request).GeneratedSource;
    }

    /// <summary>
    /// The set-property specs with the set replaced by one string property read from
    /// <c>amr</c> under <paramref name="claimForm"/>.
    /// </summary>
    /// <param name="claimForm">The claim form to declare, or null for none.</param>
    /// <returns>The auth and request specs.</returns>
    private static (ContextSpec Auth, ContextSpec Request) SpecsWithStringProperty(
        string? claimForm)
    {
        var (auth, request) = SpecsWithSetProperty(claimForm: null);
        var stringAuth = auth with
        {
            Sections =
            [
                new Section(
                    "Authentication",
                    [
                        new PropertySpec(
                            "AuthMethod",
                            "string?",
                            "amr",
                            claimForm,
                            false,
                            null,
                            null,
                            "Authentication methods.",
                            false,
                            null,
                            null,
                            false),
                    ]),
            ],
        };

        return (stringAuth, request);
    }

    private static string EmitWithClaimForm(string? claimForm)
    {
        var (auth, request) = SpecsWithSetProperty(claimForm);

        return MutableEmitter.Emit(auth, request).GeneratedSource;
    }

    private static SyntaxNode Parse(string source)
        => CSharpSyntaxTree.ParseText(source).GetRoot();

    /// <summary>
    /// Two specs identical but for the claim form on one set-typed property.
    /// </summary>
    /// <param name="claimForm">The claim form to declare, or null for none.</param>
    /// <returns>The auth and request specs.</returns>
    private static (ContextSpec Auth, ContextSpec Request) SpecsWithSetProperty(string? claimForm)
    {
        var auth = new ContextSpec(
            Name: "IAuthContext",
            Namespace: "DcsvIo.D2.AuthContext.Abstractions",
            Description: null,
            Extends: null,
            Sections:
            [
                new Section(
                    "Organization",
                    [
                        new PropertySpec(
                            "AccessibleOrgIds",
                            "IReadOnlySet<string>",
                            "d2_accessible_org_ids",
                            claimForm,
                            false,
                            null,
                            null,
                            "The organizations the caller may reach.",
                            false,
                            null,
                            null,
                            false),
                    ]),
            ]);

        var request = new ContextSpec(
            Name: "IRequestContext",
            Namespace: "DcsvIo.D2.Context.Abstractions",
            Description: null,
            Extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
            Sections:
            [
                new Section(
                    "Tracing",
                    [
                        new PropertySpec(
                            "TraceId",
                            "string?",
                            null,
                            null,
                            false,
                            null,
                            null,
                            "Trace id.",
                            false,
                            null,
                            null,
                            false),
                    ]),
            ]);

        return (auth, request);
    }
}
