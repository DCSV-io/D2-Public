// -----------------------------------------------------------------------
// <copyright file="RegistryGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.Registry.SourceGen;

using System;
using System.Collections.Immutable;
using System.IO;
using DcsvIo.D2.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits the merged cross-catalog
/// <c>ErrorCodeRegistry.g.cs</c> into <c>DcsvIo.D2.ErrorCodes.Registry</c>
/// (public GEN and public LIB catalogs only).
/// </summary>
[Generator]
public sealed class RegistryGenerator : IIncrementalGenerator
{
    private const string _HINT_NAME = "ErrorCodeRegistry.g.cs";
    private const string _CATEGORY_SPEC_NAME = "error-category.spec.json";

    private static readonly RegistryEmitConfig sr_config = RegistryEmitConfig.Public;

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var specs = context.AdditionalTextsProvider
            .Where(static file => IsRelevantSpec(file.Path))
            .Select(static (file, ct) => new SpecFile(
                Path: file.Path,
                Content: file.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        var combined = specs.Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var (specFiles, compilation) = tuple;

            if (!string.Equals(
                compilation.AssemblyName,
                sr_config.TargetAssemblyName,
                StringComparison.Ordinal))
                return;

            if (specFiles.IsDefaultOrEmpty)
                return;

            var categoryWireSet = ImmutableHashSet<string>.Empty
                .WithComparer(StringComparer.Ordinal);
            var errorCodeSpecs = ImmutableArray.CreateBuilder<SpecFile>();
            foreach (var spec in specFiles)
            {
                if (IsCategorySpec(spec.Path))
                    categoryWireSet = CategorySpecLoader.LoadWireSet(spec.Content);
                else
                    errorCodeSpecs.Add(spec);
            }

            if (errorCodeSpecs.Count == 0)
                return;

            var loadDiagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
            var entries = RegistrySpecLoader.LoadAll(
                errorCodeSpecs.ToImmutable(), loadDiagnostics);

            foreach (var diag in loadDiagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(diag));

            if (loadDiagnostics.Count > 0)
                return;

            var collisionDiagnostics = RegistryCollisionChecker.Check(entries);
            foreach (var diag in collisionDiagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(diag));

            if (!collisionDiagnostics.IsEmpty)
                return;

            var categoryDiagnostics = CategorySpecLoader.Check(entries, categoryWireSet);
            foreach (var diag in categoryDiagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(diag));

            if (!categoryDiagnostics.IsEmpty)
                return;

            var source = RegistryEmitter.Emit(entries, sr_config);
            spc.AddSource(_HINT_NAME, SourceText.From(source, System.Text.Encoding.UTF8));
        });
    }

    private static bool IsRelevantSpec(string path) =>
        IsErrorCodeSpec(path) || IsCategorySpec(path);

    private static bool IsErrorCodeSpec(string path) =>
        RegistrySpecLoader.IsErrorCodeSpec(Path.GetFileName(path));

    private static bool IsCategorySpec(string path) =>
        string.Equals(
            Path.GetFileName(path),
            _CATEGORY_SPEC_NAME,
            StringComparison.OrdinalIgnoreCase);

    private static Diagnostic ToRoslynDiagnostic(EmitDiagnostic emitDiag)
    {
        var descriptor = ResolveDescriptor(emitDiag.DescriptorId);
        return Diagnostic.Create(descriptor, Location.None, emitDiag.Args.ToArray());
    }

    private static DiagnosticDescriptor ResolveDescriptor(string id) => id switch
    {
        RegistryDiagnosticIds.CrossCatalogDuplicateCode =>
            RegistryDiagnosticDescriptors.CrossCatalogDuplicateCode,
        RegistryDiagnosticIds.ReservedNamespaceViolation =>
            RegistryDiagnosticDescriptors.ReservedNamespaceViolation,
        RegistryDiagnosticIds.MalformedRegistrySpec =>
            RegistryDiagnosticDescriptors.MalformedRegistrySpec,
        RegistryDiagnosticIds.UnknownCategory =>
            RegistryDiagnosticDescriptors.UnknownCategory,
        RegistryDiagnosticIds.UnregisteredCatalog =>
            RegistryDiagnosticDescriptors.UnregisteredCatalog,
        _ => throw new InvalidOperationException(
            $"Unknown registry diagnostic id '{id}'."),
    };
}
