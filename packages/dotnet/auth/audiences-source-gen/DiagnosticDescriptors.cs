// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Audiences.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the IDs declared in
/// <see cref="DiagnosticIds"/>. Only loaded inside the Roslyn host (at the
/// <see cref="AudiencesGenerator.Initialize"/> call site of the generator). Code
/// that needs only the IDs uses the <see cref="DiagnosticIds"/> string constants
/// directly and so avoids pulling in <c>Microsoft.CodeAnalysis</c> at runtime.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "Audience spec is malformed",
        messageFormat: "Audience spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidAudienceName"/>
    public static readonly DiagnosticDescriptor InvalidAudienceName = new(
        id: DiagnosticIds.InvalidAudienceName,
        title: "Audience name violates identifier convention",
        messageFormat: "Audience name '{0}' is invalid: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateAudienceName"/>
    public static readonly DiagnosticDescriptor DuplicateAudienceName = new(
        id: DiagnosticIds.DuplicateAudienceName,
        title: "Duplicate audience name",
        messageFormat: "Audience name '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateAudienceUrl"/>
    public static readonly DiagnosticDescriptor DuplicateAudienceUrl = new(
        id: DiagnosticIds.DuplicateAudienceUrl,
        title: "Duplicate audience URL",
        messageFormat:
            "Audiences '{0}' and '{1}' both map to URL '{2}' - rename or remove one to "
            + "avoid silent JWT aud-claim aliasing",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidAudienceUrl"/>
    public static readonly DiagnosticDescriptor InvalidAudienceUrl = new(
        id: DiagnosticIds.InvalidAudienceUrl,
        title: "Audience URL is not a valid absolute URI",
        messageFormat:
            "Audience '{0}' URL '{1}' is not a parseable absolute URI - expected "
            + "scheme://host[/path] (for example https://orders.example.internal)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MissingSpecFile"/>
    public static readonly DiagnosticDescriptor MissingSpecFile = new(
        id: DiagnosticIds.MissingSpecFile,
        title: "Audience spec file not found",
        messageFormat:
            "The Audiences source generator could not locate 'audiences.spec.json' among "
            + "AdditionalFiles; verify the consuming csproj declares the "
            + "contracts/auth-audiences/audiences.spec.json glob",
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

    private const string _CATEGORY = "DcsvIo.D2.Auth.Audiences.SourceGen";
}
