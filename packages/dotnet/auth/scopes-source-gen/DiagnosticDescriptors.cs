// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Scopes.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the IDs declared in
/// <see cref="DiagnosticIds"/>. This class references
/// <c>Microsoft.CodeAnalysis</c>. The IDs are also string constants in
/// <see cref="DiagnosticIds"/>, which does not reference
/// <c>Microsoft.CodeAnalysis</c>.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "Scope spec is malformed",
        messageFormat: "Scope spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.UnknownEnumValue"/>
    public static readonly DiagnosticDescriptor UnknownEnumValue = new(
        id: DiagnosticIds.UnknownEnumValue,
        title: "Scope grantedTo references unknown enum value",
        messageFormat:
            "Scope '{0}' grantedTo entry '{1}' references unknown {2} value '{3}' (valid: {4})",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidScopeName"/>
    public static readonly DiagnosticDescriptor InvalidScopeName = new(
        id: DiagnosticIds.InvalidScopeName,
        title: "Scope name violates naming convention",
        messageFormat: "Scope name '{0}' is invalid: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateScope"/>
    public static readonly DiagnosticDescriptor DuplicateScope = new(
        id: DiagnosticIds.DuplicateScope,
        title: "Duplicate scope name",
        messageFormat: "Scope name '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.AnonImpersonationBlockedNoise"/>
    public static readonly DiagnosticDescriptor AnonImpersonationBlockedNoise = new(
        id: DiagnosticIds.AnonImpersonationBlockedNoise,
        title: "Anonymous scope marked impersonationBlocked",
        messageFormat:
            "Anonymous scope '{0}' is marked impersonationBlocked but anonymous scopes are "
            + "pre-auth and cannot be impersonated; the flag is meaningless",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.EmptyRoleArray"/>
    public static readonly DiagnosticDescriptor EmptyRoleArray = new(
        id: DiagnosticIds.EmptyRoleArray,
        title: "Scope grantedTo has empty role array",
        messageFormat:
            "Scope '{0}' grantedTo entry '{1}' has an empty role array; for 'no grant', "
            + "omit the entry entirely",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.TreePositionCollision"/>
    public static readonly DiagnosticDescriptor TreePositionCollision = new(
        id: DiagnosticIds.TreePositionCollision,
        title: "Scope tree-position collision",
        messageFormat:
            "Scope '{0}' cannot be both a leaf constant and a parent class (it is a dot-prefix "
            + "of '{1}'); rename one with a suffix like '.consent' or '.default'",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MissingGrantedTo"/>
    public static readonly DiagnosticDescriptor MissingGrantedTo = new(
        id: DiagnosticIds.MissingGrantedTo,
        title: "Non-anonymous scope omits grantedTo",
        messageFormat:
            "Scope '{0}' is non-anonymous but omits grantedTo, so no caller can ever be granted "
            + "it; add a grantedTo entry or move under the anon.* namespace",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MissingSpecFile"/>
    public static readonly DiagnosticDescriptor MissingSpecFile = new(
        id: DiagnosticIds.MissingSpecFile,
        title: "Scope spec file not found",
        messageFormat:
            "The Scopes source generator could not locate 'scopes.spec.json' among "
            + "AdditionalFiles; verify the consuming csproj declares the "
            + "contracts/auth-scopes/scopes.spec.json glob",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InternalFlagPrefixDisagreement"/>
    public static readonly DiagnosticDescriptor InternalFlagPrefixDisagreement = new(
        id: DiagnosticIds.InternalFlagPrefixDisagreement,
        title: "Scope internal flag disagrees with the scope name prefix",
        messageFormat:
            "Scope '{0}' declares internal '{1}' but its name {2} the 'internal.' prefix; a "
            + "workload-only scope is declared by both, so set the flag and the name to agree",
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

    private const string _CATEGORY = "DcsvIo.D2.Auth.Scopes.SourceGen";
}
