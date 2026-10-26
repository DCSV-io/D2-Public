// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the IDs declared
/// in <see cref="DiagnosticIds"/>. This class loads only inside the Roslyn
/// host, where <see cref="GeoGenerator.Initialize"/> runs. Code that needs
/// only the ID strings uses the <see cref="DiagnosticIds"/> constants, which
/// do not load <c>Microsoft.CodeAnalysis</c> at runtime.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "Geo spec file is malformed",
        messageFormat: "Geo spec '{0}' could not be parsed: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.UnknownFk"/>
    /// <remarks>
    /// The default severity is <see cref="DiagnosticSeverity.Info"/>. The
    /// emitter writes <c>null</c> or leaves out the reference and continues,
    /// so the build does not fail.
    /// </remarks>
    public static readonly DiagnosticDescriptor UnknownFk = new(
        id: DiagnosticIds.UnknownFk,
        title: "Unknown foreign-key reference in geo spec",
        messageFormat:
            "Geo spec field '{0}' on entity '{1}' references unknown code '{2}' "
            + "(no matching entry in target catalog)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidIdentifier"/>
    /// <remarks>
    /// The default severity is <see cref="DiagnosticSeverity.Info"/>. The
    /// emitter skips the member for that code and continues, so the build
    /// does not fail.
    /// </remarks>
    public static readonly DiagnosticDescriptor InvalidIdentifier = new(
        id: DiagnosticIds.InvalidIdentifier,
        title: "Geo code cannot form a valid C# identifier",
        messageFormat:
            "Geo code '{0}' cannot be sanitized into a valid C# identifier; "
            + "emission target '{1}' skipped",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MissingCatalogMetadata"/>
    public static readonly DiagnosticDescriptor MissingCatalogMetadata = new(
        id: DiagnosticIds.MissingCatalogMetadata,
        title: "Geo catalog metadata missing or invalid",
        messageFormat:
            "Geo spec '{0}' is missing or has invalid catalog metadata field '{1}' "
            + "(needed for GeoCatalog emission)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MissingSpec"/>
    public static readonly DiagnosticDescriptor MissingSpec = new(
        id: DiagnosticIds.MissingSpec,
        title: "Required geo spec file not found in AdditionalFiles",
        messageFormat:
            "Required geo spec '{0}' was not found among AdditionalFiles for assembly '{1}'; "
            + "verify the consuming csproj declares the contracts/geo/*.spec.json glob",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateNormalizedName"/>
    /// <remarks>
    /// <para>
    /// The default severity is <see cref="DiagnosticSeverity.Error"/>, and the
    /// diagnostic is <b>disabled by default</b>. The 2026-05-23 Tier-2 catalog
    /// contains subdivision-name collisions that pre-date this check, such as
    /// the Spain ES-NA / ES-NC "Navarra" pair, the Indonesia ID-PA / ID-PP
    /// "Papua" pair after the province split, the Malta MT-45 / MT-46 "Rabat"
    /// pair, and the Guinea GN single-letter and two-letter pairs. Set
    /// <c>isEnabledByDefault</c> to <c>true</c> after the catalog cleanup and
    /// the overlay canonicalization remove them. Consumers MAY opt in earlier
    /// through <c>.editorconfig</c>:
    /// <code>dotnet_diagnostic.D2GEO010.severity = error</code>
    /// </para>
    /// <para>
    /// The name resolver MUST treat any collision the build cannot resolve as
    /// fail-closed (NotFound), whether or not this build-time check is
    /// enabled. It never silently picks the first match.
    /// </para>
    /// </remarks>
    public static readonly DiagnosticDescriptor DuplicateNormalizedName = new(
        id: DiagnosticIds.DuplicateNormalizedName,
        title: "Geo catalog has duplicate normalized name",
        messageFormat:
            "Geo spec '{0}' has two or more entities sharing normalized name '{1}': "
            + "{2}. Name-resolver would have to silently pick one - fail-closed "
            + "by design.",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: false);

    /// <inheritdoc cref="DiagnosticIds.MissingLocaleReference"/>
    public static readonly DiagnosticDescriptor MissingLocaleReference = new(
        id: DiagnosticIds.MissingLocaleReference,
        title: "Geo country references unknown locale",
        messageFormat:
            "Country '{0}' references locale '{1}' which is not present in "
            + "locales.spec.json",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.Geo.SourceGen";
}
