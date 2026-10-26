// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainsGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EncryptionDomains.SourceGen;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DcsvIo.D2.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits encryption-domain catalogs.
/// In the public assembly <c>DcsvIo.D2.Encryption</c> it emits
/// <c>EncryptionDomains</c> over the values of the one spec file it is given. In
/// another assembly that sets the MSBuild properties
/// <c>D2EncryptionDomainsExtensionNamespace</c> and
/// <c>D2EncryptionDomainsExtensionClassPrefix</c>, it emits the same classes with
/// the prefix before each class name, under that namespace, over the union of
/// every encryption-domain spec file the assembly lists.
/// </summary>
[Generator]
public sealed class EncryptionDomainsGenerator : IIncrementalGenerator
{
    private const string _SPEC_FILE_NAME = "encryption-domains.spec.json";
    private const string _TARGET_ASSEMBLY_NAME = "DcsvIo.D2.Encryption";
    private const string _PUBLIC_ROOT_NAMESPACE = "DcsvIo.D2.Encryption";
    private const string _PUBLIC_CLASS_NAME = "EncryptionDomains";
    private const string _PUBLIC_MODE_ENUM = "EncryptionDomainMode";
    private const string _PUBLIC_MODES_CLASS = "EncryptionDomainModes";

    private static readonly ExtensionTargetSettings sr_extensionTarget = new(
        NamespaceProperty: "D2EncryptionDomainsExtensionNamespace",
        ClassPrefixProperty: "D2EncryptionDomainsExtensionClassPrefix",
        InvalidNamespace: DiagnosticDescriptors.SR_InvalidExtensionNamespace,
        InvalidClassPrefix: DiagnosticDescriptors.SR_InvalidExtensionClassPrefix,
        ClassPrefixWithoutNamespace: DiagnosticDescriptors.SR_ExtensionClassPrefixWithoutNamespace);

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var specs = context.AdditionalTextsProvider
            .Where(static file => IsSpecFile(file.Path))
            .Select(static (file, ct) => new SpecFile(
                Path: file.Path,
                Content: file.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        var targetOptions = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
                ExtensionTargetSelector.Read(provider.GlobalOptions, sr_extensionTarget));

        var combined = specs.Combine(context.CompilationProvider).Combine(targetOptions);

        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var ((specFiles, compilation), options) = tuple;

            var target = ExtensionTargetSelector.Select(
                compilation.AssemblyName, _TARGET_ASSEMBLY_NAME, options);
            ExtensionTargetSelector.Report(spc, target, sr_extensionTarget);

            if (target.Kind is not (ExtensionTargetKind.Public or ExtensionTargetKind.Extension))
                return;

            if (specFiles.IsDefaultOrEmpty)
                return;

            var isExtension = target.Kind == ExtensionTargetKind.Extension;
            var prefix = isExtension ? target.ClassPrefix : string.Empty;
            var rootNamespace = isExtension ? target.Namespace : _PUBLIC_ROOT_NAMESPACE;
            var className = prefix + _PUBLIC_CLASS_NAME;
            var modeEnum = prefix + _PUBLIC_MODE_ENUM;
            var modesClass = prefix + _PUBLIC_MODES_CLASS;
            var sourceName = className + ".g.cs";

            var ordered = specFiles.OrderBy(s => s.Path, StringComparer.Ordinal).ToList();
            var loadResult = EncryptionDomainsSpecLoader.Load(ordered[0].Path, ordered[0].Content);
            if (loadResult.Diagnostic is { } loadDiag)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(loadDiag));
                return;
            }

            var mergedDomains = loadResult.Spec!.Domains.ToBuilder();
            var seen = new HashSet<string>(
                loadResult.Spec.Domains.Select(d => d.ConstName),
                StringComparer.Ordinal);

            for (var i = 1; i < ordered.Count; i++)
            {
                var next = EncryptionDomainsSpecLoader.Load(ordered[i].Path, ordered[i].Content);
                if (next.Diagnostic is { } nextDiag)
                {
                    spc.ReportDiagnostic(ToRoslynDiagnostic(nextDiag));
                    continue;
                }

                foreach (var domain in next.Spec!.Domains)
                {
                    if (!seen.Add(domain.ConstName))
                        continue;

                    mergedDomains.Add(domain);
                }
            }

            var emitResult = EncryptionDomainsEmitter.Emit(
                new EncryptionDomainsSpec(mergedDomains.ToImmutable()),
                rootNamespace,
                className,
                modeEnum,
                modesClass);
            foreach (var d in emitResult.Diagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(d));

            spc.AddSource(
                sourceName,
                SourceText.From(emitResult.GeneratedSource, System.Text.Encoding.UTF8));
        });
    }

    private static bool IsSpecFile(string path) =>
        string.Equals(
            Path.GetFileName(path),
            _SPEC_FILE_NAME,
            StringComparison.OrdinalIgnoreCase);

    private static Diagnostic ToRoslynDiagnostic(EmitDiagnostic emitDiag)
    {
        var descriptor = ResolveDescriptor(emitDiag.DescriptorId);
        return Diagnostic.Create(descriptor, Location.None, emitDiag.Args.ToArray());
    }

    private static DiagnosticDescriptor ResolveDescriptor(string id) => id switch
    {
        DiagnosticIds.MalformedSpec => DiagnosticDescriptors.MalformedSpec,
        DiagnosticIds.DuplicateConstName => DiagnosticDescriptors.DuplicateConstName,
        DiagnosticIds.DuplicateValue => DiagnosticDescriptors.DuplicateValue,
        DiagnosticIds.InvalidConstName => DiagnosticDescriptors.InvalidConstName,
        DiagnosticIds.EmptyValue => DiagnosticDescriptors.EmptyValue,
        DiagnosticIds.InvalidMode => DiagnosticDescriptors.InvalidMode,
        DiagnosticIds.MissingConsumerService =>
            DiagnosticDescriptors.MissingConsumerService,
        DiagnosticIds.UnexpectedConsumerService =>
            DiagnosticDescriptors.UnexpectedConsumerService,
        DiagnosticIds.InvalidConsumerService =>
            DiagnosticDescriptors.InvalidConsumerService,
        _ => throw new InvalidOperationException(
            $"Unknown EmitDiagnostic descriptor id '{id}'."),
    };
}
