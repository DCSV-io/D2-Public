// -----------------------------------------------------------------------
// <copyright file="SpecLoaderMissingSpecTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias GeoSourceGen;

namespace DcsvIo.D2.Tests.Unit.Geo.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AwesomeAssertions;
using GeoSourceGen::DcsvIo.D2.Geo.SourceGen;
using GeoSourceGen::DcsvIo.D2.SourceGen;
using Xunit;

/// <summary>
/// D2GEO007: a catalog whose spec file is absent reports MissingSpec, and a catalog whose
/// file is present but malformed reports MalformedSpec and not MissingSpec.
/// </summary>
public sealed class SpecLoaderMissingSpecTests
{
    private const string _ASSEMBLY = "DcsvIo.D2.Geo.Abstractions";

    private static readonly string[] sr_catalogFiles =
    [
        "countries.spec.json",
        "subdivisions.spec.json",
        "currencies.spec.json",
        "languages.spec.json",
        "locales.spec.json",
        "timezones.spec.json",
        "geopolitical-entities.spec.json",
    ];

    [Fact]
    public void LoadAll_NoSpecFiles_ReportsEveryCatalogFileInOrder()
    {
        var diagnostics = Load([], _ASSEMBLY);

        MissingSpecFiles(diagnostics).Should().Equal(sr_catalogFiles);
    }

    [Fact]
    public void LoadAll_OneCatalogFileAbsent_ReportsThatFileAndTheAssembly()
    {
        var present = sr_catalogFiles.Where(f => f != "timezones.spec.json")
            .Select(f => new SpecFile($"contracts/geo/{f}", "{}"));

        var diagnostics = Load(present, _ASSEMBLY);

        diagnostics.Where(d => d.DescriptorId == DiagnosticIds.MissingSpec)
            .Should().ContainSingle()
            .Which.Args.Should().Equal("timezones.spec.json", _ASSEMBLY);
    }

    [Fact]
    public void LoadAll_EveryCatalogFilePresentButMalformed_ReportsNoMissingSpec()
    {
        var present = sr_catalogFiles.Select(f => new SpecFile($"contracts/geo/{f}", "[1]"));

        var diagnostics = Load(present, _ASSEMBLY);

        MissingSpecFiles(diagnostics).Should().BeEmpty();
    }

    [Fact]
    public void LoadAll_AMalformedCatalogFile_ReportsMalformedSpec()
    {
        var present = sr_catalogFiles.Select(f => new SpecFile($"contracts/geo/{f}", "[1]"));

        var diagnostics = Load(present, _ASSEMBLY);

        diagnostics.Count(d => d.DescriptorId == DiagnosticIds.MalformedSpec)
            .Should().Be(sr_catalogFiles.Length);
    }

    [Fact]
    public void LoadAll_FileNameInAnotherCase_CountsAsPresent()
    {
        var present = sr_catalogFiles.Select(
            f => new SpecFile($"contracts/geo/{f.ToUpperInvariant()}", "{}"));

        MissingSpecFiles(Load(present, _ASSEMBLY)).Should().BeEmpty();
    }

    [Fact]
    public void LoadAll_NoAssemblyName_ReportsNoMissingSpec()
    {
        MissingSpecFiles(Load([], assemblyName: null)).Should().BeEmpty();
    }

    private static ImmutableArray<EmitDiagnostic> Load(
        IEnumerable<SpecFile> files,
        string? assemblyName)
    {
        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        SpecLoader.LoadAll([.. files], diagnostics, assemblyName);

        return diagnostics.ToImmutable();
    }

    private static string[] MissingSpecFiles(ImmutableArray<EmitDiagnostic> diagnostics)
        => [.. diagnostics
            .Where(d => d.DescriptorId == DiagnosticIds.MissingSpec)
            .Select(d => (string)d.Args[0])];
}
