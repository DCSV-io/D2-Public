// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.ErrorCodes.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the ids declared in
/// <see cref="DiagnosticIds"/>. This class loads only inside the Roslyn host,
/// where <see cref="ErrorCodesGenerator.Initialize"/> runs. Code that needs
/// only the ID strings uses the <see cref="DiagnosticIds"/> constants, which do
/// not load <c>Microsoft.CodeAnalysis</c> at runtime.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor SR_MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "Messaging error codes spec is malformed",
        messageFormat:
            "Messaging error codes spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.UnknownCategoryEnum"/>
    public static readonly DiagnosticDescriptor SR_UnknownCategoryEnum = new(
        id: DiagnosticIds.UnknownCategoryEnum,
        title: "Error code category is not one of the supported values",
        messageFormat: "Error code '{0}' has unknown category '{1}' (valid: {2})",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateCode"/>
    public static readonly DiagnosticDescriptor SR_DuplicateCode = new(
        id: DiagnosticIds.DuplicateCode,
        title: "Duplicate messaging error code",
        messageFormat: "Messaging error code '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateFactoryName"/>
    public static readonly DiagnosticDescriptor SR_DuplicateFactoryName = new(
        id: DiagnosticIds.DuplicateFactoryName,
        title: "Duplicate messaging error code factory name",
        messageFormat:
            "Messaging error code factory name '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidHttpStatus"/>
    public static readonly DiagnosticDescriptor SR_InvalidHttpStatus = new(
        id: DiagnosticIds.InvalidHttpStatus,
        title: "Messaging error code httpStatus is not supported by the codegen mapping",
        messageFormat:
            "Error code '{0}' has unsupported httpStatus '{1}' (supported: {2}). "
            + "Add the new status to the codegen mapping matrix when expanding.",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.Messaging.RabbitMq.ErrorCodes.SourceGen";
}
