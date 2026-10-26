// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Audiences.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by <see cref="AudiencesGenerator"/>.
/// Kept in a separate class from <see cref="DiagnosticDescriptors"/> so code that
/// does not run in the Roslyn host can reference the IDs without loading
/// <c>Microsoft.CodeAnalysis</c> (which the SrcGen csproj marks
/// <c>PrivateAssets="all"</c>).
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Audience spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2AUD001";

    /// <summary>
    /// Audience name violates the C# identifier convention (must match
    /// <c>^[A-Z][A-Za-z0-9]*$</c>).
    /// </summary>
    public const string InvalidAudienceName = "D2AUD002";

    /// <summary>Two audience entries share the exact same name (duplicate).</summary>
    public const string DuplicateAudienceName = "D2AUD003";

    /// <summary>
    /// Two audience entries share the exact same URL (would silently alias one to
    /// the other at validation time).
    /// </summary>
    public const string DuplicateAudienceUrl = "D2AUD004";

    /// <summary>
    /// Audience URL is not a parseable absolute URI (would never match a real
    /// JWT <c>aud</c> claim value).
    /// </summary>
    public const string InvalidAudienceUrl = "D2AUD005";

    /// <summary>No audience spec file found among <c>AdditionalFiles</c>.</summary>
    public const string MissingSpecFile = "D2AUD006";

    /// <summary>
    /// The <c>D2AudiencesExtensionNamespace</c> property is not a dot-separated
    /// sequence of C# identifiers.
    /// </summary>
    public const string InvalidExtensionNamespace = "D2AUD007";

    /// <summary>
    /// The <c>D2AudiencesExtensionNamespace</c> property is set and the
    /// <c>D2AudiencesExtensionClassPrefix</c> property is absent or not a C# identifier.
    /// </summary>
    public const string InvalidExtensionClassPrefix = "D2AUD008";

    /// <summary>
    /// The <c>D2AudiencesExtensionClassPrefix</c> property is set and the
    /// <c>D2AudiencesExtensionNamespace</c> property is not.
    /// </summary>
    public const string ExtensionClassPrefixWithoutNamespace = "D2AUD009";
}
