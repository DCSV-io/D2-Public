// -----------------------------------------------------------------------
// <copyright file="FixtureAnalyzerConfigOptionsProvider.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.SourceGen.Fixtures;

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// An <see cref="AnalyzerConfigOptionsProvider"/> that returns the given
/// <c>build_property.*</c> keys, as MSBuild exposes the properties a project
/// declares with <c>CompilerVisibleProperty</c>. A test compilation has no
/// MSBuild project, so the fixture supplies the values.
/// </summary>
/// <param name="globalOptions">The option keys and values every lookup returns.</param>
internal sealed class FixtureAnalyzerConfigOptionsProvider(
    IReadOnlyDictionary<string, string> globalOptions) : AnalyzerConfigOptionsProvider
{
    private readonly AnalyzerConfigOptions r_global =
        new FixtureAnalyzerConfigOptions(globalOptions);

    /// <inheritdoc />
    public override AnalyzerConfigOptions GlobalOptions => r_global;

    /// <summary>
    /// Builds a provider whose global options hold each MSBuild property under
    /// its <c>build_property.</c> key.
    /// </summary>
    /// <param name="properties">The property names, without the prefix, and their values.</param>
    /// <returns>The provider.</returns>
    public static FixtureAnalyzerConfigOptionsProvider FromBuildProperties(
        IReadOnlyDictionary<string, string> properties)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (name, value) in properties)
            options["build_property." + name] = value;

        return new FixtureAnalyzerConfigOptionsProvider(options);
    }

    /// <inheritdoc />
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => r_global;

    /// <inheritdoc />
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => r_global;
}
