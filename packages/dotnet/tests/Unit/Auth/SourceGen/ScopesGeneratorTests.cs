// -----------------------------------------------------------------------
// <copyright file="ScopesGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Scopes.SourceGen;
using DcsvIo.D2.Tests.Unit.SourceGen.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// IIncrementalGenerator integration tests that drive <see cref="ScopesGenerator"/>
/// through a synthetic <see cref="CSharpGeneratorDriver"/> and not the build
/// pipeline. They assert the assembly-name gate, AdditionalFiles wiring, and
/// that wildcard expansion uses the OrgType / Role enum members found in the
/// compilation.
/// </summary>
public sealed class ScopesGeneratorTests
{
    private const string _EXTENSION_ASSEMBLY = "Fixture.Scopes.Host";

    private const string _SAMPLE_SPEC = """
    {
      "scopes": [
        {
          "name": "self.read",
          "impersonationBlocked": false,
          "grantedTo": { "*": ["*"] }
        },
        {
          "name": "anon.public.health",
          "impersonationBlocked": false
        }
      ]
    }
    """;

    private const string _ENUM_SOURCE = """
    namespace DcsvIo.D2.Auth.Abstractions
    {
        public enum OrgType { Admin, Customer }
        public enum Role { Owner, Agent }
        public enum ActionSensitivity { Routine, Sensitive, Critical }
    }
    """;

    private const string _PRIVATE_SPEC = """
    {
      "scopes": [
        {
          "name": "internal.example.sign",
          "impersonationBlocked": true,
          "internal": true
        },
        {
          "name": "orders.payment.capture",
          "impersonationBlocked": true,
          "grantedTo": { "Admin": ["Owner"] }
        }
      ]
    }
    """;

    private const string _UNDECLARED_INTERNAL_SPEC = """
    {
      "scopes": [
        {
          "name": "internal.example.sign",
          "impersonationBlocked": true
        }
      ]
    }
    """;

    private static readonly Dictionary<string, string> sr_extensionProperties = new()
    {
        ["D2ScopesExtensionNamespace"] = "Example.Ext",
        ["D2ScopesExtensionClassPrefix"] = "Sample",
    };

    [Fact]
    public void Generator_TargetAssemblyWithSpec_EmitsScopesGeneratedSource()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Auth.Abstractions",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().NotBeEmpty();
        var generated = result.GeneratedTrees.Single();
        Path.GetFileName(generated.FilePath).Should().Be("Scopes.g.cs");

        var src = generated.ToString();
        src.Should().Contain("public static partial class Scopes");
        src.Should().Contain("\"self.read\"");
        src.Should().Contain("\"anon.public.health\"");
    }

    [Fact]
    public void Generator_NonTargetAssembly_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "Some.Other.Assembly",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();

        // The generator does nothing for a non-target assembly, so it produces
        // no Scopes.g.cs.
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_TargetAssemblyButNoSpec_EmitsMissingSpecFileDiagnostic()
    {
        // No AdditionalText is supplied, so the generator must report D2SCP009.
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Auth.Abstractions",
            specJson: null);

        var result = driver.GetRunResult();

        var diagnostics = result.Diagnostics;
        diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MissingSpecFile);
    }

    [Fact]
    public void Generator_InternalPrefixWithoutTheFlag_SurfacesD2SCP010AsARoslynDiagnostic()
    {
        // The emit-side diagnostic reaches the build only through the
        // generator's descriptor switch, so a new identifier that the switch
        // does not list throws and does not report.
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Auth.Abstractions",
            specJson: _UNDECLARED_INTERNAL_SPEC);

        var result = driver.GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.InternalFlagPrefixDisagreement);
        result.Diagnostics
            .Single(d => d.Id == DiagnosticIds.InternalFlagPrefixDisagreement)
            .Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Generator_InternalFlagAgreeingWithThePrefix_SurfacesNoD2SCP010()
    {
        // The admitting case over the same call site.
        var driver = RunGenerator(
            assemblyName: _EXTENSION_ASSEMBLY,
            specJson: null,
            buildProperties: sr_extensionProperties,
            multiSpecs:
            [
                ("public/scopes.spec.json", _SAMPLE_SPEC),
                ("private/scopes.spec.json", _PRIVATE_SPEC),
            ]);

        var result = driver.GetRunResult();

        result.Diagnostics.Should()
            .NotContain(d => d.Id == DiagnosticIds.InternalFlagPrefixDisagreement);
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

        // The generator emits an empty shell file even on malformed input, so
        // downstream compilation can still see the Scopes type, which avoids
        // cascade errors.
        result.GeneratedTrees.Should().HaveCount(1);
    }

    [Fact]
    public void Generator_WildcardExpansion_UsesEnumMembersFromCompilation()
    {
        // The synthetic compilation supplies custom enum members (Admin and
        // Customer for OrgType; Owner and Agent for Role). Wildcard expansion
        // must use exactly these and not a hard-coded list inside the SrcGen.
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Auth.Abstractions",
            specJson: _SAMPLE_SPEC);

        var src = driver.GetRunResult().GeneratedTrees.Single().ToString();

        // The Cartesian product of {Admin, Customer} x {Owner, Agent} must appear.
        src.Should().Contain("[(OrgType.Admin, Role.Owner)]");
        src.Should().Contain("[(OrgType.Admin, Role.Agent)]");
        src.Should().Contain("[(OrgType.Customer, Role.Owner)]");
        src.Should().Contain("[(OrgType.Customer, Role.Agent)]");

        // No mention of enum members that the synthetic compilation does not
        // define.
        src.Should().NotContain("OrgType.Support");
        src.Should().NotContain("Role.Auditor");
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
    public void Generator_ExtensionTarget_MultiSpec_EmitsPrefixedScopesUnion()
    {
        // Two spec AdditionalFiles passed together to an assembly that sets the
        // extension properties produce one scopes file with both halves present
        // (multi-spec merge).
        var driver = RunGenerator(
            assemblyName: _EXTENSION_ASSEMBLY,
            specJson: null,
            buildProperties: sr_extensionProperties,
            multiSpecs:
            [
                ("public/scopes.spec.json", _SAMPLE_SPEC),
                ("private/scopes.spec.json", _PRIVATE_SPEC),
            ]);

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(1);
        Path.GetFileName(result.GeneratedTrees.Single().FilePath)
            .Should().Be("SampleScopes.g.cs");

        var src = result.GeneratedTrees.Single().ToString();
        src.Should().Contain("namespace Example.Ext;");
        src.Should().Contain("public static partial class SampleScopes");
        src.Should().Contain("\"self.read\"");
        src.Should().Contain("\"internal.example.sign\"");
        src.Should().Contain("\"orders.payment.capture\"");

        // The Scopes class must not be emitted for the extension target.
        src.Should().NotContain("public static partial class Scopes");
    }

    [Fact]
    public void Generator_ExtensionTarget_DuplicateScopeId_EmitsCollisionDiagnostic()
    {
        // Same scope name in two specs -> D2SCP DuplicateScope diagnostic; first wins.
        const string colliding_private = """
        {
          "scopes": [
            {
              "name": "self.read",
                  "impersonationBlocked": true
            }
          ]
        }
        """;

        var driver = RunGenerator(
            assemblyName: _EXTENSION_ASSEMBLY,
            specJson: null,
            buildProperties: sr_extensionProperties,
            multiSpecs:
            [
                ("a/scopes.spec.json", _SAMPLE_SPEC),
                ("b/scopes.spec.json", colliding_private),
            ]);

        var result = driver.GetRunResult();
        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.DuplicateScope);

        var src = result.GeneratedTrees.Single().ToString();

        // This test asserts the duplicate-scope diagnostic and that
        // "self.read" appears in the output. It does not check how many
        // self.read constants the merge emits or which definition wins.
        src.Should().Contain("\"self.read\"");
    }

    [Fact]
    public void Generator_WrongAssembly_WithMultiSpec_EmitsNothing()
    {
        // Assembly gate: the generator does nothing for multi-spec inputs on a
        // non-target host.
        var driver = RunGenerator(
            assemblyName: "Some.Other.Assembly",
            specJson: null,
            multiSpecs:
            [
                ("public/scopes.spec.json", _SAMPLE_SPEC),
                ("private/scopes.spec.json", _PRIVATE_SPEC),
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
            syntaxTrees: [CSharpSyntaxTree.ParseText(_ENUM_SOURCE)],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ScopesGenerator().AsSourceGenerator();

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
                new InMemoryAdditionalText("scopes.spec.json", specJson));
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
