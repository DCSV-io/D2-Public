// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.ErrorCodes.SourceGen;

/// <summary>
/// String identifiers for the messaging catalog's per-catalog validation
/// diagnostics (<c>D2MEC*</c>). Kept in a separate class from
/// <see cref="DiagnosticDescriptors"/> so callers that are not hosted by
/// Roslyn can reference the ids without <c>Microsoft.CodeAnalysis</c>, which
/// this project marks <c>PrivateAssets="all"</c>. The shell maps these ids to
/// descriptors and supplies them to the shared engine through its
/// <c>CatalogConfig</c>; the catalog-neutral engine diagnostics
/// (<c>D2ERC*</c>) live in the engine.
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2MEC001";

    /// <summary>
    /// Entry's <c>category</c> is not one of the closed enum values declared by
    /// <c>error-category.spec.json</c>.
    /// </summary>
    public const string UnknownCategoryEnum = "D2MEC002";

    /// <summary>Two entries share the same <c>code</c>.</summary>
    public const string DuplicateCode = "D2MEC003";

    /// <summary>Two entries share the same <c>factoryName</c>.</summary>
    public const string DuplicateFactoryName = "D2MEC004";

    /// <summary>
    /// Entry's <c>httpStatus</c> is not one the codegen mapping supports.
    /// </summary>
    public const string InvalidHttpStatus = "D2MEC005";
}
