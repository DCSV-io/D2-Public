// -----------------------------------------------------------------------
// <copyright file="SourceTextExt.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// Shared source-gen text helper. Every source-gen csproj includes it from
// `source-gen-shared/core/` with the shared `Compile Include`, so every emitter
// applies the same LF normalization.
// -----------------------------------------------------------------------

namespace DcsvIo.D2.SourceGen;

/// <summary>
/// Source-text helpers shared by every D2 Roslyn emitter.
/// </summary>
internal static class SourceTextExt
{
    /// <summary>
    /// Normalizes every CRLF in a built source string to a bare LF so the
    /// emitted <c>.g.cs</c> is byte-identical regardless of the build host's
    /// OS. <see cref="System.Text.StringBuilder.AppendLine()"/> appends
    /// <see cref="System.Environment.NewLine"/> (CRLF on Windows, LF on
    /// Linux); the repo policy is LF everywhere
    /// (<c>.gitattributes</c> <c>eol=lf</c>, <c>.editorconfig</c>
    /// <c>end_of_line = lf</c>), and the committed <c>EmitCompilerGeneratedFiles</c>
    /// output is LF. Passing every emitter's returned source through this
    /// method stops a Windows rebuild from emitting the generated files with
    /// CRLF, which would show as changes between the working tree and the
    /// index. The method calls the two-argument
    /// <see cref="string.Replace(string, string)"/> because netstandard2.0 has
    /// no overload that takes a <c>StringComparison</c>.
    /// </summary>
    /// <param name="source">The built source string (may contain CRLF).</param>
    /// <returns>The same source with every CRLF collapsed to a bare LF.</returns>
    public static string LfNormalized(this string source) =>
        source.Replace("\r\n", "\n");
}
