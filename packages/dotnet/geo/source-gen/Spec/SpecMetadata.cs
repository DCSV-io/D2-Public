// -----------------------------------------------------------------------
// <copyright file="SpecMetadata.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Spec;

/// <summary>
/// Common header block of every geo spec file. It holds the catalog version
/// and the provenance fields required to emit the <c>GeoCatalog</c>
/// constants. The provenance fields differ between pipeline-derived specs
/// (<c>$generated: true</c> + <c>generatedAt</c>) and the hand-written
/// <c>geopolitical-entities.spec.json</c> (<c>$generated: false</c> +
/// <c>lastEditedAt</c>). The record represents both forms, and the unused
/// field is <c>null</c> on the other branch.
/// </summary>
/// <param name="CatalogVersion">
/// Semver string (<c>"0.1.0"</c> etc.), emitted as the <c>GeoCatalog</c>
/// constant <c>CatalogVersion</c> of the abstractions assembly.
/// </param>
/// <param name="GeneratedAt">
/// ISO-8601 timestamp for pipeline-derived specs; <c>null</c> for the
/// hand-written catalog.
/// </param>
/// <param name="LastEditedAt">
/// <c>YYYY-MM-DD</c> string for the hand-written catalog; <c>null</c> for
/// pipeline-derived specs.
/// </param>
/// <param name="IsGenerated">
/// True when <c>$generated: true</c> (pipeline-derived); false for the
/// hand-written catalog.
/// </param>
/// <param name="Source">
/// Free-form provenance string (<c>"pipeline-derived"</c> or
/// <c>"manual"</c>) - recorded verbatim from the spec.
/// </param>
internal sealed record SpecMetadata(
    string CatalogVersion,
    string? GeneratedAt,
    string? LastEditedAt,
    bool IsGenerated,
    string Source);
