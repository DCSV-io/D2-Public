// -----------------------------------------------------------------------
// <copyright file="AdvisoryLocksGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AdvisoryLocks.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.AdvisoryLocks.SourceGen;
using DcsvIo.D2.Tests.Unit.SourceGen.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// <see cref="IIncrementalGenerator"/> integration tests for
/// <see cref="AdvisoryLocksGenerator"/>: drives the generator via a synthetic
/// <see cref="CSharpGeneratorDriver"/> with fixture MSBuild properties
/// (<c>build_property.D2AdvisoryLocks*</c>) and no product assembly names.
/// </summary>
public sealed class AdvisoryLocksGeneratorTests
{
    private const string _FIXTURE_NS_ALPHA = "Fixture.Module.Alpha.Infra";
    private const string _FIXTURE_NS_BETA = "Fixture.Module.Beta.Infra";
    private const string _DB_ALPHA = "d2-fixture-alpha";
    private const string _DB_BETA = "d2-fixture-beta";

    private const string _MULTI_DB_SPEC = """
        {
          "locks": [
            {
              "constName": "REINDEX",
              "database": "d2-fixture-alpha",
              "key": 1001,
              "doc": "Alpha reindex."
            },
            {
              "constName": "WORKER",
              "database": "d2-fixture-beta",
              "key": 2002,
              "doc": "Beta worker."
            }
          ]
        }
        """;

    private const string _SAMPLE_SPEC = """
        {
          "locks": [
            {
              "constName": "REINDEX",
              "database": "d2-fixture-alpha",
              "key": 1001,
              "doc": "Reindex lock."
            }
          ]
        }
        """;

    // =========================================================================
    // Properties and AdditionalFiles pipeline (generator emit contract)
    // =========================================================================

    [Fact]
    public void Generator_BothPropsAndValidMultiDbSpec_EmitsFilteredNestInDeclaredNamespace()
    {
        var driver = RunGenerator(
            emitNamespace: _FIXTURE_NS_ALPHA,
            databases: _DB_ALPHA,
            specJson: _MULTI_DB_SPEC);

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(1);
        var source = result.GeneratedTrees[0].ToString();
        source.Should().Contain($"namespace {_FIXTURE_NS_ALPHA};");
        source.Should().Contain("public static class D2FixtureAlpha");
        source.Should().Contain("public const long REINDEX = 1001L;");
        source.Should().NotContain("D2FixtureBeta");
        source.Should().NotContain("WORKER");
    }

    [Fact]
    public void Generator_WhitespaceOnlyProps_TreatedAsEmpty_NoEmit()
    {
        var driver = RunGenerator(
            emitNamespace: "   ",
            databases: "\t",
            specJson: _SAMPLE_SPEC);

        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();
        driver.GetRunResult().Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Generator_CommaAndSpaceSeparatedFilterTokens_TrimsAndSelectsKnownDatabases()
    {
        var driver = RunGenerator(
            emitNamespace: _FIXTURE_NS_ALPHA,
            databases: $"  {_DB_ALPHA} ,  {_DB_BETA}  ",
            specJson: _MULTI_DB_SPEC);

        var source = driver.GetRunResult().GeneratedTrees
            .Should().ContainSingle().Subject.ToString();
        source.Should().Contain("D2FixtureAlpha");
        source.Should().Contain("D2FixtureBeta");
        source.Should().Contain("REINDEX");
        source.Should().Contain("WORKER");
    }

    [Fact]
    public void Generator_MalformedJsonSpec_ReportsD2LCK001_NoValidSource()
    {
        var driver = RunGenerator(
            emitNamespace: _FIXTURE_NS_ALPHA,
            databases: _DB_ALPHA,
            specJson: "{bad json");

        var result = driver.GetRunResult();
        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MalformedSpec);
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_OnlyNamespacePropSet_ReportsD2LCK006_NoSource()
    {
        var driver = RunGenerator(
            emitNamespace: _FIXTURE_NS_ALPHA,
            databases: null,
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();
        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.IncompleteEmitConfig);
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_OnlyDatabasesPropSet_ReportsD2LCK006_NoSource()
    {
        var driver = RunGenerator(
            emitNamespace: null,
            databases: _DB_ALPHA,
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();
        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.IncompleteEmitConfig);
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Theory]
    [InlineData(",")]
    [InlineData(",,,")]
    [InlineData("  ,  , ")]
    public void Generator_NamespaceSet_DatabasesEmptyAfterSplit_ReportsD2LCK006_NoSource(
        string databasesCsv)
    {
        // Databases="," (and similar values) splits into empty tokens after
        // trimming, which reports IncompleteEmitConfig (D2LCK006), as a
        // missing Databases property does.
        var driver = RunGenerator(
            emitNamespace: _FIXTURE_NS_ALPHA,
            databases: databasesCsv,
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();
        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.IncompleteEmitConfig);
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_BothPropsSet_NoAdditionalFiles_EmitsNothing()
    {
        var driver = RunGenerator(
            emitNamespace: _FIXTURE_NS_ALPHA,
            databases: _DB_ALPHA,
            specJson: null);

        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();
        driver.GetRunResult().Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Generator_TwoDriverRuns_IdenticalSourceAndDiagnostics()
    {
        var first = CaptureRun(_SAMPLE_SPEC);
        var second = CaptureRun(_SAMPLE_SPEC);

        first.Sources.Should().BeEquivalentTo(second.Sources);
        first.DiagnosticIds.Should().BeEquivalentTo(second.DiagnosticIds);
    }

    [Fact]
    public void Generator_NoPropsWithSpecPresent_GeneratedTreesEmpty()
    {
        var driver = RunGenerator(
            emitNamespace: null,
            databases: null,
            specJson: _SAMPLE_SPEC);

        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();
        driver.GetRunResult().Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Generator_AllUnknownFilterTokens_ReportsD2LCK007_SuppressesAddSource()
    {
        var driver = RunGenerator(
            emitNamespace: _FIXTURE_NS_ALPHA,
            databases: "d2-unknown-db",
            specJson: _MULTI_DB_SPEC);

        var result = driver.GetRunResult();
        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.UnknownDatabaseFilter);
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_MixedValidAndUnknownFilterTokens_ReportsD2LCK007_SuppressesAddSource()
    {
        var driver = RunGenerator(
            emitNamespace: _FIXTURE_NS_ALPHA,
            databases: $"{_DB_ALPHA},d2-not-in-catalog",
            specJson: _MULTI_DB_SPEC);

        var result = driver.GetRunResult();
        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.UnknownDatabaseFilter);
        result.GeneratedTrees.Should().BeEmpty(
            "mixed valid+invalid filter must not emit a partial AdvisoryLocks class");
    }

    [Fact]
    public void Generator_ADatabaseDeclaredOnlyInClasses_IsAKnownFilterToken()
    {
        // AdvisoryLocksEmitter unions the lock databases with the class
        // databases, so a database that contributes only classes still gets its
        // nest. The unknown-filter check must use the same set.
        const string spec = """
            {
              "locks": [
                {"constName": "REINDEX", "database": "d2-fixture-alpha", "key": 1001, "doc": "d"}
              ],
              "classes": [
                {"constName": "ACCOUNT", "database": "d2-fixture-beta", "class": 8008008, "doc": "d"}
              ]
            }
            """;

        var driver = RunGenerator(
            emitNamespace: _FIXTURE_NS_BETA,
            databases: _DB_BETA,
            specJson: spec);

        var result = driver.GetRunResult();
        result.Diagnostics.Should()
            .NotContain(d => d.Id == DiagnosticIds.UnknownDatabaseFilter);
        result.GeneratedTrees.Should().NotBeEmpty();
    }

    [Fact]
    public void Generator_DuplicateKeyInCatalog_ReportsD2LCK003_BeforeFilter()
    {
        const string spec = """
            {
              "locks": [
                {"constName": "A", "database": "d2-fixture-alpha", "key": 100, "doc": "d"},
                {"constName": "B", "database": "d2-fixture-alpha", "key": 100, "doc": "d"},
                {"constName": "C", "database": "d2-fixture-beta", "key": 200, "doc": "d"}
              ]
            }
            """;

        var driver = RunGenerator(
            emitNamespace: _FIXTURE_NS_BETA,
            databases: _DB_BETA,
            specJson: spec);

        driver.GetRunResult().Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.DuplicateKeyInDatabase);
    }

    [Fact]
    public void Generator_FixtureModuleAlpha_FilterEmitsOnlyAlphaNest()
    {
        var driver = RunGenerator(
            emitNamespace: _FIXTURE_NS_ALPHA,
            databases: _DB_ALPHA,
            specJson: _MULTI_DB_SPEC);

        var source = driver.GetRunResult().GeneratedTrees
            .Should().ContainSingle().Subject.ToString();
        source.Should().Contain($"namespace {_FIXTURE_NS_ALPHA};");
        source.Should().Contain("D2FixtureAlpha");
        source.Should().NotContain("D2FixtureBeta");
    }

    [Fact]
    public void Generator_FixtureModuleBeta_FilterEmitsOnlyBetaNest_IndependentOfAlpha()
    {
        var driver = RunGenerator(
            emitNamespace: _FIXTURE_NS_BETA,
            databases: _DB_BETA,
            specJson: _MULTI_DB_SPEC);

        var source = driver.GetRunResult().GeneratedTrees
            .Should().ContainSingle().Subject.ToString();
        source.Should().Contain($"namespace {_FIXTURE_NS_BETA};");
        source.Should().Contain("D2FixtureBeta");
        source.Should().Contain("WORKER");
        source.Should().NotContain("D2FixtureAlpha");
        source.Should().NotContain("REINDEX");
    }

    [Fact]
    public void Generator_MultiDbCatalog_FilterDropsForeignNests()
    {
        var driver = RunGenerator(
            emitNamespace: _FIXTURE_NS_ALPHA,
            databases: _DB_ALPHA,
            specJson: _MULTI_DB_SPEC);

        var source = driver.GetRunResult().GeneratedTrees
            .Should().ContainSingle().Subject.ToString();
        source.Should().Contain("d2-fixture-alpha");
        source.Should().NotContain("d2-fixture-beta");
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    private static (List<string> Sources, List<string> DiagnosticIds) CaptureRun(string specJson)
    {
        var result = RunGenerator(
                emitNamespace: _FIXTURE_NS_ALPHA,
                databases: _DB_ALPHA,
                specJson: specJson)
            .GetRunResult();

        return (
            result.GeneratedTrees
                .OrderBy(t => t.FilePath)
                .Select(t => t.ToString())
                .ToList(),
            result.Diagnostics.Select(d => d.Id).OrderBy(id => id).ToList());
    }

    private static GeneratorDriver RunGenerator(
        string? emitNamespace,
        string? databases,
        string? specJson)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "Fixture.AdvisoryLocks.Host",
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var additionalTexts = specJson is null
            ? ImmutableArray<AdditionalText>.Empty
            : ImmutableArray.Create<AdditionalText>(
                new InMemoryAdditionalTextFixture("advisory-locks.spec.json", specJson));

        var options = new Dictionary<string, string>(System.StringComparer.Ordinal);
        if (emitNamespace is not null)
            options["build_property.D2AdvisoryLocksEmitNamespace"] = emitNamespace;
        if (databases is not null)
            options["build_property.D2AdvisoryLocksDatabases"] = databases;

        var driver = CSharpGeneratorDriver.Create(
            generators: [new AdvisoryLocksGenerator().AsSourceGenerator()],
            additionalTexts: additionalTexts,
            optionsProvider: new FixtureAnalyzerConfigOptionsProvider(options));

        return driver.RunGenerators(compilation);
    }

    private sealed class InMemoryAdditionalTextFixture : AdditionalText
    {
        private readonly SourceText r_text;

        public InMemoryAdditionalTextFixture(string path, string content)
        {
            Path = path;
            r_text = SourceText.From(content);
        }

        public override string Path { get; }

        public override SourceText GetText(
            System.Threading.CancellationToken cancellationToken = default) => r_text;
    }
}
