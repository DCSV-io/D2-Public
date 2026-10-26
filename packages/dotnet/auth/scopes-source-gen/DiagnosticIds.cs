// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Scopes.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by <see cref="ScopesGenerator"/>.
/// The IDs are in a separate class from <see cref="DiagnosticDescriptors"/> so
/// that pure-logic code can use them without referencing
/// <c>Microsoft.CodeAnalysis</c>, which the SrcGen csproj marks
/// <c>PrivateAssets="all"</c>.
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Scope spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2SCP001";

    /// <summary>
    /// Scope <c>grantedTo</c> entry references an unknown <c>OrgType</c> or <c>Role</c> enum value.
    /// </summary>
    public const string UnknownEnumValue = "D2SCP002";

    /// <summary>
    /// Scope name violates naming convention (must be lowercase dot-separated; segments must be
    /// valid C# identifiers; no consecutive dots).
    /// </summary>
    public const string InvalidScopeName = "D2SCP003";

    /// <summary>Two scope entries have the same name.</summary>
    public const string DuplicateScope = "D2SCP004";

    /// <summary>
    /// Scope marked <c>impersonationBlocked</c> but lives in the <c>anon.*</c> namespace
    /// (anonymous scopes cannot be impersonated).
    /// </summary>
    public const string AnonImpersonationBlockedNoise = "D2SCP005";

    /// <summary>
    /// Scope <c>grantedTo</c> entry has an empty role array (for example
    /// <c>{ "*": [] }</c>), which is invalid.
    /// </summary>
    public const string EmptyRoleArray = "D2SCP006";

    /// <summary>
    /// Scope name's tree position collides with another scope (one is the prefix of the other;
    /// for example <c>orders.refund</c> and <c>orders.refund.force</c>).
    /// </summary>
    public const string TreePositionCollision = "D2SCP007";

    /// <summary>
    /// Non-anonymous scope omits <c>grantedTo</c> entirely, so the scope is unreachable:
    /// no caller can ever be granted it.
    /// </summary>
    public const string MissingGrantedTo = "D2SCP008";

    /// <summary>No scope spec file found among <c>AdditionalFiles</c>.</summary>
    public const string MissingSpecFile = "D2SCP009";

    /// <summary>
    /// The declared <c>internal</c> flag and the <c>internal.</c> name prefix
    /// disagree, in either direction.
    /// </summary>
    public const string InternalFlagPrefixDisagreement = "D2SCP010";

    /// <summary>
    /// The <c>D2ScopesExtensionNamespace</c> property is not a dot-separated
    /// sequence of C# identifiers.
    /// </summary>
    public const string InvalidExtensionNamespace = "D2SCP011";

    /// <summary>
    /// The <c>D2ScopesExtensionNamespace</c> property is set and the
    /// <c>D2ScopesExtensionClassPrefix</c> property is absent or not a C# identifier.
    /// </summary>
    public const string InvalidExtensionClassPrefix = "D2SCP012";

    /// <summary>
    /// The <c>D2ScopesExtensionClassPrefix</c> property is set and the
    /// <c>D2ScopesExtensionNamespace</c> property is not.
    /// </summary>
    public const string ExtensionClassPrefixWithoutNamespace = "D2SCP013";
}
