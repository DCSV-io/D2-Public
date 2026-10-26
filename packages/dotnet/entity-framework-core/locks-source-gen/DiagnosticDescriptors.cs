// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AdvisoryLocks.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>Roslyn descriptors for the IDs in <see cref="DiagnosticIds"/>.</summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor SR_MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "Advisory locks spec is malformed",
        messageFormat: "Advisory locks spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateConstNameInDatabase"/>
    public static readonly DiagnosticDescriptor SR_DuplicateConstNameInDatabase = new(
        id: DiagnosticIds.DuplicateConstNameInDatabase,
        title: "Duplicate advisory-lock constName within database",
        messageFormat:
            "Advisory-lock constName '{0}' is declared more than once "
            + "in database '{1}'",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateKeyInDatabase"/>
    public static readonly DiagnosticDescriptor SR_DuplicateKeyInDatabase = new(
        id: DiagnosticIds.DuplicateKeyInDatabase,
        title: "Duplicate advisory-lock key within database",
        messageFormat:
            "Advisory-lock key {0} is already used by '{1}' in database '{2}'; "
            + "each key must be unique within its database",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidConstName"/>
    public static readonly DiagnosticDescriptor SR_InvalidConstName = new(
        id: DiagnosticIds.InvalidConstName,
        title: "Advisory-lock constName has invalid shape",
        messageFormat:
            "Advisory-lock constName '{0}' must match UPPER_SNAKE_CASE pattern "
            + "^[A-Z][A-Z0-9_]*$",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.KeyOutOfRange"/>
    public static readonly DiagnosticDescriptor SR_KeyOutOfRange = new(
        id: DiagnosticIds.KeyOutOfRange,
        title: "Advisory-lock key is out of signed 64-bit integer range",
        messageFormat:
            "Advisory-lock '{0}' key value {1} is outside the valid range "
            + "[{2}, {3}] for a PostgreSQL int8 advisory lock",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.IncompleteEmitConfig"/>
    public static readonly DiagnosticDescriptor SR_IncompleteEmitConfig = new(
        id: DiagnosticIds.IncompleteEmitConfig,
        title: "Incomplete advisory-locks emit configuration",
        messageFormat:
            "Both MSBuild properties 'D2AdvisoryLocksEmitNamespace' and "
            + "'D2AdvisoryLocksDatabases' must be set (non-empty after trim), "
            + "or both omitted for quiet no-emit",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.UnknownDatabaseFilter"/>
    public static readonly DiagnosticDescriptor SR_UnknownDatabaseFilter = new(
        id: DiagnosticIds.UnknownDatabaseFilter,
        title: "Unknown advisory-locks database filter token",
        messageFormat:
            "Unknown database filter token(s) '{0}' in D2AdvisoryLocksDatabases "
            + "(each token must match a catalog 'database' value, Ordinal; "
            + "no AdvisoryLocks source is emitted)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateClassInDatabase"/>
    public static readonly DiagnosticDescriptor SR_DuplicateClassInDatabase = new(
        id: DiagnosticIds.DuplicateClassInDatabase,
        title: "Duplicate advisory-lock class within database",
        messageFormat:
            "Advisory-lock class {0} is already used by '{1}' in database '{2}'; "
            + "each class must be unique within its database",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.ClassOutOfRange"/>
    public static readonly DiagnosticDescriptor SR_ClassOutOfRange = new(
        id: DiagnosticIds.ClassOutOfRange,
        title: "Advisory-lock class is out of signed 32-bit integer range",
        messageFormat:
            "Advisory-lock class '{0}' value {1} is outside the valid range "
            + "[{2}, {3}] for the first argument of a two-integer advisory lock",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.AdvisoryLocks.SourceGen";
}
