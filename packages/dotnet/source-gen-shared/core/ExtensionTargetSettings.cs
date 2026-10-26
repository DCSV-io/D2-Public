// -----------------------------------------------------------------------
// <copyright file="ExtensionTargetSettings.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// The names of one generator's extension namespace and class prefix
/// properties and the descriptors of the diagnostics it reports when their
/// values are wrong.
/// </summary>
/// <param name="NamespaceProperty">The MSBuild property that sets the extension
/// namespace, without the <c>build_property.</c> prefix.</param>
/// <param name="ClassPrefixProperty">The MSBuild property that sets the prefix
/// of the extension class names, without the <c>build_property.</c> prefix.</param>
/// <param name="InvalidNamespace">The error reported for a namespace value that
/// is not a dot-separated sequence of C# identifiers.</param>
/// <param name="InvalidClassPrefix">The error reported for a class prefix value
/// that is absent or not a C# identifier while the namespace is set.</param>
/// <param name="ClassPrefixWithoutNamespace">The warning reported for a class
/// prefix value set without a namespace value.</param>
internal sealed record ExtensionTargetSettings(
    string NamespaceProperty,
    string ClassPrefixProperty,
    DiagnosticDescriptor InvalidNamespace,
    DiagnosticDescriptor InvalidClassPrefix,
    DiagnosticDescriptor ClassPrefixWithoutNamespace);
