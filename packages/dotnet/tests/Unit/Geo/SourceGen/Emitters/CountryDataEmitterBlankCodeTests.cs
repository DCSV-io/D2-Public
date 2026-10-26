// -----------------------------------------------------------------------
// <copyright file="CountryDataEmitterBlankCodeTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Geo.SourceGen.Emitters;

using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Geo.SourceGen.Emitters.Default;
using DcsvIo.D2.Geo.SourceGen.Spec;
using Xunit;

/// <summary>
/// <see cref="CountryDataEmitter"/> called directly with a currency alpha code or a
/// geopolitical entity short code that is blank.
/// </summary>
public sealed class CountryDataEmitterBlankCodeTests
{
    private const string _INVALID_IDENTIFIER = "D2GEO004";

    private static readonly SpecMetadata sr_metadata = new(
        CatalogVersion: "0.0.1-test",
        GeneratedAt: "2026-01-01T00:00:00Z",
        LastEditedAt: null,
        IsGenerated: true,
        Source: "synthetic-test");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmitAll_CurrencyWithBlankAlphaCode_ReportsNoInvalidIdentifier(string alphaCode)
    {
        var context = BuildContext(
            currencies: new SpecEnvelope<CurrencySpec>(
                sr_metadata,
                [Currency(alphaCode), Currency("USD")]));

        var results = CountryDataEmitter.EmitAll(context);

        results.Single().Diagnostics.Should()
            .NotContain(d => d.DescriptorId == _INVALID_IDENTIFIER);
    }

    [Fact]
    public void EmitAll_CurrencyWithAlphaCodeThatIsNotAnIdentifier_ReportsInvalidIdentifier()
    {
        var context = BuildContext(
            currencies: new SpecEnvelope<CurrencySpec>(sr_metadata, [Currency("U-D")]));

        var results = CountryDataEmitter.EmitAll(context);

        results.Single().Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == _INVALID_IDENTIFIER)
            .Which.Args.Should().Equal("U-D", "CurrencyCode");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmitAll_GeopoliticalEntityWithBlankShortCode_ReportsNoInvalidIdentifier(
        string shortCode)
    {
        var context = BuildContext(
            geopoliticalEntities: new SpecEnvelope<GeopoliticalEntitySpec>(
                sr_metadata,
                [GeopoliticalEntity(shortCode), GeopoliticalEntity("EU")]));

        var results = CountryDataEmitter.EmitAll(context);

        results.Single().Diagnostics.Should()
            .NotContain(d => d.DescriptorId == _INVALID_IDENTIFIER);
    }

    [Fact]
    public void EmitAll_GeopoliticalEntityWithShortCodeNotAnIdentifier_ReportsInvalidIdentifier()
    {
        var context = BuildContext(
            geopoliticalEntities: new SpecEnvelope<GeopoliticalEntitySpec>(
                sr_metadata,
                [GeopoliticalEntity("E-U")]));

        var results = CountryDataEmitter.EmitAll(context);

        results.Single().Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == _INVALID_IDENTIFIER)
            .Which.Args.Should().Equal("E-U", "GeopoliticalEntityCode");
    }

    private static GeoSpecContext BuildContext(
        SpecEnvelope<CurrencySpec>? currencies = null,
        SpecEnvelope<GeopoliticalEntitySpec>? geopoliticalEntities = null) =>
        new(
            Countries: new SpecEnvelope<CountrySpec>(sr_metadata, []),
            Subdivisions: null,
            Currencies: currencies,
            Languages: null,
            Locales: null,
            Timezones: null,
            GeopoliticalEntities: geopoliticalEntities);

    private static CurrencySpec Currency(string alphaCode) =>
        new(
            Iso4217AlphaCode: alphaCode,
            Iso4217NumericCode: "000",
            DisplayName: alphaCode + " Display",
            DecimalPlaces: 2,
            Symbol: "$",
            IsActive: true,
            IsSupported: true);

    private static GeopoliticalEntitySpec GeopoliticalEntity(string shortCode) =>
        new(
            ShortCode: shortCode,
            Name: shortCode + " Name",
            Type: "Continent",
            CountryIso31661Alpha2Codes: []);
}
