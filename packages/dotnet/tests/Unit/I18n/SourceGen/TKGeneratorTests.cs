// -----------------------------------------------------------------------
// <copyright file="TKGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.I18n.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.I18n.SourceGen;
using DcsvIo.D2.Tests.Unit.SourceGen.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// IIncrementalGenerator integration tests for emission of both TK types. The
/// generator emits <c>TK</c> into the shared keys assembly and a prefixed key
/// class into a host assembly that sets the extension properties, under
/// different fully qualified names, so that a host can add its own keys
/// without colliding with the shared catalog.
/// </summary>
public sealed class TKGeneratorTests
{
    private const string _EXTENSION_ASSEMBLY = "Fixture.I18n.Host";

    private const string _PUBLIC_EN_US = """
    {
      "common_errors_NOT_FOUND": "Not found."
    }
    """;

    private const string _PRIVATE_EN_US = """
    {
      "keycustodian_errors_KID_INVALID": "Kid invalid.",
      "files_errors_NOT_FOUND": "File not found."
    }
    """;

    private static readonly Dictionary<string, string> sr_extensionProperties = new()
    {
        ["D2TkExtensionNamespace"] = "Example.Ext",
        ["D2TkExtensionClassPrefix"] = "Sample",
    };

    [Fact]
    public void Generator_PublicKeysAssembly_EmitsTKUnderSharedI18n()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.I18n.Keys",
            catalogs:
            [
                ("contracts/messages/en-US.json", _PUBLIC_EN_US),
            ]);

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(1);
        Path.GetFileName(result.GeneratedTrees.Single().FilePath).Should().Be("TK.g.cs");

        var src = result.GeneratedTrees.Single().ToString();
        src.Should().Contain("namespace DcsvIo.D2.I18n;");
        src.Should().Contain("public static partial class TK");
        src.Should().Contain("NOT_FOUND");
    }

    [Fact]
    public void Generator_ExtensionTarget_MultiCatalog_EmitsPrefixedTKUnion()
    {
        // The host assembly merges the shared catalog and the host's own en-US
        // catalog into the prefixed key class.
        var driver = RunGenerator(
            assemblyName: _EXTENSION_ASSEMBLY,
            catalogs:
            [
                ("public/contracts/messages/en-US.json", _PUBLIC_EN_US),
                ("host/contracts/messages/en-US.json", _PRIVATE_EN_US),
            ],
            buildProperties: sr_extensionProperties);

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(1);
        Path.GetFileName(result.GeneratedTrees.Single().FilePath)
            .Should().Be("SampleTK.g.cs");

        var src = result.GeneratedTrees.Single().ToString();
        src.Should().Contain("namespace Example.Ext;");
        src.Should().Contain("public static partial class SampleTK");
        src.Should().Contain("NOT_FOUND");
        src.Should().Contain("KID_INVALID");
        src.Should().NotContain("public static partial class TK");
        src.Should().NotContain("namespace DcsvIo.D2.I18n;");
    }

    [Fact]
    public void Generator_WrongAssembly_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "Some.Other.Assembly",
            catalogs:
            [
                ("contracts/messages/en-US.json", _PUBLIC_EN_US),
            ]);

        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_ExtensionTarget_MissingEnUs_EmitsMissingEnUsDiagnostic()
    {
        var driver = RunGenerator(
            assemblyName: _EXTENSION_ASSEMBLY,
            catalogs: [],
            buildProperties: sr_extensionProperties);

        var result = driver.GetRunResult();
        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MissingEnUsJson);
        result.GeneratedTrees.Should().HaveCount(1);
        result.GeneratedTrees.Single().ToString()
            .Should().Contain("public static partial class SampleTK");
    }

    private static GeneratorDriver RunGenerator(
        string assemblyName,
        (string Path, string Content)[] catalogs,
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

        var generator = new TKGenerator().AsSourceGenerator();

        var additionalTexts = ImmutableArray.CreateRange<AdditionalText>(
            catalogs.Select(c => new InMemoryAdditionalText(c.Path, c.Content)));

        var driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            additionalTexts: additionalTexts,
            optionsProvider: FixtureAnalyzerConfigOptionsProvider.FromBuildProperties(
                buildProperties ?? new Dictionary<string, string>()));

        return driver.RunGenerators(compilation);
    }

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
