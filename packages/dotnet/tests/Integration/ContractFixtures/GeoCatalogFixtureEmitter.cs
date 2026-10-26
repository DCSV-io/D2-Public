// -----------------------------------------------------------------------
// <copyright file="GeoCatalogFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Globalization;
using DcsvIo.D2.Geo.Abstractions;
using Xunit;

/// <summary>
/// Emits the parity fixture for the codegen-emitted
/// <see cref="GeoCatalog"/> provenance constants
/// (<see cref="GeoCatalog.CatalogVersion"/> +
/// <see cref="GeoCatalog.CatalogPublishedAt"/>). Both runtimes snapshot the
/// same spec metadata, so a difference between them means one runtime's
/// catalog was regenerated without the other.
/// </summary>
/// <remarks>
/// The timestamp is serialized as an ISO-8601 UTC string with millisecond
/// precision in the format <c>yyyy-MM-ddTHH:mm:ss.fffZ</c>, the form of the
/// TypeScript <c>CATALOG_PUBLISHED_AT</c> string. The emitter writes the
/// fixture to <c>fixtures/geo/catalog.json</c>.
/// </remarks>
public sealed class GeoCatalogFixtureEmitter
{
    private const string _CATALOG = "geo";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Catalog()
    {
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["catalogVersion"] = GeoCatalog.CatalogVersion,
            ["catalogPublishedAt"] = GeoCatalog.CatalogPublishedAt
                .ToUniversalTime()
                .ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
        };

        FixturePathHelpers.WriteFixture(_CATALOG, "catalog", data);
    }
}
