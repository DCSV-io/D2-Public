// -----------------------------------------------------------------------
// <copyright file="ExtensionTargetSelector.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.SourceGen;

using System;
using DcsvIo.D2.SourceGen.Polyfills;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Selects the output target of a generator that has a fixed public target and
/// an optional extension target. The public target is the compilation whose
/// assembly name equals the generator's public assembly name. The extension
/// target is set by MSBuild properties that the host project makes visible to
/// the compiler: the namespace of the extension output and the prefix of its
/// class names.
/// </summary>
internal static class ExtensionTargetSelector
{
    private const string _BUILD_PROPERTY_PREFIX = "build_property.";

    /// <summary>
    /// Reads the extension namespace and class prefix properties from the
    /// global analyzer options.
    /// </summary>
    /// <param name="globalOptions">The global analyzer options of the compilation.</param>
    /// <param name="settings">The names of the generator's properties.</param>
    /// <returns>The raw values; an unset property reads as the empty string.</returns>
    public static ExtensionTargetOptions Read(
        AnalyzerConfigOptions globalOptions,
        ExtensionTargetSettings settings)
    {
        globalOptions.TryGetValue(
            _BUILD_PROPERTY_PREFIX + settings.NamespaceProperty, out var ns);
        globalOptions.TryGetValue(
            _BUILD_PROPERTY_PREFIX + settings.ClassPrefixProperty, out var prefix);

        return new ExtensionTargetOptions(ns ?? string.Empty, prefix ?? string.Empty);
    }

    /// <summary>
    /// Chooses the target for one compilation. A value that is empty or
    /// whitespace counts as absent, and a present value is trimmed.
    /// </summary>
    /// <param name="assemblyName">The compilation's assembly name.</param>
    /// <param name="publicAssemblyName">The assembly name of the public target.</param>
    /// <param name="options">The raw property values.</param>
    /// <returns>The chosen target and the trimmed values.</returns>
    public static ExtensionTargetSelection Select(
        string? assemblyName,
        string publicAssemblyName,
        ExtensionTargetOptions options)
    {
        if (string.Equals(assemblyName, publicAssemblyName, StringComparison.Ordinal))
        {
            return new ExtensionTargetSelection(
                ExtensionTargetKind.Public, string.Empty, string.Empty, false, false);
        }

        var ns = options.Namespace.Trim();
        var prefix = options.ClassPrefix.Trim();

        if (ns.Falsey())
        {
            var kind = prefix.Falsey()
                ? ExtensionTargetKind.None
                : ExtensionTargetKind.ClassPrefixWithoutNamespace;

            return new ExtensionTargetSelection(kind, string.Empty, prefix, false, false);
        }

        var namespaceInvalid = !IsValidNamespace(ns);
        var prefixInvalid = !IsValidIdentifier(prefix);

        var selected = namespaceInvalid || prefixInvalid
            ? ExtensionTargetKind.Rejected
            : ExtensionTargetKind.Extension;

        return new ExtensionTargetSelection(selected, ns, prefix, namespaceInvalid, prefixInvalid);
    }

    /// <summary>
    /// Reports the diagnostics a selection carries: an error for an invalid
    /// namespace, an error for an absent or invalid class prefix, and a
    /// warning for a class prefix set without a namespace.
    /// </summary>
    /// <param name="context">The source production context to report to.</param>
    /// <param name="selection">The selection to report on.</param>
    /// <param name="settings">The generator's property names and descriptors.</param>
    public static void Report(
        SourceProductionContext context,
        ExtensionTargetSelection selection,
        ExtensionTargetSettings settings)
    {
        if (selection.NamespaceInvalid)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                settings.InvalidNamespace,
                Location.None,
                settings.NamespaceProperty,
                selection.Namespace));
        }

        if (selection.ClassPrefixInvalid)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                settings.InvalidClassPrefix,
                Location.None,
                settings.ClassPrefixProperty,
                selection.ClassPrefix,
                settings.NamespaceProperty));
        }

        if (selection.Kind == ExtensionTargetKind.ClassPrefixWithoutNamespace)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                settings.ClassPrefixWithoutNamespace,
                Location.None,
                settings.ClassPrefixProperty,
                selection.ClassPrefix,
                settings.NamespaceProperty));
        }
    }

    private static bool IsValidNamespace(string value)
    {
        foreach (var segment in value.Split('.'))
        {
            if (!IsValidIdentifier(segment))
                return false;
        }

        return true;
    }

    private static bool IsValidIdentifier(string value) =>
        SyntaxFacts.IsValidIdentifier(value)
        && SyntaxFacts.GetKeywordKind(value) == SyntaxKind.None;
}
