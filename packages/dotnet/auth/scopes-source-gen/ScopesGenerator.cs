// -----------------------------------------------------------------------
// <copyright file="ScopesGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Scopes.SourceGen;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DcsvIo.D2.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits scope catalogs from
/// <c>contracts/auth-scopes/scopes.spec.json</c> via <c>AdditionalFiles</c>.
/// In the public assembly <c>DcsvIo.D2.Auth.Abstractions</c> it emits
/// <c>Scopes</c> over the values of the one spec file it is given. In another
/// assembly that sets the MSBuild properties <c>D2ScopesExtensionNamespace</c>
/// and <c>D2ScopesExtensionClassPrefix</c>, it emits the class named by the
/// prefix followed by <c>Scopes</c>, under that namespace, over the union of
/// every scope spec file the assembly lists.
/// </summary>
[Generator]
public sealed class ScopesGenerator : IIncrementalGenerator
{
    private const string _SPEC_FILE_NAME = "scopes.spec.json";
    private const string _TARGET_ASSEMBLY_NAME = "DcsvIo.D2.Auth.Abstractions";
    private const string _PUBLIC_ROOT_NAMESPACE = "DcsvIo.D2.Auth.Abstractions";
    private const string _PUBLIC_CLASS_NAME = "Scopes";
    private const string _ORG_TYPE_FQN = "DcsvIo.D2.Auth.Abstractions.OrgType";
    private const string _ROLE_FQN = "DcsvIo.D2.Auth.Abstractions.Role";

    private static readonly ExtensionTargetSettings sr_extensionTarget = new(
        NamespaceProperty: "D2ScopesExtensionNamespace",
        ClassPrefixProperty: "D2ScopesExtensionClassPrefix",
        InvalidNamespace: DiagnosticDescriptors.SR_InvalidExtensionNamespace,
        InvalidClassPrefix: DiagnosticDescriptors.SR_InvalidExtensionClassPrefix,
        ClassPrefixWithoutNamespace: DiagnosticDescriptors.SR_ExtensionClassPrefixWithoutNamespace);

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Filter AdditionalFiles to scopes.spec.json files.
        var specs = context.AdditionalTextsProvider
            .Where(static file => IsSpecFile(file.Path))
            .Select(static (file, ct) => new SpecFile(
                Path: file.Path,
                Content: file.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        // 2. Read the extension properties the host project sets.
        var targetOptions = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
                ExtensionTargetSelector.Read(provider.GlobalOptions, sr_extensionTarget));

        // 3. Combine with the compilation, so emission can depend on the assembly name
        //    and the OrgType / Role enum members can be read at codegen time. The
        //    grantedTo wildcard expansion therefore includes new members automatically.
        var combined = specs.Combine(context.CompilationProvider).Combine(targetOptions);

        // 4. On each pipeline run, call the emitter.
        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var ((specFiles, compilation), options) = tuple;

            var target = ExtensionTargetSelector.Select(
                compilation.AssemblyName, _TARGET_ASSEMBLY_NAME, options);
            ExtensionTargetSelector.Report(spc, target, sr_extensionTarget);

            if (target.Kind is not (ExtensionTargetKind.Public or ExtensionTargetKind.Extension))
                return;

            var isExtension = target.Kind == ExtensionTargetKind.Extension;
            var rootNamespace = isExtension ? target.Namespace : _PUBLIC_ROOT_NAMESPACE;
            var className = isExtension
                ? target.ClassPrefix + _PUBLIC_CLASS_NAME
                : _PUBLIC_CLASS_NAME;
            var sourceName = className + ".g.cs";

            if (specFiles.IsDefaultOrEmpty)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MissingSpecFile,
                    Location.None));
                spc.AddSource(
                    sourceName,
                    SourceText.From(EmptyScopesSource(rootNamespace, className), System.Text.Encoding.UTF8));
                return;
            }

            // Merge every scopes.spec.json in AdditionalFiles as a union of rows, and
            // report an error diagnostic when two rows have the same scope name. The
            // public assembly lists one file.
            var ordered = specFiles.OrderBy(s => s.Path, StringComparer.Ordinal).ToList();
            var loadResult = ScopeSpecLoader.Load(ordered[0].Path, ordered[0].Content);
            if (loadResult.Diagnostic is { } loadDiag)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(loadDiag));
                spc.AddSource(
                    sourceName,
                    SourceText.From(EmptyScopesSource(rootNamespace, className), System.Text.Encoding.UTF8));
                return;
            }

            var mergedScopes = loadResult.Spec!.Scopes.ToBuilder();
            var seenNames = new HashSet<string>(
                loadResult.Spec.Scopes.Select(s => s.Name),
                StringComparer.Ordinal);

            for (var i = 1; i < ordered.Count; i++)
            {
                var next = ScopeSpecLoader.Load(ordered[i].Path, ordered[i].Content);
                if (next.Diagnostic is { } nextDiag)
                {
                    spc.ReportDiagnostic(ToRoslynDiagnostic(nextDiag));
                    continue;
                }

                foreach (var scope in next.Spec!.Scopes)
                {
                    if (!seenNames.Add(scope.Name))
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.DuplicateScope,
                            Location.None,
                            scope.Name));
                        continue;
                    }

                    mergedScopes.Add(scope);
                }
            }

            var mergedSpec = new ScopesSpec(mergedScopes.ToImmutable());

            var orgTypeNames = ExtractEnumMembers(compilation, _ORG_TYPE_FQN);
            var roleNames = ExtractEnumMembers(compilation, _ROLE_FQN);

            var emitResult = ScopesEmitter.Emit(
                mergedSpec, orgTypeNames, roleNames, rootNamespace, className);
            foreach (var d in emitResult.Diagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(d));

            spc.AddSource(
                sourceName,
                SourceText.From(emitResult.GeneratedSource, System.Text.Encoding.UTF8));
        });
    }

    /// <summary>
    /// Reads enum member names from the compilation by fully-qualified type
    /// name. Returns the member names in source order. Returns an empty list
    /// when the compilation does not contain the type (for example mid-build, before
    /// auth-abstractions is fully resolved).
    /// </summary>
    private static IReadOnlyList<string> ExtractEnumMembers(
        Compilation compilation,
        string fullyQualifiedName)
    {
        var symbol = compilation.GetTypeByMetadataName(fullyQualifiedName);
        if (symbol is not { TypeKind: TypeKind.Enum })
            return [];

        var members = new List<string>();
        foreach (var member in symbol.GetMembers())
        {
            if (member is IFieldSymbol { IsConst: true, IsStatic: true } field)
                members.Add(field.Name);
        }

        return members;
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
        DiagnosticIds.UnknownEnumValue => DiagnosticDescriptors.UnknownEnumValue,
        DiagnosticIds.InvalidScopeName => DiagnosticDescriptors.InvalidScopeName,
        DiagnosticIds.DuplicateScope => DiagnosticDescriptors.DuplicateScope,
        DiagnosticIds.AnonImpersonationBlockedNoise =>
            DiagnosticDescriptors.AnonImpersonationBlockedNoise,
        DiagnosticIds.EmptyRoleArray => DiagnosticDescriptors.EmptyRoleArray,
        DiagnosticIds.TreePositionCollision => DiagnosticDescriptors.TreePositionCollision,
        DiagnosticIds.MissingGrantedTo => DiagnosticDescriptors.MissingGrantedTo,
        DiagnosticIds.MissingSpecFile => DiagnosticDescriptors.MissingSpecFile,
        DiagnosticIds.InternalFlagPrefixDisagreement =>
            DiagnosticDescriptors.InternalFlagPrefixDisagreement,
        _ => throw new InvalidOperationException($"Unknown EmitDiagnostic descriptor id '{id}'."),
    };

    private static string EmptyScopesSource(string rootNamespace, string className) =>
        "// <auto-generated>\n#nullable enable\n" +
        $"namespace {rootNamespace};\npublic static partial class {className} {{ }}\n";
}
