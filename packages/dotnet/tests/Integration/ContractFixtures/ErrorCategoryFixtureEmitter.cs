// -----------------------------------------------------------------------
// <copyright file="ErrorCategoryFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using DcsvIo.D2.ErrorCodes.Category;
using Xunit;

/// <summary>
/// Emits the cross-runtime parity fixture for the
/// <see cref="ErrorCategory"/> enum. Reflects every enum member, maps it to its
/// snake_case wire string via <see cref="ErrorCategoryWire.ToWire"/>, and writes
/// a deterministic <c>mapping.json</c> fixture to
/// <c>fixtures/error-category/</c> in the fixture directory that the TypeScript
/// contract tests read. The fixture is the
/// PascalCase-member -> wire-string map of the enum, which the TypeScript
/// <c>ErrorCategory</c> union exposes identically.
/// </summary>
public sealed class ErrorCategoryFixtureEmitter
{
    private const string _CATALOG = "error-category";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Mapping()
    {
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var category in Enum.GetValues<ErrorCategory>())
            data[category.ToString()] = category.ToWire();

        FixturePathHelpers.WriteFixture(_CATALOG, "mapping", data);
    }
}
