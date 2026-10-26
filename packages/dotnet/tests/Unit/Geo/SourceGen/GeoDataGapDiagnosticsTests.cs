// -----------------------------------------------------------------------
// <copyright file="GeoDataGapDiagnosticsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias GeoSourceGen;

namespace DcsvIo.D2.Tests.Unit.Geo.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Tests.Unit.Geo.SourceGen.Fixtures;
using DcsvIo.D2.Tests.Unit.SourceGen.Fixtures;
using GeoSourceGen::DcsvIo.D2.Geo.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

/// <summary>
/// D2GEO002 (UnknownFk) and D2GEO004 (InvalidIdentifier): the Default-target
/// emitters report an Info diagnostic where they skip a reference with no
/// catalog entry or a code that does not convert to a C# identifier.
/// </summary>
public sealed class GeoDataGapDiagnosticsTests
{
    private const string _DEFAULT_ASSEMBLY = "DcsvIo.D2.Geo.Default";
    private const string _ABSTRACTIONS_ASSEMBLY = "DcsvIo.D2.Geo.Abstractions";
    private const string _UNKNOWN_FK = "D2GEO002";
    private const string _INVALID_IDENTIFIER = "D2GEO004";

    [Fact]
    public void Generate_PrimaryLanguageWithNoLanguageEntry_ReportsUnknownFkInfo()
    {
        var specs = new GeoSpecSetFixture { Countries = [Country("XA", primaryLanguage: "fil")] };

        var diagnostic = Run(specs).Should().ContainSingle(d => d.Id == _UNKNOWN_FK).Which;

        diagnostic.Severity.Should().Be(DiagnosticSeverity.Info);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "Geo spec field 'primaryLanguageISO6391Code' on entity 'XA' references unknown code "
            + "'fil' (no matching entry in target catalog)");
    }

    [Fact]
    public void Generate_PrimaryLanguageWithNoLanguageEntry_EmitsNullPrimaryLanguage()
    {
        var specs = new GeoSpecSetFixture { Countries = [Country("XA", primaryLanguage: "fil")] };

        var source = GeneratedSource(specs, "CountryLookup.g.cs");

        source.Should().Contain("PrimaryLanguageIso6391Code = null,");
        source.Should().NotContain("rec.PrimaryLanguage =");
    }

    [Fact]
    public void Generate_PrimaryLanguageWithLanguageEntry_ReportsNoUnknownFk()
    {
        var specs = new GeoSpecSetFixture
        {
            Countries = [Country("XA", primaryLanguage: "en")],
            Languages = [Language("en")],
        };

        Run(specs).Should().NotContain(d => d.Id == _UNKNOWN_FK);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Generate_PrimaryLanguageAbsentOrBlank_ReportsNoUnknownFk(string? primaryLanguage)
    {
        var specs = new GeoSpecSetFixture
        {
            Countries = [Country("XA", primaryLanguage: primaryLanguage)],
        };

        Run(specs).Should().NotContain(d => d.Id == _UNKNOWN_FK);
    }

    [Fact]
    public void Generate_PrimaryCurrencyWithNoCurrencyEntry_ReportsUnknownFkInfo()
    {
        var specs = new GeoSpecSetFixture { Countries = [Country("XA", primaryCurrency: "ZZZ")] };

        var diagnostic = Run(specs).Should().ContainSingle(d => d.Id == _UNKNOWN_FK).Which;

        diagnostic.Severity.Should().Be(DiagnosticSeverity.Info);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "Geo spec field 'primaryCurrencyISO4217AlphaCode' on entity 'XA' references unknown "
            + "code 'ZZZ' (no matching entry in target catalog)");
    }

    [Fact]
    public void Generate_PrimaryCurrencyWithCurrencyEntry_ReportsNoUnknownFk()
    {
        var specs = new GeoSpecSetFixture
        {
            Countries = [Country("XA", primaryCurrency: "USD")],
            Currencies = [Currency("USD")],
        };

        Run(specs).Should().NotContain(d => d.Id == _UNKNOWN_FK);
    }

    [Fact]
    public void Generate_AcceptedCurrencyWithNoCurrencyEntry_ReportsUnknownFkOnce()
    {
        var specs = new GeoSpecSetFixture
        {
            Countries = [Country("XA", acceptedCurrencies: ["USD", "ZZZ"])],
            Currencies = [Currency("USD")],
        };

        Run(specs).Where(d => d.Id == _UNKNOWN_FK)
            .Select(d => d.GetMessage(CultureInfo.InvariantCulture))
            .Should().Equal(
                "Geo spec field 'currencies' on entity 'XA' references unknown code 'ZZZ' "
                + "(no matching entry in target catalog)");
    }

    [Fact]
    public void Generate_CountryGeopoliticalEntityWithNoEntry_ReportsUnknownFkOnce()
    {
        var specs = new GeoSpecSetFixture
        {
            Countries = [Country("XA", geopoliticalEntities: ["EU", "ZZ"])],
            GeopoliticalEntities = [GeopoliticalEntity("EU", members: ["XA"])],
        };

        Run(specs).Where(d => d.Id == _UNKNOWN_FK)
            .Select(d => d.GetMessage(CultureInfo.InvariantCulture))
            .Should().Equal(
                "Geo spec field 'geopoliticalEntityShortCodes' on entity 'XA' references unknown "
                + "code 'ZZ' (no matching entry in target catalog)");
    }

    [Fact]
    public void Generate_GeopoliticalEntityMemberWithNoCountryEntry_ReportsUnknownFkInfo()
    {
        var specs = new GeoSpecSetFixture
        {
            Countries = [Country("XA")],
            GeopoliticalEntities = [GeopoliticalEntity("EU", members: ["XA", "XK"])],
        };

        var diagnostic = Run(specs).Should().ContainSingle(d => d.Id == _UNKNOWN_FK).Which;

        diagnostic.Severity.Should().Be(DiagnosticSeverity.Info);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "Geo spec field 'countryISO31661Alpha2Codes' on entity 'EU' references unknown code "
            + "'XK' (no matching entry in target catalog)");
    }

    [Fact]
    public void Generate_TimezoneSegmentWithHyphen_EmitsUnderscoreMember()
    {
        var specs = new GeoSpecSetFixture
        {
            Timezones = [Timezone("America/Port-au-Prince"), Timezone("Asia/Ust-Nera")],
        };

        var source = GeneratedSource(specs, "TimezonesNested.g.cs");

        source.Should().Contain(
            "public static readonly TimezoneCode Port_au_Prince = "
            + "TimezoneCode.FromString(\"America/Port-au-Prince\");");
        source.Should().Contain(
            "public static readonly TimezoneCode Ust_Nera = "
            + "TimezoneCode.FromString(\"Asia/Ust-Nera\");");
        Run(specs).Should().NotContain(d => d.Id == _INVALID_IDENTIFIER);
    }

    [Fact]
    public void Generate_TimezoneParentSegmentWithHyphen_EmitsUnderscoreClass()
    {
        var specs = new GeoSpecSetFixture
        {
            Timezones = [Timezone("Bad-Area/One"), Timezone("Bad-Area/Two")],
        };

        var source = GeneratedSource(specs, "TimezonesNested.g.cs");

        source.Should().Contain("public static class Bad_Area");
        source.Should().Contain("TimezoneCode.FromString(\"Bad-Area/One\");");
        source.Should().Contain("TimezoneCode.FromString(\"Bad-Area/Two\");");
        Run(specs).Should().NotContain(d => d.Id == _INVALID_IDENTIFIER);
    }

    [Theory]
    [InlineData("America/Port.au", "America/Port.au")]
    [InlineData("America/Port au", "America/Port au")]
    [InlineData("Area//One", "Area/")]
    public void Generate_TimezoneSegmentThatDoesNotConvert_ReportsInvalidIdentifierInfo(
        string iana,
        string reportedPath)
    {
        var specs = new GeoSpecSetFixture { Timezones = [Timezone(iana)] };

        var diagnostic = Run(specs).Should().ContainSingle(d => d.Id == _INVALID_IDENTIFIER).Which;

        diagnostic.Severity.Should().Be(DiagnosticSeverity.Info);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            $"Geo code '{reportedPath}' cannot be sanitized into a valid C# identifier; "
            + "emission target 'Timezones' skipped");
    }

    [Fact]
    public void Generate_TimezoneParentSegmentThatDoesNotConvert_ReportsTheParentPathOnce()
    {
        var specs = new GeoSpecSetFixture
        {
            Timezones = [Timezone("Bad.Area/One"), Timezone("Bad.Area/Two")],
        };

        Run(specs).Where(d => d.Id == _INVALID_IDENTIFIER)
            .Select(d => d.GetMessage(CultureInfo.InvariantCulture))
            .Should().Equal(
                "Geo code 'Bad.Area' cannot be sanitized into a valid C# identifier; "
                + "emission target 'Timezones' skipped");
        GeneratedSource(specs, "TimezonesNested.g.cs").Should().NotContain("Bad");
    }

    [Fact]
    public void Generate_TwoSiblingSegmentsWithTheSameMemberName_EmitsOneAndReportsTheOther()
    {
        var specs = new GeoSpecSetFixture
        {
            Timezones = [Timezone("Area/A_B"), Timezone("Area/A-B")],
        };

        var source = GeneratedSource(specs, "TimezonesNested.g.cs");

        source.Split("public static readonly TimezoneCode A_B =").Should().HaveCount(2);
        source.Should().Contain("TimezoneCode.FromString(\"Area/A-B\");");
        source.Should().NotContain("TimezoneCode.FromString(\"Area/A_B\");");
        Run(specs).Where(d => d.Id == _INVALID_IDENTIFIER)
            .Select(d => d.GetMessage(CultureInfo.InvariantCulture))
            .Should().Equal(
                "Geo code 'Area/A_B' cannot be sanitized into a valid C# identifier; "
                + "emission target 'Timezones' skipped");
    }

    [Fact]
    public void Generate_TwoTopLevelSegmentsWithTheSameMemberName_EmitsOneAndReportsTheOther()
    {
        var specs = new GeoSpecSetFixture
        {
            Timezones = [Timezone("A_B/Zone"), Timezone("A-B/Zone")],
        };

        var source = GeneratedSource(specs, "TimezonesNested.g.cs");

        source.Split("public static class A_B").Should().HaveCount(2);
        source.Should().Contain("TimezoneCode.FromString(\"A-B/Zone\");");
        source.Should().NotContain("TimezoneCode.FromString(\"A_B/Zone\");");
        Run(specs).Where(d => d.Id == _INVALID_IDENTIFIER)
            .Select(d => d.GetMessage(CultureInfo.InvariantCulture))
            .Should().Equal(
                "Geo code 'A_B' cannot be sanitized into a valid C# identifier; "
                + "emission target 'Timezones' skipped");
    }

    [Fact]
    public void Generate_SameSegmentUnderTwoParents_EmitsBothMembers()
    {
        var specs = new GeoSpecSetFixture
        {
            Timezones = [Timezone("One/A-B"), Timezone("Two/A-B")],
        };

        var source = GeneratedSource(specs, "TimezonesNested.g.cs");

        source.Should().Contain("TimezoneCode.FromString(\"One/A-B\");");
        source.Should().Contain("TimezoneCode.FromString(\"Two/A-B\");");
        Run(specs).Should().NotContain(d => d.Id == _INVALID_IDENTIFIER);
    }

    [Theory]
    [InlineData("America/New_York")]
    [InlineData("Area/10")]
    public void Generate_TimezoneSegmentsThatConvert_ReportNoInvalidIdentifier(string iana)
    {
        var specs = new GeoSpecSetFixture { Timezones = [Timezone(iana)] };

        Run(specs).Should().NotContain(d => d.Id == _INVALID_IDENTIFIER);
    }

    [Fact]
    public void Generate_CurrencyCodeThatIsNotAnIdentifier_ReportsInvalidIdentifierInfo()
    {
        var specs = new GeoSpecSetFixture
        {
            Countries = [Country("XA")],
            Currencies = [Currency("U-D")],
        };

        var diagnostic = Run(specs).Should().ContainSingle(d => d.Id == _INVALID_IDENTIFIER).Which;

        diagnostic.Severity.Should().Be(DiagnosticSeverity.Info);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "Geo code 'U-D' cannot be sanitized into a valid C# identifier; "
            + "emission target 'CurrencyCode' skipped");
    }

    [Fact]
    public void Generate_GeopoliticalEntityShortCodeThatIsNotAnIdentifier_ReportsInvalidIdentifier()
    {
        var specs = new GeoSpecSetFixture
        {
            Countries = [Country("XA")],
            GeopoliticalEntities = [GeopoliticalEntity("E-U", members: [])],
        };

        Run(specs).Where(d => d.Id == _INVALID_IDENTIFIER)
            .Select(d => d.GetMessage(CultureInfo.InvariantCulture))
            .Should().Equal(
                "Geo code 'E-U' cannot be sanitized into a valid C# identifier; "
                + "emission target 'GeopoliticalEntityCode' skipped");
    }

    [Fact]
    public void Generate_AbstractionsTarget_ReportsNeitherDataGapDiagnostic()
    {
        var specs = new GeoSpecSetFixture
        {
            Countries = [Country("XA", primaryLanguage: "fil")],
            Timezones = [Timezone("America/Port.au")],
        };

        Run(specs, _ABSTRACTIONS_ASSEMBLY).Should()
            .NotContain(d => d.Id == _UNKNOWN_FK || d.Id == _INVALID_IDENTIFIER);
    }

    [Fact]
    public void Generate_RepositoryGeoSpecs_ReportsNoDataGap()
    {
        var geoDirectory = Path.Combine(RepositoryRoot(), "public", "contracts", "geo");
        var texts = Directory.GetFiles(geoDirectory, "*.spec.json")
            .Select(path => (AdditionalText)new InMemoryAdditionalTextFixture(
                path, File.ReadAllText(path)))
            .ToImmutableArray();

        var messages = RunTexts(texts, _DEFAULT_ASSEMBLY)
            .Where(d => d.Id == _UNKNOWN_FK || d.Id == _INVALID_IDENTIFIER)
            .Select(d => d.GetMessage(CultureInfo.InvariantCulture));

        messages.Should().BeEmpty();
    }

    private static ImmutableArray<Diagnostic> Run(
        GeoSpecSetFixture specs,
        string assemblyName = _DEFAULT_ASSEMBLY)
        => RunTexts(specs.ToAdditionalTexts(), assemblyName);

    private static ImmutableArray<Diagnostic> RunTexts(
        ImmutableArray<AdditionalText> texts,
        string assemblyName)
        => Drive(texts, assemblyName).GetRunResult().Diagnostics;

    private static string GeneratedSource(GeoSpecSetFixture specs, string hintName)
        => Drive(specs.ToAdditionalTexts(), _DEFAULT_ASSEMBLY).GetRunResult().GeneratedTrees
            .Single(t => Path.GetFileName(t.FilePath) == hintName)
            .ToString();

    private static GeneratorDriver Drive(ImmutableArray<AdditionalText> texts, string assemblyName)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [],
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(
            generators: [new GeoGenerator().AsSourceGenerator()],
            additionalTexts: texts,
            optionsProvider: FixtureAnalyzerConfigOptionsProvider.FromBuildProperties(
                new Dictionary<string, string>()));

        return driver.RunGenerators(compilation);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "D2.slnx")))
            directory = directory.Parent;

        directory.Should().NotBeNull("the test runs inside the repository, which has D2.slnx at its root");
        return directory.FullName;
    }

    private static string Country(
        string alpha2,
        string? primaryLanguage = null,
        string? primaryCurrency = null,
        string[]? acceptedCurrencies = null,
        string[]? geopoliticalEntities = null)
    {
        var currencies = string.Join(
            ",",
            (acceptedCurrencies ?? []).Select(
                c => $$"""{"iso4217AlphaCode":"{{c}}","level":"LegalTender"}"""));
        var gpes = string.Join(",", (geopoliticalEntities ?? []).Select(g => $"\"{g}\""));

        return $$"""
            {
              "iso31661Alpha2Code": "{{alpha2}}",
              "iso31661Alpha3Code": "{{alpha2}}X",
              "iso31661NumericCode": "999",
              "displayName": "Country {{alpha2}}",
              "officialName": "Country {{alpha2}}",
              "firstDayOfWeek": "Monday",
              "weekendStart": "Saturday",
              "weekendEnd": "Sunday",
              "measurementSystem": "Metric",
              "primaryLanguageISO6391Code": {{JsonString(primaryLanguage)}},
              "primaryCurrencyISO4217AlphaCode": {{JsonString(primaryCurrency)}},
              "geopoliticalEntityShortCodes": [{{gpes}}],
              "currencies": [{{currencies}}]
            }
            """;
    }

    private static string Language(string code) =>
        $$"""{"iso6391Code":"{{code}}","name":"Language {{code}}","writingDirection":"LTR"}""";

    private static string Currency(string code) =>
        $$"""{"iso4217AlphaCode":"{{code}}","displayName":"Currency {{code}}"}""";

    private static string GeopoliticalEntity(string shortCode, string[] members) =>
        $$"""
        {"shortCode":"{{shortCode}}","name":"Entity {{shortCode}}","type":"Continent",
         "countryISO31661Alpha2Codes":[{{string.Join(",", members.Select(m => $"\"{m}\""))}}]}
        """;

    private static string Timezone(string iana) =>
        $$"""{"ianaIdentifier":"{{iana}}","displayName":"{{iana}}","currentStdOffsetMinutes":0}""";

    private static string JsonString(string? value) => value is null ? "null" : $"\"{value}\"";
}
