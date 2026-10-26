// -----------------------------------------------------------------------
// <copyright file="AudiencesGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Audiences.SourceGen;
using DcsvIo.D2.Tests.Unit.SourceGen.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// IIncrementalGenerator integration tests that drive <see cref="AudiencesGenerator"/>
/// through a synthetic <see cref="CSharpGeneratorDriver"/> and not the build
/// pipeline. They assert assembly-name gating, AdditionalFiles wiring, the
/// missing-spec and malformed-spec degradation paths, and cache stability.
/// </summary>
public sealed class AudiencesGeneratorTests
{
    private const string _SAMPLE_SPEC = """
    {
      "audiences": [
        { "name": "Catalog", "url": "https://catalog.example.internal" },
        { "name": "Shipping", "url": "https://shipping.example.internal" }
      ]
    }
    """;

    private const string _EXTENSION_SPEC = """
    {
      "audiences": [
        { "name": "Inventory", "url": "https://inventory.example.internal" },
        { "name": "Orders", "url": "https://orders.example.internal" }
      ]
    }
    """;

    [Fact]
    public void Generator_TargetAssemblyWithSpec_EmitsAudiencesGeneratedSource()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Auth.Abstractions",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().NotBeEmpty();
        var generated = result.GeneratedTrees.Single();
        Path.GetFileName(generated.FilePath).Should().Be("Audiences.g.cs");

        var src = generated.ToString();
        src.Should().Contain("public static partial class Audiences");
        src.Should().Contain("\"https://catalog.example.internal\"");
        src.Should().Contain("\"https://shipping.example.internal\"");
    }

    [Fact]
    public void Generator_NonTargetAssembly_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "Some.Other.Assembly",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();

        // The generator does nothing for a non-target assembly, so it produces
        // no Audiences.g.cs.
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_TargetAssemblyButNoSpec_EmitsMissingSpecFileDiagnostic()
    {
        // No AdditionalText is supplied, so the generator must report D2AUD006
        // (MissingSpecFile).
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Auth.Abstractions",
            specJson: null);

        var result = driver.GetRunResult();

        var diagnostics = result.Diagnostics;
        diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MissingSpecFile);

        // The generator emits an empty shell file even when the spec is
        // missing, so downstream compilation can still see the Audiences type,
        // which avoids cascade errors.
        result.GeneratedTrees.Should().HaveCount(1);
    }

    [Fact]
    public void Generator_MalformedSpec_EmitsMalformedSpecDiagnosticAndStillProducesEmptyShell()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Auth.Abstractions",
            specJson: "{not valid");

        var result = driver.GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MalformedSpec);

        // The generator still emits an empty shell on malformed input, so
        // downstream compilation does not report "type does not exist" errors
        // that mask the real diagnostic.
        result.GeneratedTrees.Should().HaveCount(1);
    }

    [Fact]
    public void Generator_RunTwice_SameInputs_ProducesIdenticalOutput()
    {
        // Cache stability: identical inputs must produce identical generator
        // output, otherwise downstream incremental builds re-run unnecessarily.
        var first = RunGenerator(
                assemblyName: "DcsvIo.D2.Auth.Abstractions",
                specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees.Single().ToString();

        var second = RunGenerator(
                assemblyName: "DcsvIo.D2.Auth.Abstractions",
                specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees.Single().ToString();

        Normalize(second).Should().Be(Normalize(first));
    }

    // ----------------------------------------------------------------------
    // Extension target / multi-spec merge (an assembly that sets the
    // extension properties and merges several specs)
    // ----------------------------------------------------------------------

    [Fact]
    public void Generator_ExtensionTarget_MultiSpec_EmitsPrefixedAudiencesUnion()
    {
        var driver = RunGenerator(
            assemblyName: "Fixture.Audiences.Host",
            specJson: null,
            multiSpecs:
            [
                ("public/audiences.spec.json", _SAMPLE_SPEC),
                ("private/audiences.spec.json", _EXTENSION_SPEC),
            ],
            buildProperties: new Dictionary<string, string>
            {
                ["D2AudiencesExtensionNamespace"] = "Example.Ext",
                ["D2AudiencesExtensionClassPrefix"] = "Sample",
            });

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(1);
        Path.GetFileName(result.GeneratedTrees.Single().FilePath)
            .Should().Be("SampleAudiences.g.cs");

        var src = result.GeneratedTrees.Single().ToString();
        src.Should().Contain("namespace Example.Ext;");
        src.Should().Contain("public static partial class SampleAudiences");
        src.Should().Contain("\"https://catalog.example.internal\"");
        src.Should().Contain("\"https://inventory.example.internal\"");
        src.Should().Contain("\"https://orders.example.internal\"");
        src.Should().NotContain("public static partial class Audiences");
    }

    [Fact]
    public void Generator_WrongAssembly_WithMultiSpec_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "Some.Other.Assembly",
            specJson: null,
            multiSpecs:
            [
                ("public/audiences.spec.json", _SAMPLE_SPEC),
                ("private/audiences.spec.json", _EXTENSION_SPEC),
            ]);

        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();
    }

    private static GeneratorDriver RunGenerator(
        string assemblyName,
        string? specJson,
        (string Path, string Content)[]? multiSpecs = null,
        IReadOnlyDictionary<string, string>? buildProperties = null)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new AudiencesGenerator().AsSourceGenerator();

        ImmutableArray<AdditionalText> additionalTexts;
        if (multiSpecs is not null)
        {
            additionalTexts = ImmutableArray.CreateRange<AdditionalText>(
                multiSpecs.Select(s => new InMemoryAdditionalText(s.Path, s.Content)));
        }
        else if (specJson is null)
        {
            additionalTexts = ImmutableArray<AdditionalText>.Empty;
        }
        else
        {
            additionalTexts = ImmutableArray.Create<AdditionalText>(
                new InMemoryAdditionalText("audiences.spec.json", specJson));
        }

        var driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            additionalTexts: additionalTexts,
            optionsProvider: FixtureAnalyzerConfigOptionsProvider.FromBuildProperties(
                buildProperties ?? new Dictionary<string, string>()));

        return driver.RunGenerators(compilation);
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Trim();

    /// <summary>
    /// Minimal AdditionalText implementation that synthesizes AdditionalFiles
    /// in generator tests without file system I/O.
    /// </summary>
    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText r_text;

        public InMemoryAdditionalText(string path, string content)
        {
            Path = path;
            r_text = SourceText.From(content);
        }

        public override string Path { get; }

        public override SourceText GetText(
            System.Threading.CancellationToken cancellationToken = default) => r_text;
    }
}
