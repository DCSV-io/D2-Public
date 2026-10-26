// -----------------------------------------------------------------------
// <copyright file="GeneratedSourceTreeTestKit.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.SourceGen.Fixtures;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Runs a source generator over a given spec and compares what it produces with
/// a committed expected file as parsed syntax trees rather than as raw text.
/// </summary>
/// <remarks>
/// Two trees match when they are structurally equivalent and when their text,
/// re-laid out by the parser's own whitespace normalization, is equal. The second
/// check keeps comments and documentation comments in the comparison, which
/// structural equivalence ignores, while line endings and indentation stop
/// mattering. A mismatch reports the first line of the normalized text that
/// differs, from both sides, so the failure message contains the drifted value.
/// </remarks>
internal static class GeneratedSourceTreeTestKit
{
    /// <summary>
    /// Asserts that the regenerated source and the committed source parse to the
    /// same tree.
    /// </summary>
    /// <param name="regenerated">The source the generator produced.</param>
    /// <param name="committed">The committed expected source.</param>
    /// <param name="because">Why the two must match, for the failure message.</param>
    public static void ShouldMatchCommittedTree(
        string regenerated,
        string committed,
        string because)
    {
        var regeneratedTree = CSharpSyntaxTree.ParseText(regenerated);
        var committedTree = CSharpSyntaxTree.ParseText(committed);

        var regeneratedLines = Canonical(regeneratedTree).Split('\n');
        var committedLines = Canonical(committedTree).Split('\n');

        FirstDifference(regeneratedLines, committedLines).Should().BeNull(because);
        regeneratedTree.IsEquivalentTo(committedTree, topLevel: false)
            .Should().BeTrue(because);
    }

    /// <summary>
    /// Runs one generator over an in-memory compilation and returns what it
    /// produced, keyed by generated file name.
    /// </summary>
    /// <param name="generator">The generator under test.</param>
    /// <param name="assemblyName">The assembly name the compilation carries.</param>
    /// <param name="additionalFiles">The additional files, as path and content.</param>
    /// <param name="sources">Source files the compilation carries, if the generator reads any.</param>
    /// <returns>Every generated file's text, keyed by file name.</returns>
    public static IReadOnlyDictionary<string, string> RunGenerator(
        IIncrementalGenerator generator,
        string assemblyName,
        IEnumerable<(string Path, string Content)> additionalFiles,
        IEnumerable<string>? sources = null)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: (sources ?? []).Select(s => CSharpSyntaxTree.ParseText(s)),
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            additionalTexts: additionalFiles
                .Select(f => (AdditionalText)new InMemoryAdditionalTextFixture(f.Path, f.Content))
                .ToImmutableArray());

        var result = driver.RunGenerators(compilation).GetRunResult();

        result.Diagnostics.Should().BeEmpty("a clean spec produces no generator diagnostics");

        return result.GeneratedTrees.ToDictionary(
            t => Path.GetFileName(t.FilePath),
            t => t.GetText().ToString());
    }

    private static string Canonical(SyntaxTree tree)
        => tree.GetRoot().NormalizeWhitespace(eol: "\n").ToFullString();

    private static string? FirstDifference(string[] regenerated, string[] committed)
    {
        var shared = Math.Min(regenerated.Length, committed.Length);

        for (var i = 0; i < shared; i++)
        {
            if (!string.Equals(regenerated[i], committed[i], StringComparison.Ordinal))
            {
                return $"line {i + 1}: regenerated '{regenerated[i].Trim()}', "
                    + $"committed '{committed[i].Trim()}'";
            }
        }

        if (regenerated.Length == committed.Length)
            return null;

        return regenerated.Length > committed.Length
            ? $"line {shared + 1}: regenerated '{regenerated[shared].Trim()}', committed ends"
            : $"line {shared + 1}: committed '{committed[shared].Trim()}', regenerated ends";
    }
}
