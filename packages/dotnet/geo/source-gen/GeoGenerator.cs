// -----------------------------------------------------------------------
// <copyright file="GeoGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen;

using System;
using System.Collections.Immutable;
using System.IO;
using DcsvIo.D2.Geo.SourceGen.Emitters;
using DcsvIo.D2.Geo.SourceGen.Emitters.Default;
using DcsvIo.D2.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits geo types and data from
/// the geo Tier-2 JSON spec files via <c>AdditionalFiles</c>. The
/// generator's responsibilities are split across two consumer assemblies:
/// <list type="bullet">
///   <item>
///     <c>DcsvIo.D2.Geo.Abstractions</c> -> emit all TYPES (enums, wrapper
///     structs, JsonConverters, record shapes, <c>GeoCatalog</c> constants,
///     <c>SubdivisionParentCountryLookup</c>).
///   </item>
///   <item>
///     <c>DcsvIo.D2.Geo.Default</c> -> emit all DATA (per-entity static
///     instances, nested static-class shells, lookup tables, and the
///     wire-nav coordinator).
///   </item>
///   <item>Anything else -> emit nothing.</item>
/// </list>
/// Diagnostics surfaced by <see cref="SpecLoader.LoadAll"/> + the per-emitter
/// passes are translated into Roslyn <c>Diagnostic</c> instances via the
/// generator's <see cref="ResolveDescriptor"/> switch over
/// <see cref="DiagnosticIds"/>.
/// </summary>
[Generator]
public sealed class GeoGenerator : IIncrementalGenerator
{
    private const string _SPEC_SUFFIX = ".spec.json";
    private const string _GEO_SPEC_DIRECTORY_NAME = "geo";

    private const string _ABSTRACTIONS_ASSEMBLY = "DcsvIo.D2.Geo.Abstractions";
    private const string _DEFAULT_ASSEMBLY = "DcsvIo.D2.Geo.Default";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var specs = context.AdditionalTextsProvider
            .Where(static file => IsGeoSpec(file.Path))
            .Select(static (file, ct) => new SpecFile(
                Path: file.Path,
                Content: file.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        var combined = specs.Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var (specFiles, compilation) = tuple;
            var assemblyName = compilation.AssemblyName ?? string.Empty;

            var isAbstractionsTarget = string.Equals(
                assemblyName, _ABSTRACTIONS_ASSEMBLY, StringComparison.Ordinal);
            var isDefaultTarget = string.Equals(
                assemblyName, _DEFAULT_ASSEMBLY, StringComparison.Ordinal);

            if (!isAbstractionsTarget && !isDefaultTarget)
                return;

            // Load every spec file present and report any parse diagnostics
            // before the per-emitter pass runs.
            var loadDiagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
            var specContext = SpecLoader.LoadAll(specFiles, loadDiagnostics, assemblyName);

            foreach (var diag in loadDiagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(diag));

            // Catalog uniqueness check. It reports each collision of two
            // identically named records; the runtime name resolver is what
            // fails closed (NotFound). It runs on every target, so whichever assembly is being
            // emitted into reports a D2GEO010 collision. That diagnostic fails
            // the build only when it is enabled (it is disabled by default).
            var uniquenessDiagnostics = CatalogUniquenessChecker.Check(specContext);
            foreach (var diag in uniquenessDiagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(diag));

            // Cross-catalog locale-reference check. It verifies that every
            // country.primaryLocaleIETFBCP47Tag and every entry in
            // country.localeIETFBCP47Tags[] resolves to a locale in
            // locales.spec.json. The build fails on D2GEO012.
            var localeRefDiagnostics = LocaleReferenceChecker.Check(specContext);
            foreach (var diag in localeRefDiagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(diag));

            if (isAbstractionsTarget)
            {
                // Emission order for this target: enums, wrapper structs,
                // JsonConverters, record shapes, then the parent-country
                // table and the GeoCatalog constants. The structs reference
                // the converters through [JsonConverter] and the converters
                // reference the structs through JsonConverter<T>, so both are
                // emitted as partials in the same compilation unit to satisfy
                // that cycle.
                EmitAll(spc, EnumEmitter.EmitAll(specContext));
                EmitAll(spc, WrapperStructEmitter.EmitAll(specContext));
                EmitAll(spc, JsonConverterEmitter.EmitAll(specContext));
                EmitAll(spc, RecordShapeEmitter.EmitAll(specContext));
                EmitAll(spc, ParentCountryTableEmitter.EmitAll(specContext));
                EmitAll(spc, GeoCatalogEmitter.EmitAll(specContext));
                return;
            }

            if (isDefaultTarget)
            {
                // Per-entity static-instance data, nested static-class
                // hierarchies and FrozenDictionary lookup tables. Each catalog
                // emits a static ctor and a WireNav() method. The static ctor
                // constructs the records with default or empty navigation
                // values (first pass of the two-pass populate pattern).
                // WireNav() sets the navigation references through the
                // friend-assembly internal setters (second pass). The
                // GeoDataInitializer coordinator, emitted last, has a
                // [ModuleInitializer] that runs every catalog's static ctor
                // first and then calls WireNav() in dependency order. The emit
                // order between catalogs does not affect the
                // [ModuleInitializer] runtime sequence.
                EmitAll(spc, CountryDataEmitter.EmitAll(specContext));
                EmitAll(spc, CurrencyDataEmitter.EmitAll(specContext));
                EmitAll(spc, LanguageDataEmitter.EmitAll(specContext));
                EmitAll(spc, GeopoliticalEntityDataEmitter.EmitAll(specContext));
                EmitAll(spc, SubdivisionDataEmitter.EmitAll(specContext));
                EmitAll(spc, LocaleDataEmitter.EmitAll(specContext));
                EmitAll(spc, TimezoneDataEmitter.EmitAll(specContext));
                EmitAll(spc, GeoDataInitializerEmitter.EmitAll(specContext));
            }
        });
    }

    private static bool IsGeoSpec(string path)
    {
        if (!path.EndsWith(_SPEC_SUFFIX, StringComparison.OrdinalIgnoreCase))
            return false;

        var dir = Path.GetDirectoryName(path);
        if (dir is null)
            return false;

        var dirName = Path.GetFileName(dir);
        return string.Equals(dirName, _GEO_SPEC_DIRECTORY_NAME, StringComparison.OrdinalIgnoreCase);
    }

    private static void EmitAll(SourceProductionContext spc, ImmutableArray<EmitResult> results)
    {
        foreach (var result in results)
        {
            foreach (var diag in result.Diagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(diag));

            spc.AddSource(
                result.HintName,
                SourceText.From(result.GeneratedSource, System.Text.Encoding.UTF8));
        }
    }

    private static Diagnostic ToRoslynDiagnostic(EmitDiagnostic emitDiag)
    {
        var descriptor = ResolveDescriptor(emitDiag.DescriptorId);
        return Diagnostic.Create(descriptor, Location.None, emitDiag.Args.ToArray());
    }

    private static DiagnosticDescriptor ResolveDescriptor(string id) => id switch
    {
        DiagnosticIds.MalformedSpec => DiagnosticDescriptors.MalformedSpec,
        DiagnosticIds.UnknownFk => DiagnosticDescriptors.UnknownFk,
        DiagnosticIds.InvalidIdentifier => DiagnosticDescriptors.InvalidIdentifier,
        DiagnosticIds.MissingCatalogMetadata => DiagnosticDescriptors.MissingCatalogMetadata,
        DiagnosticIds.MissingSpec => DiagnosticDescriptors.MissingSpec,
        DiagnosticIds.DuplicateNormalizedName => DiagnosticDescriptors.DuplicateNormalizedName,
        DiagnosticIds.MissingLocaleReference => DiagnosticDescriptors.MissingLocaleReference,
        _ => throw new InvalidOperationException(
            $"Unknown EmitDiagnostic descriptor id '{id}'."),
    };
}
