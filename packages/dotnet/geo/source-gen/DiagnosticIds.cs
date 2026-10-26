// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by <see cref="GeoGenerator"/>.
/// This class is separate from <see cref="DiagnosticDescriptors"/> so that code
/// running outside the Roslyn host, such as the spec loader and the emitters,
/// can reference the IDs without referencing <c>Microsoft.CodeAnalysis</c>.
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Geo spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2GEO001";

    /// <summary>
    /// FK code refers to an entity not present in the target catalog
    /// (e.g. a country primary-currency referencing an ISO 4217 alpha code
    /// absent from <c>currencies.spec.json</c>).
    /// </summary>
    public const string UnknownFk = "D2GEO002";

    /// <summary>
    /// A currency code, geopolitical-entity short code or time zone
    /// identifier segment cannot form a valid C# identifier even after
    /// sanitization, so the Default-target emitters skip it.
    /// </summary>
    public const string InvalidIdentifier = "D2GEO004";

    /// <summary>
    /// Missing or invalid <c>catalogVersion</c> / <c>generatedAt</c> in a
    /// spec file (needed for <c>GeoCatalog</c> constant emission).
    /// </summary>
    public const string MissingCatalogMetadata = "D2GEO006";

    /// <summary>
    /// A required spec file is missing from <c>AdditionalFiles</c>. The spec
    /// files in <c>contracts/geo/</c> are <c>countries</c>,
    /// <c>subdivisions</c>, <c>currencies</c>, <c>languages</c>,
    /// <c>locales</c>, <c>timezones</c> and <c>geopolitical-entities</c>, each
    /// with the <c>.spec.json</c> suffix.
    /// </summary>
    public const string MissingSpec = "D2GEO007";

    /// <summary>
    /// Catalog-uniqueness violation - two entities in the same catalog share
    /// the same normalized name across any matchable name field (DisplayName,
    /// OfficialName, EndonymDisplayName, EndonymOfficialName, ShortCode,
    /// alias, ISO code). When enabled (it is disabled by default), the check
    /// reports each collision and fails the build; the runtime name resolver is
    /// what fails closed (NotFound) on a collision instead of letting the first
    /// match win in <c>IGeoNameResolver</c>.
    /// </summary>
    public const string DuplicateNormalizedName = "D2GEO010";

    /// <summary>
    /// A country references a locale tag (via
    /// <c>primaryLocaleIETFBCP47Tag</c> or <c>localeIETFBCP47Tags[]</c>) that
    /// has no corresponding entry in <c>locales.spec.json</c>. Surfaced
    /// during spec validation so the data emitters can emit direct indexer
    /// access (fail-loud) rather than defensive <c>TryGetValue + skip</c>
    /// patterns that mask drift between catalogs.
    /// </summary>
    /// <remarks>
    /// D2GEO003, D2GEO005, D2GEO008 and D2GEO009 are not allocated here.
    /// D2GEO011 is reserved on the TS-side for the geo-data-pipeline's
    /// <c>GEO_CLDR_ZOMBIE_DROPPED</c> (no .NET equivalent - the pipeline is
    /// TS-only). D2GEO012 is the first slot shared across both languages
    /// after that carve-out.
    /// </remarks>
    public const string MissingLocaleReference = "D2GEO012";
}
