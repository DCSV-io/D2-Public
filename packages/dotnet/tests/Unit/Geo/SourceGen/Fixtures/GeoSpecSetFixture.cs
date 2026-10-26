// -----------------------------------------------------------------------
// <copyright file="GeoSpecSetFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Geo.SourceGen.Fixtures;

using System.Collections.Immutable;
using DcsvIo.D2.Tests.Unit.SourceGen.Fixtures;
using Microsoft.CodeAnalysis;

/// <summary>
/// The geo catalog entries a test hands the geo source generator, one list of
/// JSON entry objects per spec file. Every spec file is present, so the
/// generator reports no missing-spec diagnostic.
/// </summary>
internal sealed class GeoSpecSetFixture
{
    public string[] Countries { get; init; } = [];

    public string[] Currencies { get; init; } = [];

    public string[] Languages { get; init; } = [];

    public string[] Timezones { get; init; } = [];

    public string[] GeopoliticalEntities { get; init; } = [];

    /// <summary>Builds one additional file per geo spec, under a <c>geo</c> folder.</summary>
    /// <returns>The additional files.</returns>
    public ImmutableArray<AdditionalText> ToAdditionalTexts() =>
    [
        CatalogFile("countries.spec.json", Countries),
        CatalogFile("subdivisions.spec.json", []),
        CatalogFile("currencies.spec.json", Currencies),
        CatalogFile("languages.spec.json", Languages),
        CatalogFile("locales.spec.json", []),
        CatalogFile("timezones.spec.json", Timezones),
        CatalogFile("geopolitical-entities.spec.json", GeopoliticalEntities),
    ];

    private static InMemoryAdditionalTextFixture CatalogFile(string name, string[] entries) =>
        new(
            $"contracts/geo/{name}",
            $$"""
            {"$generated":true,"catalogVersion":"0.1.0","generatedAt":"2026-01-01T00:00:00Z",
             "entries":[{{string.Join(",", entries)}}]}
            """);
}
