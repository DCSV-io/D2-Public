// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.I18n.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the IDs declared in
/// <see cref="DiagnosticIds"/>. This class loads only inside the Roslyn host,
/// where <see cref="TKGenerator.Initialize"/> runs. Code that needs only the
/// ID strings uses the <see cref="DiagnosticIds"/> constants, which do not
/// load <c>Microsoft.CodeAnalysis</c> at runtime.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.InvalidTranslationKey"/>
    public static readonly DiagnosticDescriptor InvalidTranslationKey = new(
        id: DiagnosticIds.InvalidTranslationKey,
        title: "Translation key cannot be decomposed into TK constant",
        messageFormat: "Translation key '{0}' is invalid: {1}; the key was skipped",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MissingKeyInLocale"/>
    public static readonly DiagnosticDescriptor MissingKeyInLocale = new(
        id: DiagnosticIds.MissingKeyInLocale,
        title: "Translation key missing in locale",
        messageFormat: "Translation key '{0}' is present in en-US but missing in locale '{1}'",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.TranslationKeyCollision"/>
    public static readonly DiagnosticDescriptor TranslationKeyCollision = new(
        id: DiagnosticIds.TranslationKeyCollision,
        title: "Translation key collision after decomposition",
        messageFormat:
            "Translation keys '{0}' and '{1}' both decompose to '{2}'; the latter was skipped",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.OrphanKeyInLocale"/>
    public static readonly DiagnosticDescriptor OrphanKeyInLocale = new(
        id: DiagnosticIds.OrphanKeyInLocale,
        title: "Orphan translation key in locale",
        messageFormat:
            "Translation key '{0}' exists in locale '{1}' but is missing from en-US " +
            "(the source of truth); the key is not represented in TK",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MissingEnUsJson"/>
    public static readonly DiagnosticDescriptor MissingEnUsJson = new(
        id: DiagnosticIds.MissingEnUsJson,
        title: "Required en-US.json not found in AdditionalFiles",
        messageFormat:
            "The TK source generator could not locate 'en-US.json' among AdditionalFiles; " +
            "verify the consuming csproj declares the contracts/messages/*.json glob",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MalformedJsonCatalog"/>
    public static readonly DiagnosticDescriptor MalformedJsonCatalog = new(
        id: DiagnosticIds.MalformedJsonCatalog,
        title: "Translation catalog JSON is malformed",
        messageFormat: "Translation catalog '{0}' could not be parsed: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidExtensionNamespace"/>
    public static readonly DiagnosticDescriptor SR_InvalidExtensionNamespace = new(
        id: DiagnosticIds.InvalidExtensionNamespace,
        title: "Extension namespace property is not a valid namespace",
        messageFormat:
            "MSBuild property '{0}' is '{1}', which is not a dot-separated sequence of C# "
            + "identifiers; no extension output is generated",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidExtensionClassPrefix"/>
    public static readonly DiagnosticDescriptor SR_InvalidExtensionClassPrefix = new(
        id: DiagnosticIds.InvalidExtensionClassPrefix,
        title: "Extension class prefix property is missing or invalid",
        messageFormat:
            "MSBuild property '{0}' is '{1}', which is not a C# identifier; it must be one "
            + "when '{2}' is set, and no extension output is generated",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.ExtensionClassPrefixWithoutNamespace"/>
    public static readonly DiagnosticDescriptor SR_ExtensionClassPrefixWithoutNamespace = new(
        id: DiagnosticIds.ExtensionClassPrefixWithoutNamespace,
        title: "Extension class prefix property is set without a namespace",
        messageFormat:
            "MSBuild property '{0}' is '{1}' but '{2}' is not set; no extension output is "
            + "generated",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.I18n.SourceGen";
}
