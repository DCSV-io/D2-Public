// -----------------------------------------------------------------------
// <copyright file="MqGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits the messaging registry
/// types from the mq-messages and mq-subscriptions spec files. It has two
/// target assemblies. Assembly <c>DcsvIo.D2.Messaging.Abstractions</c> gets
/// <c>MqMessages</c>, <c>MqMessagesRegistry</c>, <c>MqSubscriptions</c> and
/// <c>MqSubscriptionsRegistry</c> in <c>DcsvIo.D2.Messaging</c>. Another
/// catalog assembly that sets the MSBuild properties
/// <c>D2MqExtensionNamespace</c> and <c>D2MqExtensionClassPrefix</c> gets the
/// same types with the prefix before each class name, in that namespace, built
/// from the union of every spec file it lists.
/// </summary>
/// <remarks>
/// The union rejects a constant declared in two files of one catalog
/// (<c>D2MQ013</c>) and does not resolve it by path order, because which file
/// wins would then depend on where the repository sits on disk.
/// </remarks>
[Generator]
public sealed class MqGenerator : IIncrementalGenerator
{
    private const string _MESSAGES_FILE = "mq-messages.spec.json";
    private const string _SUBSCRIPTIONS_FILE = "mq-subscriptions.spec.json";
    private const string _TARGET_ASSEMBLY = "DcsvIo.D2.Messaging.Abstractions";
    private const string _ENCRYPTION_DOMAINS_FQN = "DcsvIo.D2.Encryption.EncryptionDomains";
    private const string _EXTRA_ENCRYPTION_DOMAINS_PROPERTY =
        "build_property.D2MqExtraEncryptionDomainsType";

    private static readonly ExtensionTargetSettings sr_extensionTarget = new(
        NamespaceProperty: "D2MqExtensionNamespace",
        ClassPrefixProperty: "D2MqExtensionClassPrefix",
        InvalidNamespace: DiagnosticDescriptors.SR_InvalidExtensionNamespace,
        InvalidClassPrefix: DiagnosticDescriptors.SR_InvalidExtensionClassPrefix,
        ClassPrefixWithoutNamespace: DiagnosticDescriptors.SR_ExtensionClassPrefixWithoutNamespace);

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var specs = context.AdditionalTextsProvider
            .Where(static file => IsMessagesSpec(file.Path) || IsSubscriptionsSpec(file.Path))
            .Select(static (file, ct) => new SpecFile(
                Path: file.Path,
                Content: file.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        var options = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
            {
                provider.GlobalOptions.TryGetValue(
                    _EXTRA_ENCRYPTION_DOMAINS_PROPERTY, out var extraDomainsType);

                var targetOptions = ExtensionTargetSelector.Read(
                    provider.GlobalOptions, sr_extensionTarget);

                return (
                    TargetOptions: targetOptions,
                    ExtraDomainsType: (extraDomainsType ?? string.Empty).Trim());
            });

        var combined = specs.Combine(context.CompilationProvider).Combine(options);

        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var ((specFiles, compilation), (targetOptions, extraDomainsType)) = tuple;

            var selection = ExtensionTargetSelector.Select(
                compilation.AssemblyName, _TARGET_ASSEMBLY, targetOptions);
            ExtensionTargetSelector.Report(spc, selection, sr_extensionTarget);

            var target = selection.Kind switch
            {
                ExtensionTargetKind.Public => MqEmitTarget.Public,
                ExtensionTargetKind.Extension =>
                    MqEmitTarget.Extension(selection.Namespace, selection.ClassPrefix),
                _ => null,
            };

            if (target is null)
                return;

            var messagesFiles = specFiles
                .Where(s => IsMessagesSpec(s.Path))
                .OrderBy(s => s.Path, StringComparer.Ordinal)
                .ToList();
            var subsFiles = specFiles
                .Where(s => IsSubscriptionsSpec(s.Path))
                .OrderBy(s => s.Path, StringComparer.Ordinal)
                .ToList();

            if (messagesFiles.Count == 0)
            {
                spc.ReportDiagnostic(ToRoslyn(EmitDiagnostics.MissingMessagesSpecFile()));
                EmitEmpty(spc, target);
                return;
            }

            if (subsFiles.Count == 0)
            {
                spc.ReportDiagnostic(ToRoslyn(EmitDiagnostics.MissingSubscriptionsSpecFile()));
                EmitEmpty(spc, target);
                return;
            }

            var messages = UnionMessages(spc, messagesFiles);

            if (messages is null)
            {
                EmitEmpty(spc, target);
                return;
            }

            var subscriptions = UnionSubscriptions(spc, subsFiles);

            if (subscriptions is null)
            {
                EmitEmpty(spc, target);
                return;
            }

            var encryptionDomains = ResolveEncryptionDomains(
                compilation,
                selection.Kind == ExtensionTargetKind.Extension ? extraDomainsType : string.Empty);

            var (messagesEmit, subsEmit) = MqEmitter.Emit(
                messages, subscriptions, encryptionDomains, target);

            foreach (var d in messagesEmit.Diagnostics) spc.ReportDiagnostic(ToRoslyn(d));
            foreach (var d in subsEmit.Diagnostics) spc.ReportDiagnostic(ToRoslyn(d));

            spc.AddSource(
                target.MessagesHint,
                SourceText.From(messagesEmit.GeneratedSource, System.Text.Encoding.UTF8));
            spc.AddSource(
                target.SubscriptionsHint,
                SourceText.From(subsEmit.GeneratedSource, System.Text.Encoding.UTF8));
        });
    }

    /// <summary>Loads every messages spec file and unions their entries.
    /// Returns null when any file failed to load or declared a constant a
    /// previous file already declared.</summary>
    /// <param name="spc">Diagnostic sink.</param>
    /// <param name="files">The spec files, ordered.</param>
    internal static MqMessagesSpec? UnionMessages(
        SourceProductionContext spc, IReadOnlyList<SpecFile> files)
    {
        var merged = new List<MqMessageEntry>();
        var declaredIn = new Dictionary<string, string>(StringComparer.Ordinal);
        var failed = false;

        foreach (var file in files)
        {
            var load = MqMessagesLoader.Load(file.Path, file.Content);

            if (load.Diagnostic is { } diag)
            {
                spc.ReportDiagnostic(ToRoslyn(diag));
                failed = true;
                continue;
            }

            foreach (var entry in load.Spec!.Messages)
            {
                if (declaredIn.TryGetValue(entry.Constant, out var firstFile))
                {
                    spc.ReportDiagnostic(ToRoslyn(EmitDiagnostics.CrossFileDuplicateConstant(
                        entry.Constant, firstFile, file.Path)));
                    failed = true;
                    continue;
                }

                declaredIn[entry.Constant] = file.Path;
                merged.Add(entry);
            }
        }

        return failed ? null : new MqMessagesSpec(merged.ToImmutableArray());
    }

    /// <summary>Loads every subscriptions spec file and unions their entries.
    /// Returns null when any file failed to load or declared a constant a
    /// previous file already declared.</summary>
    /// <param name="spc">Diagnostic sink.</param>
    /// <param name="files">The spec files, ordered.</param>
    internal static MqSubscriptionsSpec? UnionSubscriptions(
        SourceProductionContext spc, IReadOnlyList<SpecFile> files)
    {
        var merged = new List<MqSubscriptionEntry>();
        var declaredIn = new Dictionary<string, string>(StringComparer.Ordinal);
        var failed = false;

        foreach (var file in files)
        {
            var load = MqSubscriptionsLoader.Load(file.Path, file.Content);

            if (load.Diagnostic is { } diag)
            {
                spc.ReportDiagnostic(ToRoslyn(diag));
                failed = true;
                continue;
            }

            foreach (var entry in load.Spec!.Subscriptions)
            {
                if (declaredIn.TryGetValue(entry.Constant, out var firstFile))
                {
                    spc.ReportDiagnostic(ToRoslyn(EmitDiagnostics.CrossFileDuplicateConstant(
                        entry.Constant, firstFile, file.Path)));
                    failed = true;
                    continue;
                }

                declaredIn[entry.Constant] = file.Path;
                merged.Add(entry);
            }
        }

        return failed ? null : new MqSubscriptionsSpec(merged.ToImmutableArray());
    }

    /// <summary>The encryption-domain values a message entry may name: the
    /// framework domain catalog, plus the string constants of the type that the
    /// <c>D2MqExtraEncryptionDomainsType</c> property names, when the target is
    /// an extension and the compilation resolves that type. A domain in neither
    /// is reported as <c>UnknownEncryption</c>, so the public assembly's spec
    /// stays framework-only.</summary>
    /// <param name="compilation">The compilation being generated into.</param>
    /// <param name="extraDomainsType">The metadata name of the extra domain type,
    /// or the empty string for none.</param>
    private static IReadOnlyList<string> ResolveEncryptionDomains(
        Compilation compilation, string extraDomainsType)
    {
        var domains = new List<string>(
            ExtractConstStringValues(compilation, _ENCRYPTION_DOMAINS_FQN));

        if (extraDomainsType.Falsey())
            return domains;

        foreach (var value in ExtractConstStringValues(compilation, extraDomainsType))
        {
            if (!domains.Contains(value)) domains.Add(value);
        }

        return domains;
    }

    private static IReadOnlyList<string> ExtractConstStringValues(
        Compilation compilation, string fullyQualifiedName)
    {
        var symbol = compilation.GetTypeByMetadataName(fullyQualifiedName);
        if (symbol is null) return [];

        var values = new List<string>();
        foreach (var member in symbol.GetMembers())
        {
            if (member is IFieldSymbol { IsConst: true, ConstantValue: string v })
                values.Add(v);
        }

        return values;
    }

    private static bool IsMessagesSpec(string path) =>
        string.Equals(Path.GetFileName(path), _MESSAGES_FILE, StringComparison.OrdinalIgnoreCase);

    private static bool IsSubscriptionsSpec(string path) =>
        string.Equals(
            Path.GetFileName(path), _SUBSCRIPTIONS_FILE, StringComparison.OrdinalIgnoreCase);

    private static void EmitEmpty(SourceProductionContext spc, MqEmitTarget target)
    {
        var empty =
            "// <auto-generated>\n#nullable enable\n"
            + $"namespace {target.TargetNamespace};\n";
        spc.AddSource(target.MessagesHint, SourceText.From(empty, System.Text.Encoding.UTF8));
        spc.AddSource(target.SubscriptionsHint, SourceText.From(empty, System.Text.Encoding.UTF8));
    }

    private static Diagnostic ToRoslyn(EmitDiagnostic d)
    {
        var descriptor = ResolveDescriptor(d.DescriptorId);
        return Diagnostic.Create(descriptor, Location.None, d.Args.ToArray());
    }

    private static DiagnosticDescriptor ResolveDescriptor(string id) => id switch
    {
        DiagnosticIds.MalformedSpec => DiagnosticDescriptors.MalformedSpec,
        DiagnosticIds.MissingRequiredField => DiagnosticDescriptors.MissingRequiredField,
        DiagnosticIds.DuplicateConstant => DiagnosticDescriptors.DuplicateConstant,
        DiagnosticIds.UnknownEncryption => DiagnosticDescriptors.UnknownEncryption,
        DiagnosticIds.MissingPlaintextReason => DiagnosticDescriptors.MissingPlaintextReason,
        DiagnosticIds.UnknownPattern => DiagnosticDescriptors.UnknownPattern,
        DiagnosticIds.UnknownMessageType => DiagnosticDescriptors.UnknownMessageType,
        DiagnosticIds.UnknownExchangeType => DiagnosticDescriptors.UnknownExchangeType,
        DiagnosticIds.MissingMessagesSpecFile => DiagnosticDescriptors.MissingMessagesSpecFile,
        DiagnosticIds.MissingSubscriptionsSpecFile =>
            DiagnosticDescriptors.MissingSubscriptionsSpecFile,
        DiagnosticIds.InvalidConstantName => DiagnosticDescriptors.InvalidConstantName,
        DiagnosticIds.InvalidTieredRetryDuration =>
            DiagnosticDescriptors.InvalidTieredRetryDuration,
        DiagnosticIds.CrossFileDuplicateConstant =>
            DiagnosticDescriptors.SR_CrossFileDuplicateConstant,
        _ => throw new InvalidOperationException($"Unknown EmitDiagnostic descriptor id '{id}'."),
    };
}
