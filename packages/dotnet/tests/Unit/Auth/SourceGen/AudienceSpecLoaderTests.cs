// -----------------------------------------------------------------------
// <copyright file="AudienceSpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Audiences.SourceGen;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="AudienceSpecLoader.Load"/>. The loader
/// validates only the JSON shape (D2AUD001 - malformed spec).
/// <see cref="AudiencesEmitter"/> performs all semantic validation (name shape,
/// URL parsing, duplicates), and its tests cover that separately.
/// </summary>
public sealed class AudienceSpecLoaderTests
{
    // ----------------------------------------------------------------------
    // Happy path
    // ----------------------------------------------------------------------

    [Fact]
    public void Load_HappyPath_ReturnsSpecWithEntries()
    {
        const string json = """
        {
          "audiences": [
            {
              "name": "Catalog",
              "url": "https://catalog.example.internal",
              "description": "Catalog service."
            }
          ]
        }
        """;

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.Audiences.Should().HaveCount(1);
        var audience = result.Spec.Audiences[0];
        audience.Name.Should().Be("Catalog");
        audience.Url.Should().Be("https://catalog.example.internal");
        audience.Description.Should().Be("Catalog service.");
    }

    [Fact]
    public void Load_DescriptionAbsent_ReturnsSpecWithNullDescription()
    {
        const string json = """
        { "audiences": [ { "name": "Catalog", "url": "https://catalog.example.internal" } ] }
        """;

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Audiences[0].Description.Should().BeNull();
    }

    [Fact]
    public void Load_MultipleAudiences_PreservesOrder()
    {
        const string json = """
        {
          "audiences": [
            { "name": "Zeta",    "url": "https://z.internal" },
            { "name": "Alpha",   "url": "https://a.internal" },
            { "name": "Mu",      "url": "https://m.internal" }
          ]
        }
        """;

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Audiences.Select(a => a.Name)
            .Should().ContainInOrder("Zeta", "Alpha", "Mu");
    }

    // ----------------------------------------------------------------------
    // D2AUD001 - malformed JSON / schema-violating
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("{not valid json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("\"a string\"")]
    [InlineData("[]")]
    public void Load_NonObjectRoot_EmitsD2AUD001(string json)
    {
        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingAudiencesArray_EmitsD2AUD001()
    {
        const string json = """{ "notAudiences": [] }""";

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("audiences");
    }

    [Fact]
    public void Load_AudiencesIsObjectNotArray_EmitsD2AUD001()
    {
        const string json = """{ "audiences": { "x": 1 } }""";

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_AudienceEntryIsString_EmitsD2AUD001()
    {
        const string json = """{ "audiences": ["not an object"] }""";

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("audiences[0]");
    }

    [Fact]
    public void Load_AudienceMissingName_EmitsD2AUD001()
    {
        const string json = """{ "audiences": [ { "url": "https://x.internal" } ] }""";

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("name");
    }

    [Fact]
    public void Load_AudienceMissingUrl_EmitsD2AUD001()
    {
        const string json = """{ "audiences": [ { "name": "Catalog" } ] }""";

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("url");
    }

    [Fact]
    public void Load_AudienceNameIsNumber_EmitsD2AUD001()
    {
        const string json = """
        { "audiences": [ { "name": 42, "url": "https://x.internal" } ] }
        """;

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_AudienceUrlIsNumber_EmitsD2AUD001()
    {
        const string json = """
        { "audiences": [ { "name": "Catalog", "url": 42 } ] }
        """;

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    // ----------------------------------------------------------------------
    // Adversarial - extra unknown properties
    //
    // The schema sets additionalProperties to `false` (an editor-time gate), but
    // the loader only validates JSON shape. The loader silently ignores unknown
    // properties on an audience entry at load time; the schema gate catches
    // them in editors, IDEs and a JSON-Schema CLI.
    // ----------------------------------------------------------------------

    [Fact]
    public void Load_UnknownExtraPropertyOnAudience_IsIgnoredByLoader()
    {
        const string json = """
        {
          "audiences": [
            { "name": "Catalog", "url": "https://catalog.example.internal",
              "extraField": "ignored", "anotherExtra": 42 }
          ]
        }
        """;

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Audiences.Should().HaveCount(1);
        result.Spec.Audiences[0].Name.Should().Be("Catalog");
    }

    [Fact]
    public void Load_DescriptionWrongType_IsTreatedAsAbsent()
    {
        // description is optional; the loader silently treats a non-string
        // value as absent and does not reject it, which keeps the loader
        // lenient on optional fields.
        const string json = """
        { "audiences": [ { "name": "Catalog", "url": "https://x.internal", "description": 42 } ] }
        """;

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Audiences[0].Description.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // Boundary - empty audiences array
    // ----------------------------------------------------------------------

    [Fact]
    public void Load_EmptyAudiencesArray_ReturnsEmptySpec()
    {
        // The schema's minItems: 1 applies at editor time only; the loader
        // accepts an empty array.
        const string json = """{ "audiences": [] }""";

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Audiences.Should().BeEmpty();
    }
}
