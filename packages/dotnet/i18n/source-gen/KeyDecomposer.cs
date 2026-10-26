// -----------------------------------------------------------------------
// <copyright file="KeyDecomposer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.I18n.SourceGen;

using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Pure logic for decomposing a translation key (e.g. <c>"common_errors_NOT_FOUND"</c>)
/// into a TK constant path: a domain segment (e.g. <c>"Common"</c>), a category
/// segment (e.g. <c>"Errors"</c>), and a constant name (e.g. <c>"NOT_FOUND"</c>).
/// </summary>
/// <remarks>
/// <para>
/// This class is stateless and has no dependency on the Roslyn host.
/// <see cref="TKGenerator"/> is the Roslyn-host integration.
/// </para>
/// <para>
/// Decomposition rules:
/// <list type="bullet">
/// <item>Split the key by <c>'_'</c>.</item>
/// <item>Require at least 3 non-empty segments.</item>
/// <item>Segment 0 -> domain (PascalCase first letter).</item>
/// <item>Segment 1 -> category (PascalCase first letter).</item>
/// <item>Segments 2..N joined by <c>'_'</c> and uppercased -> constant name.</item>
/// <item>The constant name MUST be a valid C# identifier (start with letter or
/// underscore; rest letters/digits/underscores; ASCII only for predictability).</item>
/// </list>
/// </para>
/// </remarks>
internal static class KeyDecomposer
{
    private const int _MIN_SEGMENTS = 3;

    /// <summary>
    /// Decomposes a translation key into its TK path components.
    /// </summary>
    /// <param name="key">The translation key (e.g. <c>"common_errors_NOT_FOUND"</c>).</param>
    /// <returns>
    /// A <see cref="DecomposedKey"/> with <c>IsValid=true</c> on success or
    /// <c>IsValid=false</c> with an explanatory <c>InvalidReason</c> otherwise.
    /// </returns>
    public static DecomposedKey Decompose(string? key)
    {
        if (key.Falsey())
        {
            return DecomposedKey.Invalid(string.Empty, "key is null or empty");
        }

        var segments = key!.Split('_');
        if (segments.Length < _MIN_SEGMENTS)
        {
            return DecomposedKey.Invalid(
                key,
                $"key must have at least {_MIN_SEGMENTS} underscore-separated segments");
        }

        for (var i = 0; i < segments.Length; i++)
        {
            if (segments[i].Length == 0)
            {
                return DecomposedKey.Invalid(
                    key,
                    $"segment at index {i} is empty (consecutive underscores " +
                        "or leading/trailing underscore)");
            }
        }

        var domain = ToPascalCase(segments[0]);
        var category = ToPascalCase(segments[1]);

        // Validate the domain and category as C# identifiers (start with a
        // letter, ASCII only).
        if (!IsValidCSharpIdentifier(domain))
        {
            return DecomposedKey.Invalid(
                key,
                $"domain segment '{segments[0]}' produces invalid C# identifier '{domain}'");
        }

        if (!IsValidCSharpIdentifier(category))
        {
            return DecomposedKey.Invalid(
                key,
                $"category segment '{segments[1]}' produces invalid C# identifier '{category}'");
        }

        // The constant name is everything after the domain and category
        // segments, uppercased.
        const int constantStartIndex = _MIN_SEGMENTS - 1;
        var constantSegmentCount = segments.Length - constantStartIndex;
        var constantName = string
            .Join("_", segments, constantStartIndex, constantSegmentCount)
            .ToUpperInvariant();

        // C# keywords are lower case and the constant name is upper case, so a valid
        // identifier here is never a keyword.
        if (!IsValidCSharpIdentifier(constantName))
        {
            return DecomposedKey.Invalid(
                key,
                $"constant name '{constantName}' is not a valid C# identifier");
        }

        return DecomposedKey.Valid(key, domain, category, constantName);
    }

    /// <summary>
    /// Uppercases the first character of <paramref name="segment"/> and leaves
    /// the rest unchanged. Domain and category segments are conventionally
    /// single lowercase words ("common", "auth", "geo"); this PascalCases them.
    /// </summary>
    private static string ToPascalCase(string segment)
    {
        if (segment.Length == 0 || char.IsUpper(segment[0]))
        {
            return segment;
        }

        return char.ToUpperInvariant(segment[0]) + segment.Substring(1);
    }

    /// <summary>
    /// Strict ASCII C# identifier check: first char letter or underscore,
    /// remaining chars letters / digits / underscores. Restricting to ASCII
    /// keeps emitted code predictable across encoding tooling.
    /// </summary>
    private static bool IsValidCSharpIdentifier(string identifier)
    {
        if (identifier.Falsey())
        {
            return false;
        }

        var first = identifier[0];
        if (!IsAsciiLetter(first) && first != '_')
        {
            return false;
        }

        for (var i = 1; i < identifier.Length; i++)
        {
            var c = identifier[i];
            if (!IsAsciiLetter(c) && !IsAsciiDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiLetter(char c) =>
        c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';
}
