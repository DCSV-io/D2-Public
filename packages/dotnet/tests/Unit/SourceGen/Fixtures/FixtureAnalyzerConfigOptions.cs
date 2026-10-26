// -----------------------------------------------------------------------
// <copyright file="FixtureAnalyzerConfigOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.SourceGen.Fixtures;

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// <see cref="AnalyzerConfigOptions"/> over a fixed dictionary of keys and values.
/// </summary>
/// <param name="values">The option keys and values.</param>
internal sealed class FixtureAnalyzerConfigOptions(
    IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
{
    /// <inheritdoc />
    public override bool TryGetValue(string key, out string value) =>
        values.TryGetValue(key, out value!);
}
