// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainsGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.EncryptionDomains.SourceGen;
using DcsvIo.D2.Tests.Unit.SourceGen.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// IIncrementalGenerator integration tests for the EncryptionDomains SrcGen
/// - drives <see cref="EncryptionDomainsGenerator"/> via a synthetic
/// <see cref="CSharpGeneratorDriver"/> rather than the build pipeline.
/// </summary>
public sealed class EncryptionDomainsGeneratorTests
{
    private const string _SAMPLE_SPEC = """
    {
      "domains": [
        {
          "constName": "ORDERS",
          "value": "orders",
          "doc": "doc"
        }
      ]
    }
    """;

    private const string _PUBLIC_PLAINTEXT = """
    {
      "domains": [
        { "constName": "PLAINTEXT", "value": "plaintext", "doc": "sentinel" }
      ]
    }
    """;

    private const string _PRIVATE_PRODUCT = """
    {
      "domains": [
        {
          "constName": "SHIPPING",
          "value": "shipping",
          "mode": "sealed",
          "consumerService": "d2-shipping",
          "doc": "product"
        },
        {
          "constName": "INVENTORY",
          "value": "inventory",
          "mode": "sealed",
          "consumerService": "d2-inventory",
          "doc": "product"
        }
      ]
    }
    """;

    [Fact]
    public void Generator_TargetAssemblyWithSpec_EmitsDomainsGeneratedSource()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Encryption",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(1);
        var fileName = Path.GetFileName(result.GeneratedTrees[0].FilePath);
        fileName.Should().Be("EncryptionDomains.g.cs");
    }

    [Fact]
    public void Generator_NonTargetAssembly_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "Some.Other.Assembly",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_TargetAssemblyButNoSpec_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Encryption",
            specJson: null);

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_MalformedSpec_EmitsMalformedSpecDiagnostic()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Encryption",
            specJson: "{not valid");

        var result = driver.GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Generator_SealedDomainMissingConsumer_SurfacesConsumerDiagnostic()
    {
        // Proves the generator's ResolveDescriptor switch maps the
        // mode/consumer diagnostic IDs to real Roslyn descriptors (a missing
        // switch arm would throw at generation time instead).
        const string sealedNoConsumer = """
        {
          "domains": [
            { "constName": "ORDERS", "value": "orders", "mode": "sealed", "doc": "d" }
          ]
        }
        """;

        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Encryption",
            specJson: sealedNoConsumer);

        var result = driver.GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MissingConsumerService);
    }

    [Fact]
    public void Generator_RunTwice_SameInputs_ProducesIdenticalOutput()
    {
        var first = RunGenerator(
                assemblyName: "DcsvIo.D2.Encryption",
                specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees
            .OrderBy(t => t.FilePath)
            .Select(t => t.ToString())
            .ToList();
        var second = RunGenerator(
                assemblyName: "DcsvIo.D2.Encryption",
                specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees
            .OrderBy(t => t.FilePath)
            .Select(t => t.ToString())
            .ToList();

        second.Count.Should().Be(first.Count);
        for (var i = 0; i < first.Count; i++)
            Normalize(second[i]).Should().Be(Normalize(first[i]));
    }

    // ----------------------------------------------------------------------
    // Extension target / multi-spec merge
    // ----------------------------------------------------------------------

    [Fact]
    public void Generator_ExtensionTarget_MultiSpec_EmitsPrefixedEncryptionDomainsUnion()
    {
        var driver = RunGenerator(
            assemblyName: "Fixture.Encryption.Host",
            specJson: null,
            multiSpecs:
            [
                ("public/encryption-domains.spec.json", _PUBLIC_PLAINTEXT),
                ("private/encryption-domains.spec.json", _PRIVATE_PRODUCT),
            ],
            buildProperties: new Dictionary<string, string>
            {
                ["D2EncryptionDomainsExtensionNamespace"] = "Example.Ext",
                ["D2EncryptionDomainsExtensionClassPrefix"] = "Sample",
            });

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(1);
        Path.GetFileName(result.GeneratedTrees.Single().FilePath)
            .Should().Be("SampleEncryptionDomains.g.cs");

        var src = result.GeneratedTrees.Single().ToString();
        src.Should().Contain("namespace Example.Ext;");
        src.Should().Contain("public static class SampleEncryptionDomains");
        src.Should().Contain("PLAINTEXT");
        src.Should().Contain("SHIPPING");
        src.Should().Contain("INVENTORY");
        src.Should().Contain("SampleEncryptionDomainMode");
        src.Should().Contain("SampleEncryptionDomainModes");
        src.Should().NotContain("public static class EncryptionDomains");
    }

    [Fact]
    public void Generator_WrongAssembly_WithMultiSpec_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "Some.Other.Assembly",
            specJson: null,
            multiSpecs:
            [
                ("public/encryption-domains.spec.json", _PUBLIC_PLAINTEXT),
                ("private/encryption-domains.spec.json", _PRIVATE_PRODUCT),
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

        var generator = new EncryptionDomainsGenerator().AsSourceGenerator();

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
                new InMemoryAdditionalText("encryption-domains.spec.json", specJson));
        }

        var driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            additionalTexts: additionalTexts,
            optionsProvider: FixtureAnalyzerConfigOptionsProvider.FromBuildProperties(
                buildProperties ?? new Dictionary<string, string>()));

        return driver.RunGenerators(compilation);
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Trim();

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
