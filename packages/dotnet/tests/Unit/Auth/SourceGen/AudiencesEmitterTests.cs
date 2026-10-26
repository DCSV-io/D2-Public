// -----------------------------------------------------------------------
// <copyright file="AudiencesEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Audiences.SourceGen;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="AudiencesEmitter.Emit"/> - the pure-logic
/// half of the audiences source generator. Covers semantic validation
/// (D2AUD002 / D2AUD003 / D2AUD004 / D2AUD005) and emission shape.
/// </summary>
public sealed class AudiencesEmitterTests
{
    // ----------------------------------------------------------------------
    // Happy path
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_HappyPath_ProducesConstants()
    {
        var spec = new AudiencesSpec(
        [
            Audience("Catalog", "https://catalog.example.internal", "Catalog service."),
            Audience("Shipping", "https://shipping.example.internal", "Shipping service."),
        ]);

        var result = AudiencesEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public static partial class Audiences");
        result.GeneratedSource.Should().Contain(
            "public const string Catalog = \"https://catalog.example.internal\";");
        result.GeneratedSource.Should().Contain(
            "public const string Shipping = \"https://shipping.example.internal\";");
    }

    [Fact]
    public void Emit_OrdersConstantsAlphabetically()
    {
        var spec = new AudiencesSpec(
        [
            Audience("Zeta", "https://z.internal"),
            Audience("Alpha", "https://a.internal"),
            Audience("Mu", "https://m.internal"),
        ]);

        var result = AudiencesEmitter.Emit(spec);

        var src = result.GeneratedSource;
        var alphaPos = src.IndexOf("public const string Alpha", StringComparison.Ordinal);
        var muPos = src.IndexOf("public const string Mu", StringComparison.Ordinal);
        var zetaPos = src.IndexOf("public const string Zeta", StringComparison.Ordinal);

        alphaPos.Should().BeGreaterThan(0);
        muPos.Should().BeGreaterThan(alphaPos);
        zetaPos.Should().BeGreaterThan(muPos);
    }

    [Fact]
    public void Emit_RendersDescriptionAsXmlDoc()
    {
        var spec = new AudiencesSpec(
        [
            Audience("Catalog", "https://catalog.example.internal", "Catalog listings."),
        ]);

        var result = AudiencesEmitter.Emit(spec);

        result.GeneratedSource.Should().Contain("/// Catalog listings.");
    }

    [Fact]
    public void Emit_FallsBackToUrlWhenDescriptionAbsent()
    {
        var spec = new AudiencesSpec(
        [
            Audience("Catalog", "https://catalog.example.internal"),
        ]);

        var result = AudiencesEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();

        // When description is null, the URL becomes the doc body.
        result.GeneratedSource.Should().Contain("/// https://catalog.example.internal");
    }

    [Fact]
    public void Emit_EscapesXmlMetacharsInDescription()
    {
        var spec = new AudiencesSpec(
        [
            Audience("Catalog", "https://catalog.example.internal", "A & B <crash> service."),
        ]);

        var result = AudiencesEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("A &amp; B &lt;crash&gt; service.");
    }

    // ----------------------------------------------------------------------
    // Helper emission
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_EmitsHelperFunctions()
    {
        var spec = new AudiencesSpec([Audience("Catalog", "https://catalog.example.internal")]);

        var result = AudiencesEmitter.Emit(spec);

        result.GeneratedSource.Should().Contain("public static bool IsKnown(string audience)");
        result.GeneratedSource.Should().Contain("public static string? Resolve(string name)");
        result.GeneratedSource.Should().Contain("public static string? ResolveByUrl(string url)");
        result.GeneratedSource.Should().Contain("public static IReadOnlySet<string> AllUrls");
        result.GeneratedSource.Should().Contain(
            "public static IReadOnlyDictionary<string, string> ByName");
    }

    [Fact]
    public void Emit_PopulatesBackingCollections()
    {
        var spec = new AudiencesSpec(
        [
            Audience("Catalog", "https://catalog.example.internal"),
            Audience("Shipping", "https://shipping.example.internal"),
        ]);

        var result = AudiencesEmitter.Emit(spec);

        var src = result.GeneratedSource;
        src.Should().Contain("private static readonly HashSet<string> sr_allUrls");
        src.Should().Contain("\"https://catalog.example.internal\"");
        src.Should().Contain("\"https://shipping.example.internal\"");
        src.Should().Contain("private static readonly Dictionary<string, string> sr_byName");
        src.Should().Contain("[\"Catalog\"] = \"https://catalog.example.internal\"");
        src.Should().Contain("private static readonly Dictionary<string, string> sr_byUrl");
        src.Should().Contain("[\"https://catalog.example.internal\"] = \"Catalog\"");
    }

    // ----------------------------------------------------------------------
    // D2AUD002 - invalid audience name
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("catalog")] // lowercase first letter
    [InlineData("123Catalog")] // starts with digit
    [InlineData("_Catalog")] // underscore
    [InlineData("My-Service")] // hyphen
    [InlineData("My.Service")] // dot
    [InlineData("My Service")] // space
    [InlineData("")] // empty
    public void Emit_InvalidName_EmitsD2AUD002(string badName)
    {
        var spec = new AudiencesSpec([Audience(badName, "https://x.internal")]);

        var result = AudiencesEmitter.Emit(spec);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.InvalidAudienceName);

        // Anchor on the leading 4-space indent so that the check does not match
        // the class-level XML doc comment, which mentions "public const string".
        result.GeneratedSource.Should().NotContain($"    public const string {badName} = ");
    }

    [Theory]
    [InlineData("Catalog")] // canonical
    [InlineData("CatalogV2")] // digits in middle / end
    [InlineData("ABC123")] // all-uppercase + digits
    [InlineData("X")] // single letter
    public void Emit_ValidName_EmitsConstant(string goodName)
    {
        var spec = new AudiencesSpec([Audience(goodName, "https://x.internal")]);

        var result = AudiencesEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain($"public const string {goodName} = ");
    }

    // ----------------------------------------------------------------------
    // D2AUD003 - duplicate audience name
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_DuplicateName_EmitsD2AUD003AndDropsSecond()
    {
        var spec = new AudiencesSpec(
        [
            Audience("Catalog", "https://catalog.example.internal"),
            Audience("Catalog", "https://catalog-v2.example.internal"),
        ]);

        var result = AudiencesEmitter.Emit(spec);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.DuplicateAudienceName);

        // Only the first mapping survives.
        result.GeneratedSource.Should().Contain(
            "public const string Catalog = \"https://catalog.example.internal\";");
        result.GeneratedSource.Should().NotContain("https://catalog-v2.example.internal");
    }

    // ----------------------------------------------------------------------
    // D2AUD004 - duplicate audience URL
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_DuplicateUrl_EmitsD2AUD004AndDropsSecond()
    {
        var spec = new AudiencesSpec(
        [
            Audience("Catalog", "https://shared.internal"),
            Audience("Catalog2", "https://shared.internal"),
        ]);

        var result = AudiencesEmitter.Emit(spec);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.DuplicateAudienceUrl);
        result.GeneratedSource.Should().Contain(
            "public const string Catalog = \"https://shared.internal\";");
        result.GeneratedSource.Should().NotContain("public const string Catalog2");
    }

    [Fact]
    public void Emit_DuplicateUrlDiagnosticContainsBothNames()
    {
        var spec = new AudiencesSpec(
        [
            Audience("Catalog", "https://shared.internal"),
            Audience("CatalogAlias", "https://shared.internal"),
        ]);

        var result = AudiencesEmitter.Emit(spec);

        var dupe = result.Diagnostics.Single(
            d => d.DescriptorId == DiagnosticIds.DuplicateAudienceUrl);
        dupe.Args[0].Should().Be("Catalog");
        dupe.Args[1].Should().Be("CatalogAlias");
        dupe.Args[2].Should().Be("https://shared.internal");
    }

    // ----------------------------------------------------------------------
    // D2AUD005 - invalid URL
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("not a url")]
    [InlineData("/relative/path")] // not absolute
    [InlineData("catalog.example.internal")] // missing scheme
    [InlineData("")] // empty
    public void Emit_InvalidUrl_EmitsD2AUD005(string badUrl)
    {
        var spec = new AudiencesSpec([Audience("Catalog", badUrl)]);

        var result = AudiencesEmitter.Emit(spec);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.InvalidAudienceUrl);
        result.GeneratedSource.Should().NotContain("public const string Catalog = ");
    }

    [Theory]
    [InlineData("https://catalog.example.internal")]
    [InlineData("https://catalog.example.internal/api/v1")]
    [InlineData("http://localhost:5000")]
    [InlineData("https://catalog.example.internal:8443/path?q=1")]
    public void Emit_ValidAbsoluteUrl_EmitsConstant(string goodUrl)
    {
        var spec = new AudiencesSpec([Audience("Catalog", goodUrl)]);

        var result = AudiencesEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain($"public const string Catalog = \"{goodUrl}\";");
    }

    // ----------------------------------------------------------------------
    // Empty spec
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_EmptySpec_EmitsClassWithNoConstants()
    {
        var spec = new AudiencesSpec([]);

        var result = AudiencesEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public static partial class Audiences");

        // Helpers still emitted (with empty backing collections).
        result.GeneratedSource.Should().Contain("public static bool IsKnown(string audience)");

        // Anchor on the 4-space indent so that the check does not match the
        // class-level XML doc comment, which mentions
        // "public const string {Name} = ...".
        result.GeneratedSource.Should().NotContain("    public const string ");
    }

    // ----------------------------------------------------------------------
    // Mixed valid + invalid
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_OneInvalid_StillEmitsValidEntries()
    {
        var spec = new AudiencesSpec(
        [
            Audience("Catalog", "https://catalog.example.internal"),
            Audience("bad-name", "https://x.internal"),
            Audience("Shipping", "https://shipping.example.internal"),
        ]);

        var result = AudiencesEmitter.Emit(spec);

        result.Diagnostics.Should().HaveCount(1);
        result.Diagnostics[0].DescriptorId.Should().Be(DiagnosticIds.InvalidAudienceName);
        result.GeneratedSource.Should().Contain("public const string Catalog = ");
        result.GeneratedSource.Should().Contain("public const string Shipping = ");
    }

    private static AudienceEntry Audience(string name, string url, string? description = null) =>
        new(Name: name, Url: url, Description: description);
}
