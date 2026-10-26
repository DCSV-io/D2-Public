// -----------------------------------------------------------------------
// <copyright file="InMemoryAdditionalTextFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.SourceGen.Fixtures;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// An additional file held in memory, handed to a generator driver in place of
/// a file on disk.
/// </summary>
/// <param name="path">The path the generator sees.</param>
/// <param name="content">The file's text.</param>
internal sealed class InMemoryAdditionalTextFixture(string path, string content) : AdditionalText
{
    private readonly SourceText r_text = SourceText.From(content);

    /// <inheritdoc />
    public override string Path { get; } = path;

    /// <inheritdoc />
    public override SourceText GetText(CancellationToken cancellationToken = default) => r_text;
}
