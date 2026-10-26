// -----------------------------------------------------------------------
// <copyright file="AdvisoryLocksGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AdvisoryLocks.SourceGen;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits the <c>AdvisoryLocks</c>
/// static class into each owning-module assembly that opts in via MSBuild
/// properties and supplies the advisory-locks catalog as an AdditionalFile.
/// Emit namespace and database-nest filter are host-supplied
/// (<c>D2AdvisoryLocksEmitNamespace</c> / <c>D2AdvisoryLocksDatabases</c>);
/// the generator never hardcodes product assemblies. Shared Postgres owns
/// mechanism only (<c>PgAdvisoryLock</c> / migrator); domain lock-key catalogs
/// live with the owning module. Uniqueness still validates the full
/// catalog before filtering.
/// </summary>
[Generator]
public sealed class AdvisoryLocksGenerator : IIncrementalGenerator
{
    private const string _SOURCE_NAME = "AdvisoryLocks.g.cs";
    private const string _SPEC_FILE_NAME = "advisory-locks.spec.json";
    private const string _PROP_NAMESPACE = "build_property.D2AdvisoryLocksEmitNamespace";
    private const string _PROP_DATABASES = "build_property.D2AdvisoryLocksDatabases";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var specs = context.AdditionalTextsProvider
            .Where(static file => IsSpecFile(file.Path))
            .Select(static (file, ct) => new SpecFile(
                Path: file.Path,
                Content: file.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        var emitConfig = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
            {
                provider.GlobalOptions.TryGetValue(_PROP_NAMESPACE, out var ns);
                provider.GlobalOptions.TryGetValue(_PROP_DATABASES, out var dbs);
                return new EmitConfig(ns ?? string.Empty, dbs ?? string.Empty);
            });

        var combined = specs.Combine(emitConfig);

        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var (specFiles, config) = tuple;

            var nsSet = config.Namespace.Truthy();
            var dbsSet = config.Databases.Truthy();

            // Opt-in is silent: when both properties are missing, empty or whitespace,
            // emit nothing.
            if (!nsSet && !dbsSet)
                return;

            // Incomplete: exactly one non-empty after Falsey trim semantics.
            if (nsSet != dbsSet)
            {
                spc.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.SR_IncompleteEmitConfig,
                        Location.None));
                return;
            }

            var rootNamespace = config.Namespace.Trim();
            var filterTokens = ParseDatabaseFilter(config.Databases);

            // Namespace set but databases empty after comma-split/trim -> incomplete.
            if (filterTokens.Length == 0)
            {
                spc.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.SR_IncompleteEmitConfig,
                        Location.None));
                return;
            }

            if (specFiles.IsDefaultOrEmpty)
                return;

            var spec = specFiles.OrderBy(s => s.Path, StringComparer.Ordinal).First();

            var loadResult = AdvisoryLocksSpecLoader.Load(spec.Path, spec.Content);
            if (loadResult.Diagnostic is { } loadDiag)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(loadDiag));
                return;
            }

            var loadedSpec = loadResult.Spec!;

            // The emitter unions both arrays, so a database contributing only
            // classes still gets its nest and is a known filter token here.
            var catalogDatabases = new HashSet<string>(
                loadedSpec.Locks.Select(e => e.Database),
                StringComparer.Ordinal);

            foreach (var classEntry in loadedSpec.Classes)
                catalogDatabases.Add(classEntry.Database);

            var unknownTokens = filterTokens
                .Where(t => !catalogDatabases.Contains(t))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            // Always validate uniqueness over the full catalog first, whether or not the
            // filter is valid.
            var emitResult = AdvisoryLocksEmitter.Emit(
                loadedSpec,
                rootNamespace,
                filterTokens);
            foreach (var d in emitResult.Diagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(d));

            // Unknown filter tokens: report D2LCK007 and skip AddSource, so no partial
            // class is emitted.
            if (unknownTokens.Length > 0)
            {
                spc.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.SR_UnknownDatabaseFilter,
                        Location.None,
                        string.Join(", ", unknownTokens)));
                return;
            }

            spc.AddSource(
                _SOURCE_NAME,
                SourceText.From(emitResult.GeneratedSource, System.Text.Encoding.UTF8));
        });
    }

    private static string[] ParseDatabaseFilter(string databasesProp)
    {
        if (databasesProp.Falsey())
            return [];

        return databasesProp
            .Split([','], StringSplitOptions.None)
            .Select(static t => t.Trim())
            .Where(static t => t.Truthy())
            .ToArray();
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
        DiagnosticIds.MalformedSpec => DiagnosticDescriptors.SR_MalformedSpec,
        DiagnosticIds.DuplicateConstNameInDatabase =>
            DiagnosticDescriptors.SR_DuplicateConstNameInDatabase,
        DiagnosticIds.DuplicateKeyInDatabase => DiagnosticDescriptors.SR_DuplicateKeyInDatabase,
        DiagnosticIds.DuplicateClassInDatabase =>
            DiagnosticDescriptors.SR_DuplicateClassInDatabase,
        DiagnosticIds.ClassOutOfRange => DiagnosticDescriptors.SR_ClassOutOfRange,
        DiagnosticIds.InvalidConstName => DiagnosticDescriptors.SR_InvalidConstName,
        DiagnosticIds.KeyOutOfRange => DiagnosticDescriptors.SR_KeyOutOfRange,
        DiagnosticIds.IncompleteEmitConfig => DiagnosticDescriptors.SR_IncompleteEmitConfig,
        DiagnosticIds.UnknownDatabaseFilter => DiagnosticDescriptors.SR_UnknownDatabaseFilter,
        _ => throw new InvalidOperationException(
            $"Unknown EmitDiagnostic descriptor id '{id}'."),
    };

    private sealed record EmitConfig(string Namespace, string Databases);
}
