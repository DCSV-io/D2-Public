// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.ProtocolAudiences.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by
/// <see cref="ProtocolAudiencesGenerator"/>. The values equal the TypeScript
/// emitter's <c>D2PAUD*</c> IDs byte for byte, because both sides
/// read the same spec and so check the same predicates. The IDs are in a separate
/// class from <see cref="DiagnosticDescriptors"/> so that pure-logic code can
/// use them without referencing <c>Microsoft.CodeAnalysis</c>, which the SrcGen csproj marks
/// <c>PrivateAssets="all"</c>.
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Two protocol-audience entries have the same name.</summary>
    public const string DuplicateName = "D2PAUD001";

    /// <summary>
    /// Protocol-audience name violates the SCREAMING_SNAKE_CASE convention (must
    /// match <c>^[A-Z][A-Z0-9_]*$</c>).
    /// </summary>
    public const string InvalidName = "D2PAUD002";

    /// <summary>Two protocol-audience entries have the same value.</summary>
    public const string DuplicateValue = "D2PAUD003";

    /// <summary>Protocol-audience value is empty (would never match a real aud claim).</summary>
    public const string EmptyValue = "D2PAUD004";

    /// <summary>Protocol-audience spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2PAUD009";

    /// <summary>No protocol-audience spec file found among <c>AdditionalFiles</c>.</summary>
    public const string MissingSpecFile = "D2PAUD010";
}
