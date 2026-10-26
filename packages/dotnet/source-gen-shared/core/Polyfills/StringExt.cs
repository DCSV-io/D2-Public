// -----------------------------------------------------------------------
// <copyright file="StringExt.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// Instance-style `this string?` extension is required because source generators
// target netstandard2.0 and cannot reference DcsvIo.D2.Utilities (net10).
// This polyfill reproduces Falsey/Truthy semantics locally.
// -----------------------------------------------------------------------

namespace DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// netstandard2.0 polyfill of
/// <c>DcsvIo.D2.Utilities.Extensions.StringExtensions.Falsey()</c> /
/// <c>Truthy()</c>. Source generators cannot reference
/// <c>DcsvIo.D2.Utilities</c> (which targets <c>net10</c>) because Roslyn
/// analyzer hosts require <c>netstandard2.0</c>. This polyfill lets generator
/// code call <c>Falsey</c> and <c>Truthy</c>, as the repository's rules
/// require. Each source-gen csproj includes it from
/// <c>source-gen-shared/</c> with the shared <c>Compile Include</c>, so every
/// generator uses the same Falsey/Truthy semantics.
/// </summary>
internal static class StringExt
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="value"/> is null, empty, or
    /// whitespace-only - matching the real
    /// <c>DcsvIo.D2.Utilities.Extensions.StringExtensions.Falsey(string?)</c>
    /// semantics.
    /// </summary>
    /// <param name="value">The string to test, or <c>null</c>.</param>
    /// <returns><c>true</c> when null, empty, or whitespace-only; otherwise <c>false</c>.</returns>
    public static bool Falsey(this string? value)
    {
        if (value is null) return true;

        for (var i = 0; i < value.Length; i++)
            if (!char.IsWhiteSpace(value[i])) return false;

        return true;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="value"/> is non-null and
    /// contains at least one non-whitespace character - the inverse of
    /// <see cref="Falsey"/>.
    /// </summary>
    /// <param name="value">The string to test, or <c>null</c>.</param>
    /// <returns>
    /// <c>true</c> when non-null and not whitespace-only; otherwise <c>false</c>.
    /// </returns>
    public static bool Truthy(this string? value) => !value.Falsey();
}
