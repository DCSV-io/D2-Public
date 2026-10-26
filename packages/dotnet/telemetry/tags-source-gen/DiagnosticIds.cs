// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry.Tags.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by
/// <see cref="TelemetryTagsGenerator"/>. Kept in a separate class from
/// <see cref="DiagnosticDescriptors"/> so code that runs outside the Roslyn
/// host, such as the pure-logic <see cref="TelemetryTagsEmitter"/> and
/// <see cref="TelemetrySpecLoader"/>, can reference the IDs without
/// referencing <c>Microsoft.CodeAnalysis</c>, which the source generator
/// project marks <c>PrivateAssets="all"</c>.
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2TEL001";

    /// <summary>Two meters share the same <c>meter</c> name.</summary>
    public const string DuplicateMeter = "D2TEL002";

    /// <summary>Two instruments within a single meter share the same <c>name</c>.</summary>
    public const string DuplicateInstrument = "D2TEL003";

    /// <summary>
    /// Instrument <c>kind</c> is not one of <c>counter</c> / <c>histogram</c>
    /// / <c>gauge</c>.
    /// </summary>
    public const string UnknownInstrumentKind = "D2TEL004";

    /// <summary>A tag declares duplicate values within its <c>values</c> array.</summary>
    public const string DuplicateTagValue = "D2TEL005";

    /// <summary>
    /// Cross-spec inconsistency - a tag's <c>valuesFromSpec</c> reference is
    /// unknown or could not be resolved (e.g. AuthErrorCodes spec missing
    /// from <c>AdditionalFiles</c>).
    /// </summary>
    public const string CrossSpecInconsistency = "D2TEL006";
}
