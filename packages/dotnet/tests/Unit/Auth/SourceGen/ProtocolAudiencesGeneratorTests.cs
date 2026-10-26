// -----------------------------------------------------------------------
// <copyright file="ProtocolAudiencesGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Auth.ProtocolAudiences.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// IIncrementalGenerator integration tests that drive
/// <see cref="ProtocolAudiencesGenerator"/> through a synthetic
/// <see cref="CSharpGeneratorDriver"/> and not the build pipeline. They assert
/// assembly-name gating, AdditionalFiles wiring, the missing-spec and
/// malformed-spec degradation paths, and cache stability. The generator and
/// the shared source-gen scaffolding run unmodified; the only test double is
/// <c>InMemoryAdditionalText</c>, which supplies the spec files.
/// </summary>
public sealed class ProtocolAudiencesGeneratorTests
{
    private const string _SAMPLE_SPEC = """
    {
      "protocolAudiences": [
        { "name": "D2_INTERNAL_AUDIENCE", "value": "d2.internal" },
        { "name": "D2_EDGE_SELF_AUDIENCE", "value": "d2-edge" }
      ]
    }
    """;

    [Fact]
    public void Generator_TargetAssemblyWithSpec_EmitsWellKnownAudiencesGeneratedSource()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Auth.Abstractions",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().NotBeEmpty();
        var generated = result.GeneratedTrees.Single();
        Path.GetFileName(generated.FilePath).Should().Be("WellKnownAudiences.g.cs");

        var src = generated.ToString();
        src.Should().Contain("public static partial class WellKnownAudiences");
        src.Should().Contain("public const string D2_INTERNAL_AUDIENCE = \"d2.internal\";");
        src.Should().Contain("public const string D2_EDGE_SELF_AUDIENCE = \"d2-edge\";");
    }

    [Fact]
    public void Generator_EmitsConstantsInDeterministicNameOrder()
    {
        var src = RunGenerator(
                assemblyName: "DcsvIo.D2.Auth.Abstractions",
                specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees.Single().ToString();

        // The generator sorts names ordinally, so D2_EDGE_SELF_AUDIENCE comes
        // before D2_INTERNAL_AUDIENCE whatever the order in the spec.
        var edgeIdx = src.IndexOf("D2_EDGE_SELF_AUDIENCE", System.StringComparison.Ordinal);
        var internalIdx = src.IndexOf("D2_INTERNAL_AUDIENCE", System.StringComparison.Ordinal);
        edgeIdx.Should().BeGreaterThanOrEqualTo(0);
        internalIdx.Should().BeGreaterThan(edgeIdx);
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
    public void Generator_TargetAssemblyButNoSpec_EmitsMissingSpecFileDiagnostic()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Auth.Abstractions",
            specJson: null);

        var result = driver.GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MissingSpecFile);

        // The generator still emits an empty shell, so downstream compilation
        // can see the type.
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

        result.GeneratedTrees.Should().HaveCount(1);
    }

    [Fact]
    public void Generator_RunTwice_SameInputs_ProducesIdenticalOutput()
    {
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

    private static GeneratorDriver RunGenerator(string assemblyName, string? specJson)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ProtocolAudiencesGenerator().AsSourceGenerator();

        var additionalTexts = specJson is null
            ? ImmutableArray<AdditionalText>.Empty
            : ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText(
                "protocol-audiences.spec.json",
                specJson));

        var driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            additionalTexts: additionalTexts);

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
