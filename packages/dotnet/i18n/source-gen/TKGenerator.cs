// -----------------------------------------------------------------------
// <copyright file="TKGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.I18n.SourceGen;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits translation-key catalogs from
/// <c>contracts/messages/*.json</c> via <c>AdditionalFiles</c>.
/// The public assembly <c>DcsvIo.D2.I18n.Keys</c> receives <c>TK</c> over the
/// messages of the catalogs it lists. Another assembly that sets the MSBuild
/// properties <c>D2TkExtensionNamespace</c> and <c>D2TkExtensionClassPrefix</c>
/// receives the class named by the prefix followed by <c>TK</c>, under that
/// namespace, over the union of the catalogs it lists.
/// </summary>
/// <remarks>
/// The generator filters AdditionalFiles to <c>.json</c> files whose
/// containing directory is named <c>messages</c>. That directory convention
/// matches the consuming csproj's
/// <c>&lt;AdditionalFiles Include="...contracts/messages/*.json" /&gt;</c>
/// glob and prevents unrelated AdditionalFiles from being treated as catalogs.
/// The extension class calls the internal <c>TKMessage</c> constructor, so an
/// assembly that sets the properties needs an <c>InternalsVisibleTo</c> grant
/// from <c>DcsvIo.D2.I18n.Abstractions</c>.
/// </remarks>
[Generator]
public sealed class TKGenerator : IIncrementalGenerator
{
    private const string _MESSAGES_DIRECTORY_NAME = "messages";
    private const string _EN_US_LOCALE = "en-US";

    private const string _PUBLIC_TARGET_ASSEMBLY = "DcsvIo.D2.I18n.Keys";
    private const string _PUBLIC_ROOT_NAMESPACE = "DcsvIo.D2.I18n";
    private const string _PUBLIC_CLASS_NAME = "TK";

    private static readonly ExtensionTargetSettings sr_extensionTarget = new(
        NamespaceProperty: "D2TkExtensionNamespace",
        ClassPrefixProperty: "D2TkExtensionClassPrefix",
        InvalidNamespace: DiagnosticDescriptors.SR_InvalidExtensionNamespace,
        InvalidClassPrefix: DiagnosticDescriptors.SR_InvalidExtensionClassPrefix,
        ClassPrefixWithoutNamespace: DiagnosticDescriptors.SR_ExtensionClassPrefixWithoutNamespace);

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Filter AdditionalFiles to JSON catalogs.
        var catalogs = context.AdditionalTextsProvider
            .Where(static file => IsCatalogPath(file.Path))
            .Select(static (file, ct) => new LocaleFile(
                Locale: Path.GetFileNameWithoutExtension(file.Path),
                Content: file.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        // 2. Read the extension properties the host project sets.
        var targetOptions = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
                ExtensionTargetSelector.Read(provider.GlobalOptions, sr_extensionTarget));

        // 3. Combine with the compilation so the assembly name selects the target.
        var combined = catalogs.Combine(context.CompilationProvider).Combine(targetOptions);

        // 4. On each pipeline run, call the emitter.
        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var ((files, compilation), options) = tuple;

            var target = ExtensionTargetSelector.Select(
                compilation.AssemblyName, _PUBLIC_TARGET_ASSEMBLY, options);
            ExtensionTargetSelector.Report(spc, target, sr_extensionTarget);

            // Any other assembly, such as a test project that also hosts the
            // generator, gets no output, which prevents CS0433 from two TK types.
            if (target.Kind is not (ExtensionTargetKind.Public or ExtensionTargetKind.Extension))
            {
                return;
            }

            var isExtension = target.Kind == ExtensionTargetKind.Extension;
            var rootNamespace = isExtension ? target.Namespace : _PUBLIC_ROOT_NAMESPACE;
            var className = isExtension
                ? target.ClassPrefix + _PUBLIC_CLASS_NAME
                : _PUBLIC_CLASS_NAME;
            var sourceName = className + ".g.cs";

            // Multi-dir AdditionalFiles: merge all en-US catalogs by key union.
            var enUsFiles = files
                .Where(f => string.Equals(f.Locale, _EN_US_LOCALE, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (enUsFiles.Count == 0 || enUsFiles.All(f => f.Content.Falsey()))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MissingEnUsJson,
                    Location.None));
                spc.AddSource(
                    sourceName,
                    SourceText.From(
                        EmptyTKSource(rootNamespace, className),
                        System.Text.Encoding.UTF8));
                return;
            }

            // Merge the en-US key sets of every catalog directory the assembly lists.
            var mergedEnUs = MergeLocaleJson(enUsFiles.Select(f => f.Content));

            var others = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var localeGroup in files
                .Where(f => !string.Equals(f.Locale, _EN_US_LOCALE, StringComparison.OrdinalIgnoreCase))
                .GroupBy(f => f.Locale, StringComparer.OrdinalIgnoreCase))
            {
                others[localeGroup.Key] = MergeLocaleJson(localeGroup.Select(f => f.Content));
            }

            var result = TKEmitter.Emit(mergedEnUs, others, rootNamespace, className);

            foreach (var d in result.Diagnostics)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(d));
            }

            spc.AddSource(
                sourceName,
                SourceText.From(result.GeneratedSource, System.Text.Encoding.UTF8));
        });
    }

    private static string MergeLocaleJson(IEnumerable<string> jsonContents)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        string? schema = null;
        foreach (var json in jsonContents)
        {
            if (json.Falsey())
            {
                continue;
            }

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, "$schema", StringComparison.Ordinal))
                {
                    schema ??= prop.Value.GetString();
                    continue;
                }

                if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    // The first value for a key wins, so the merge is stable
                    // when the public catalog comes before the private one.
                    if (!merged.ContainsKey(prop.Name))
                    {
                        merged[prop.Name] = prop.Value.GetString()!;
                    }
                }
            }
        }

        // Rebuild via JsonSerializer so newlines/quotes escape correctly.
        var ordered = new System.Collections.Generic.SortedDictionary<string, string>(
            StringComparer.Ordinal);
        if (schema is not null)
        {
            ordered["$schema"] = schema;
        }

        foreach (var kvp in merged.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            ordered[kvp.Key] = kvp.Value;
        }

        return System.Text.Json.JsonSerializer.Serialize(ordered);
    }

    private static bool IsCatalogPath(string path)
    {
        if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var dir = Path.GetDirectoryName(path);
        if (dir is null)
        {
            return false;
        }

        var dirName = Path.GetFileName(dir);
        return string.Equals(dirName, _MESSAGES_DIRECTORY_NAME, StringComparison.OrdinalIgnoreCase);
    }

    private static Diagnostic ToRoslynDiagnostic(EmitDiagnostic emitDiag)
    {
        var descriptor = ResolveDescriptor(emitDiag.DescriptorId);
        return Diagnostic.Create(descriptor, Location.None, emitDiag.Args.ToArray());
    }

    private static DiagnosticDescriptor ResolveDescriptor(string id) => id switch
    {
        DiagnosticIds.InvalidTranslationKey => DiagnosticDescriptors.InvalidTranslationKey,
        DiagnosticIds.MissingKeyInLocale => DiagnosticDescriptors.MissingKeyInLocale,
        DiagnosticIds.TranslationKeyCollision => DiagnosticDescriptors.TranslationKeyCollision,
        DiagnosticIds.OrphanKeyInLocale => DiagnosticDescriptors.OrphanKeyInLocale,
        DiagnosticIds.MissingEnUsJson => DiagnosticDescriptors.MissingEnUsJson,
        DiagnosticIds.MalformedJsonCatalog => DiagnosticDescriptors.MalformedJsonCatalog,
        _ => throw new InvalidOperationException($"Unknown EmitDiagnostic descriptor id '{id}'."),
    };

    private static string EmptyTKSource(string rootNamespace, string className) =>
        "// <auto-generated>\n#nullable enable\n" +
        $"namespace {rootNamespace};\npublic static partial class {className} {{ }}\n";
}
