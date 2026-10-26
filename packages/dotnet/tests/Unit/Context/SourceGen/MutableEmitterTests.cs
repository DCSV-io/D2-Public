// -----------------------------------------------------------------------
// <copyright file="MutableEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Context.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using DcsvIo.D2.Context.SourceGen;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="MutableEmitter.Emit"/>. Asserts the
/// shape of the generated <c>MutableRequestContext</c>:
/// get/set vs computed getters, the FromJwtPayloadNoValidation and FromClaims
/// factories, claim-source dispatch, and the property-name collision
/// diagnostic (D2CTX003).
/// </summary>
public sealed class MutableEmitterTests
{
    // ----------------------------------------------------------------------
    // Mutable shape - non-derived properties get { get; set; }
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_NonDerivedProperty_HasGetAndSet()
    {
        var (auth, request) = MinimalSpecs();

        var mutable = MutableEmitter.Emit(auth, request);

        mutable.Diagnostics.Should().BeEmpty();
        mutable.HintName.Should().Be("MutableRequestContext.g.cs");
        mutable.GeneratedSource.Should().Contain("public string? Subject { get; set; }");
        mutable.GeneratedSource.Should().Contain("public Guid? UserId { get; set; }");
    }

    [Fact]
    public void Emit_DerivedActorChainProperty_IsGetterOnly()
    {
        var (auth, request) = MinimalSpecs();

        var mutable = MutableEmitter.Emit(auth, request);

        // ImmediateCallerClientId / OriginatingClientId / IsServiceIdentity are
        // computed from ActorChain - must not have a setter.
        mutable.GeneratedSource.Should()
            .NotContain("public string? ImmediateCallerClientId { get; set; }");
        mutable.GeneratedSource.Should()
            .NotContain("public string? OriginatingClientId { get; set; }");

        // The computed getter walks ActorChain.
        mutable.GeneratedSource.Should().Contain("ImmediateCallerClientId");
        mutable.GeneratedSource.Should().Contain("ActorChain.FirstOrDefault");
    }

    [Fact]
    public void Emit_IsServiceIdentityDerived_GuardsAgainstNullPreAuth()
    {
        var (auth, request) = MinimalSpecs();

        var mutable = MutableEmitter.Emit(auth, request);

        // Trinary semantics: when IsAuthenticated is null, derived auth flags
        // also return null (pre-auth state). The emitter pattern is identical
        // for IsServiceIdentity and IsImpersonating.
        mutable.GeneratedSource.Should().Contain("if (IsAuthenticated is null)");
    }

    // ----------------------------------------------------------------------
    // Two factories - FromJwtPayloadNoValidation / FromClaims
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_BothFactoriesPresent()
    {
        var (auth, request) = MinimalSpecs();

        var mutable = MutableEmitter.Emit(auth, request);

        mutable.GeneratedSource.Should().Contain("FromJwtPayloadNoValidation(JsonElement payload)");
        mutable.GeneratedSource.Should().Contain("FromClaims(ClaimsPrincipal principal)");

        // Propagation uses the hand-written PropagatedContext and a single
        // x-d2-context header, not an envelope wire shape. The assertions check
        // that the generated source contains no envelope methods.
        mutable.GeneratedSource.Should().NotContain("ContextEnvelope");
        mutable.GeneratedSource.Should().NotContain("FromContextEnvelope");
        mutable.GeneratedSource.Should().NotContain("ToContextEnvelope");
        mutable.GeneratedSource.Should().NotContain("PopulateFromEnvelope");
    }

    [Fact]
    public void Emit_FromJwtPayloadNoValidation_DefaultsIsAuthenticatedToFalseAndHasLoudDoc()
    {
        var (auth, request) = MinimalSpecs();

        var mutable = MutableEmitter.Emit(auth, request);

        // The warning XML doc must contain both phrases: DOES NOT VALIDATE and
        // forged-token impersonation.
        mutable.GeneratedSource.Should().Contain("DOES NOT VALIDATE");
        mutable.GeneratedSource.Should().Contain("forged-token impersonation");

        // Default IsAuthenticated = false in the factory body.
        mutable.GeneratedSource.Should()
            .Contain("ctx.IsAuthenticated = false;  // ⚠ caller MUST set true after validation");
    }

    [Fact]
    public void Emit_FromClaims_UsesFindAllForStringListType()
    {
        // RFC 7519 section 4.1.3: aud claim may be string or array. ASP.NET expands
        // multi-valued claims into multiple Claim entries - FromClaims must
        // use FindAll, not FindFirst, for IReadOnlyList<string> properties.
        var (auth, request) = MinimalSpecs();

        var mutable = MutableEmitter.Emit(auth, request);

        mutable.GeneratedSource.Should().Contain("principal.FindAll(\"aud\")");
    }

    [Fact]
    public void Emit_FromJwtPayloadNoValidation_AcceptsBothStringAndArrayForStringList()
    {
        // RFC 7519 section 4.1.3: the wire format may be a single string or an array.
        // FromJwtPayloadNoValidation must handle both ValueKinds for any
        // IReadOnlyList<string> property.
        var (auth, request) = MinimalSpecs();

        var mutable = MutableEmitter.Emit(auth, request);

        var src = mutable.GeneratedSource;
        src.Should().Contain("JsonValueKind.String");
        src.Should().Contain("JsonValueKind.Array");
    }

    // ----------------------------------------------------------------------
    // D2CTX003 - property-name collision in the combined spec hierarchy
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_PropertyNameCollidesAcrossAuthAndRequest_EmitsD2CTX003()
    {
        var auth = Spec(
            name: "IAuthContext",
            @namespace: "DcsvIo.D2.AuthContext.Abstractions",
            sections: [Section("S", Property("Subject", "string?", claim: "sub"))]);

        var request = Spec(
            name: "IRequestContext",
            @namespace: "DcsvIo.D2.Context.Abstractions",
            extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
            sections: [Section("S", Property("Subject", "string?"))]); // collision

        var mutable = MutableEmitter.Emit(auth, request);

        mutable.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.PropertyNameCollision);
        var diag = mutable.Diagnostics
            .Single(d => d.DescriptorId == DiagnosticIds.PropertyNameCollision);
        ((string)diag.Args[0]).Should().Be("Subject");
    }

    [Fact]
    public void Emit_PropertyNameRepeatedInOneSpec_EmitsD2CTX003AndNoClass()
    {
        var auth = Spec(
            name: "IAuthContext",
            @namespace: "DcsvIo.D2.AuthContext.Abstractions",
            sections: [
                Section(
                    "S",
                    Property("Subject", "string?", claim: "sub"),
                    Property("Subject", "string?", claim: "sub")),
            ]);

        var request = Spec(
            name: "IRequestContext",
            @namespace: "DcsvIo.D2.Context.Abstractions",
            extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
            sections: [Section("Tracing", Property("TraceId", "string?"))]);

        var mutable = MutableEmitter.Emit(auth, request);

        mutable.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.PropertyNameCollision);
        mutable.GeneratedSource.Should().BeEmpty();
    }

    [Fact]
    public void Emit_TwoPropertiesReadSameClaim_NoCollisionDiagnostic()
    {
        // Carve-out: properties in the same spec that intentionally map to the
        // same JWT claim (e.g. Subject and UserId both reading "sub" with
        // different parsers) are allowed, because the collision check compares
        // property names and not claims.
        var auth = Spec(
            name: "IAuthContext",
            @namespace: "DcsvIo.D2.AuthContext.Abstractions",
            sections: [
                Section(
                    "S",
                    Property("Subject", "string?", claim: "sub"),
                    Property("UserId", "Guid?", claim: "sub")),
            ]);

        var request = Spec(
            name: "IRequestContext",
            @namespace: "DcsvIo.D2.Context.Abstractions",
            extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
            sections: [Section("Tracing", Property("TraceId", "string?"))]);

        var mutable = MutableEmitter.Emit(auth, request);

        // Different property names, so no collision (the carve-out is structural,
        // not name-based; the test asserts no false positives on legitimate twin claims).
        mutable.Diagnostics.Should()
            .NotContain(d => d.DescriptorId == DiagnosticIds.PropertyNameCollision);
    }

    // ----------------------------------------------------------------------
    // Adversarial - minimal specs (no derived sections, single-section auth)
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_AuthSpecOnlyAuthSideProperties_RequestSideFieldsAbsent()
    {
        // Defensive: the emitter must produce the fields that the specs declare
        // and no others. The auth spec declares only Subject and the request
        // spec declares only TraceId, so a field that neither declares
        // (OrgName) must be absent. This validates that the emitter does not
        // hard-code assumptions about which fields exist.
        var auth = Spec(
            name: "IAuthContext",
            @namespace: "DcsvIo.D2.AuthContext.Abstractions",
            sections: [Section("Token", Property("Subject", "string?", claim: "sub"))]);

        var request = Spec(
            name: "IRequestContext",
            @namespace: "DcsvIo.D2.Context.Abstractions",
            extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
            sections: [Section("Tracing", Property("TraceId", "string?"))]);

        var mutable = MutableEmitter.Emit(auth, request);

        mutable.Diagnostics.Should().BeEmpty();

        // Auth-side prop appears in mutable.
        mutable.GeneratedSource.Should().Contain("public string? Subject { get; set; }");

        // Request-side prop also appears (since request spec carries it).
        mutable.GeneratedSource.Should().Contain("public string? TraceId { get; set; }");

        // No phantom invented fields.
        mutable.GeneratedSource.Should().NotContain("public string? OrgName { get; set; }");
    }

    [Fact]
    public void Emit_NoActorChainSection_HandlesAbsenceGracefully()
    {
        // A spec with no ActorChain property must produce neither derived
        // getter machinery nor diagnostics. The switch-on-derived emits a
        // placeholder only for an unknown derived rule, and not for a missing
        // ActorChain.
        var auth = Spec(
            name: "IAuthContext",
            @namespace: "DcsvIo.D2.AuthContext.Abstractions",
            sections: [Section("S", Property("Subject", "string?", claim: "sub"))]);

        var request = Spec(
            name: "IRequestContext",
            @namespace: "DcsvIo.D2.Context.Abstractions",
            extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
            sections: [Section("S", Property("TraceId", "string?"))]);

        var mutable = MutableEmitter.Emit(auth, request);

        mutable.Diagnostics.Should().BeEmpty();
        mutable.GeneratedSource.Should().NotContain("ActorChain");
    }

    // ----------------------------------------------------------------------
    // Determinism
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_IdenticalInputs_ProduceIdenticalSource()
    {
        var (auth, request) = MinimalSpecs();

        var first = MutableEmitter.Emit(auth, request);
        var second = MutableEmitter.Emit(auth, request);

        Normalize(second.GeneratedSource).Should().Be(Normalize(first.GeneratedSource));
    }

    // ----------------------------------------------------------------------
    // [RedactData] emission from spec `redact` field
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_NonDerivedPropertyWithRedactTrue_PlacesRedactDataAttribute()
    {
        var auth = Spec(
            name: "IAuthContext",
            @namespace: "DcsvIo.D2.AuthContext.Abstractions",
            sections: [
                Section(
                    "Identity",
                    Property("Username", "string?", claim: "d2_username", redact: true)),
            ]);
        var request = Spec(
            name: "IRequestContext",
            @namespace: "DcsvIo.D2.Context.Abstractions",
            extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
            sections: [Section("S", Property("TraceId", "string?"))]);

        var mutable = MutableEmitter.Emit(auth, request);

        mutable.Diagnostics.Should().BeEmpty();
        var src = Normalize(mutable.GeneratedSource);
        src.Should().Contain("[RedactData(Reason = RedactReason.PersonalInformation)]");

        // The attribute appears immediately above the matching property declaration.
        src.Should().Contain(
            "[RedactData(Reason = RedactReason.PersonalInformation)]\n"
            + "    public string? Username { get; set; }");
    }

    [Fact]
    public void Emit_PropertyWithoutRedact_DoesNotPlaceRedactDataAttribute()
    {
        var (auth, request) = MinimalSpecs();

        var mutable = MutableEmitter.Emit(auth, request);

        mutable.Diagnostics.Should().BeEmpty();

        // None of the MinimalSpecs properties carry redact, so the concrete
        // class does not have the attribute on any property.
        mutable.GeneratedSource.Should().NotContain("[RedactData");
    }

    [Fact]
    public void Emit_RedactAnnotation_AddsUtilitiesAttributeUsings()
    {
        var auth = Spec(
            name: "IAuthContext",
            @namespace: "DcsvIo.D2.AuthContext.Abstractions",
            sections: [
                Section(
                    "Identity",
                    Property("Username", "string?", redact: true)),
            ]);
        var request = Spec(
            name: "IRequestContext",
            @namespace: "DcsvIo.D2.Context.Abstractions",
            extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
            sections: [Section("S", Property("TraceId", "string?"))]);

        var mutable = MutableEmitter.Emit(auth, request);

        mutable.GeneratedSource.Should().Contain("using DcsvIo.D2.Utilities.Attributes;");
        mutable.GeneratedSource.Should().Contain("using DcsvIo.D2.Utilities.Enums;");
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    /// <summary>
    /// Auth + request spec pair carrying enough properties to exercise the
    /// emitter's full type-vocabulary dispatch (string?, Guid?, IReadOnlyList,
    /// trinary bool?, derived actor-chain rules, claim-mapped properties).
    /// </summary>
    private static (ContextSpec Auth, ContextSpec Request) MinimalSpecs()
    {
        var auth = Spec(
            name: "IAuthContext",
            @namespace: "DcsvIo.D2.AuthContext.Abstractions",
            sections:
            [
                Section(
                    "Token",
                    Property("IsAuthenticated", "bool?", trinaryAuth: true),
                    Property("Audience", "IReadOnlyList<string>", claim: "aud"),
                    Property(
                        "ActorChain",
                        "IReadOnlyList<ActorEntry>",
                        claim: "act",
                        @default: "[]")),
                Section(
                    "Identity",
                    Property("Subject", "string?", claim: "sub"),
                    Property("UserId", "Guid?", claim: "sub"),
                    Property("ImmediateCallerClientId", "string?", derived: "actorChain"),
                    Property("OriginatingClientId", "string?", derived: "actorChain"),
                    Property(
                        "IsServiceIdentity",
                        "bool?",
                        trinaryAuth: true,
                        derived: "actorChain")),
                Section(
                    "Impersonation",
                    Property(
                        "IsImpersonating",
                        "bool?",
                        trinaryAuth: true,
                        derived: "actorChain")),
            ]);

        var request = Spec(
            name: "IRequestContext",
            @namespace: "DcsvIo.D2.Context.Abstractions",
            extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
            sections: [
                Section("Tracing", Property("TraceId", "string?")),
            ]);

        return (auth, request);
    }

    private static ContextSpec Spec(
        string name,
        string @namespace,
        string? description = null,
        string? extends = null,
        ImmutableArray<Section> sections = default)
    {
        return new ContextSpec(
            Name: name,
            Namespace: @namespace,
            Description: description,
            Extends: extends,
            Sections: sections.IsDefault ? [] : sections);
    }

    private static Section Section(string name, params PropertySpec[] props) =>
        new(name, [.. props]);

    private static PropertySpec Property(
        string name,
        string type,
        string? claim = null,
        string? claimForm = null,
        bool trinaryAuth = false,
        string? derived = null,
        string? @default = null,
        string? doc = null,
        bool propagate = false,
        int? maxLength = null,
        int? entryIdMaxLength = null,
        bool redact = false) =>
        new(
            name,
            type,
            claim,
            claimForm,
            trinaryAuth,
            derived,
            @default,
            doc,
            propagate,
            maxLength,
            entryIdMaxLength,
            redact);

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Trim();
}
