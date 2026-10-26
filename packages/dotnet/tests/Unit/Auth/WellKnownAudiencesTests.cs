// -----------------------------------------------------------------------
// <copyright file="WellKnownAudiencesTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth;

using System.IO;
using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using Xunit;

/// <summary>
/// Unit tests for the hand-declared <see cref="WellKnownAudiences"/> protocol
/// constants - the universal internal <i>receive</i> audience that the
/// forward-unchanged model depends on. Pins the wire value, the
/// reachable-constant shape, and (the most important guard) that the value is
/// hand-declared and not spec-derived: it must not appear in the codegen-emitted
/// <see cref="Audiences"/> surface, which marks the boundary between
/// hand-declared and generated audiences.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WellKnownAudiencesTests
{
    [Fact]
    public void D2InternalAudience_PinsTheWireValue()
    {
        // Pin the receive-audience wire value. Changing it is a breaking change to
        // every hop's `aud == d2.internal` check and to any token minter.
        WellKnownAudiences.D2_INTERNAL_AUDIENCE.Should().Be("d2.internal");
    }

    [Fact]
    public void WellKnownAudiences_IsAStaticClass()
    {
        var type = typeof(WellKnownAudiences);

        type.IsAbstract.Should().BeTrue("static classes are abstract+sealed at IL");
        type.IsSealed.Should().BeTrue();
    }

    [Fact]
    public void D2InternalAudience_IsAPublicCompileTimeConstant()
    {
        // The abstractions package exposes it as a plain compile-time constant
        // (public, static and literal), and not as a runtime-computed value.
        var field = typeof(WellKnownAudiences).GetField(
            nameof(WellKnownAudiences.D2_INTERNAL_AUDIENCE),
            BindingFlags.Public | BindingFlags.Static);

        field.Should().NotBeNull();
        field.IsLiteral.Should().BeTrue("a const is a compile-time literal");
        field.IsInitOnly.Should().BeFalse("a const is not a runtime readonly field");
        field.FieldType.Should().Be<string>();
    }

    // ----------------------------------------------------------------------
    // Codegen boundary: the value is hand-declared and not spec-derived. It
    // must be absent from every projection of the generated Audiences catalog.
    // ----------------------------------------------------------------------

    [Fact]
    public void D2InternalAudience_IsNotAKnownSpecAudience()
    {
        // The spec-generated known set (the inbound aud-validation surface) does
        // not contain the internal receive audience, which shows that the
        // audience is hand-declared and is not an audiences.spec.json entry.
        Audiences.IsKnown(WellKnownAudiences.D2_INTERNAL_AUDIENCE).Should().BeFalse();
    }

    [Fact]
    public void D2InternalAudience_IsAbsentFromEverySpecProjection()
    {
        Audiences.AllUrls.Should().NotContain(WellKnownAudiences.D2_INTERNAL_AUDIENCE);
        Audiences.ByName.Values.Should().NotContain(WellKnownAudiences.D2_INTERNAL_AUDIENCE);
    }

    [Fact]
    public void D2InternalAudience_DoesNotResolveThroughTheSpecHelpers()
    {
        // Neither name->url nor url->name resolution returns it, because no spec
        // entry exists for it to mirror.
        Audiences.Resolve(WellKnownAudiences.D2_INTERNAL_AUDIENCE).Should().BeNull();
        Audiences.ResolveByUrl(WellKnownAudiences.D2_INTERNAL_AUDIENCE).Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // Adversarial: exactly one hand-declared constant carries this value, and
    // it collides with no generated spec URL.
    // ----------------------------------------------------------------------

    [Fact]
    public void OnlyOneHandDeclaredConstant_CarriesTheInternalAudienceValue()
    {
        // Reflection walk over every public const string on WellKnownAudiences:
        // exactly one must equal "d2.internal", so no duplicate copy exists.
        var matches = typeof(WellKnownAudiences)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string?)f.GetRawConstantValue())
            .Count(value => value == "d2.internal");

        matches.Should().Be(1, "exactly one hand-declared constant holds the internal receive audience");
    }

    [Fact]
    public void D2InternalAudience_DoesNotCollideWithAnyGeneratedAudienceUrl()
    {
        // The hand-declared receive audience must be distinct from every
        // spec-derived token-exchange target URL: the two have different roles
        // and different values.
        Audiences.AllUrls.Should().NotContain(WellKnownAudiences.D2_INTERNAL_AUDIENCE);
    }

    [Fact]
    public void D2InternalAudience_IsABareTokenNotAUrl()
    {
        // Format check: the receive audience is a bare token and is intentionally
        // not URL-shaped like the spec entries (https://<audience>.internal and so on).
        // It has no scheme separator, no whitespace, and is not empty.
        var value = WellKnownAudiences.D2_INTERNAL_AUDIENCE;

        value.Should().NotBeNullOrWhiteSpace();
        value.Should().NotContain(":");
        value.Should().NotContainAny(" ", "\t", "\n", "\r");
    }

    // ----------------------------------------------------------------------
    // The self-audience constant is spec-driven, like d2.internal.
    // ----------------------------------------------------------------------

    [Fact]
    public void D2EdgeSelfAudience_PinsTheWireValue()
    {
        WellKnownAudiences.D2_EDGE_SELF_AUDIENCE.Should().Be("d2-edge");
    }

    [Fact]
    public void D2EdgeSelfAudience_IsAPublicCompileTimeConstant()
    {
        var field = typeof(WellKnownAudiences).GetField(
            nameof(WellKnownAudiences.D2_EDGE_SELF_AUDIENCE),
            BindingFlags.Public | BindingFlags.Static);

        field.Should().NotBeNull();
        field.IsLiteral.Should().BeTrue("a const is a compile-time literal");
        field.FieldType.Should().Be<string>();
    }

    // ----------------------------------------------------------------------
    // Single-source parity: the generated C# constants are byte-identical to the
    // protocol-audiences spec (the source of truth) and to the generated
    // TypeScript ProtocolAudiences const. Both runtimes derive from the same
    // spec, so neither can drift from the other, and these tests pin that
    // contract.
    // ----------------------------------------------------------------------

    [Fact]
    public void GeneratedConstants_MatchTheProtocolAudiencesSpecValues()
    {
        var spec = ReadProtocolAudiencesSpec();

        // Each spec entry name maps to a C# const of that name with the spec value.
        foreach (var (name, value) in spec)
        {
            var field = typeof(WellKnownAudiences).GetField(
                name,
                BindingFlags.Public | BindingFlags.Static);

            field.Should().NotBeNull(
                $"every protocol-audience spec entry '{name}' must emit a WellKnownAudiences constant");
            ((string?)field.GetRawConstantValue()).Should().Be(
                value,
                $"WellKnownAudiences.{name} must carry the spec value byte-for-byte");
        }
    }

    [Fact]
    public void GeneratedConstants_ByteMatchTheTypeScriptProtocolAudiences()
    {
        // The TS const-object is emitted from the same spec into
        // @dcsv-io/d2-auth-abstractions. Assert that each C# const value appears in
        // the TS file under the same SCREAMING_SNAKE name, which shows cross-runtime
        // byte identity.
        var spec = ReadProtocolAudiencesSpec();
        var tsPath = Path.Combine(
            TestPaths.PublicPackagesTypescriptRoot(),
            "auth",
            "abstractions",
            "src",
            "protocol-audiences.g.ts");

        File.Exists(tsPath).Should().BeTrue(
            "the generated TypeScript ProtocolAudiences file must be committed at " + tsPath);
        var tsSource = File.ReadAllText(tsPath);

        foreach (var (name, value) in spec)
            tsSource.Should().Contain(
                $"{name}: \"{value}\"",
                $"the TS ProtocolAudiences must carry {name} = {value} byte-identically to C#");
    }

    private static (string Name, string Value)[] ReadProtocolAudiencesSpec()
    {
        var specPath = Path.Combine(
            TestPaths.PublicContractsRoot(),
            "auth-protocol-audiences",
            "protocol-audiences.spec.json");

        using var doc = JsonDocument.Parse(File.ReadAllText(specPath));
        var entries = doc.RootElement.GetProperty("protocolAudiences");
        var result = new (string, string)[entries.GetArrayLength()];
        var i = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            result[i] = (
                entry.GetProperty("name").GetString()!,
                entry.GetProperty("value").GetString()!);
            i++;
        }

        return result;
    }
}
