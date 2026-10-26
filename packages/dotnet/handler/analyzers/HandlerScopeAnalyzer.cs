// -----------------------------------------------------------------------
// <copyright file="HandlerScopeAnalyzer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler.Analyzers;

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Enforces handler scope binding as defense-in-depth: a class implementing an
/// <c>I{Op}Handler</c> interface that derives from
/// <c>DcsvIo.D2.Handler.Abstractions.IHandler&lt;TInput, TOutput&gt;</c> must be
/// named <c>{Op}Handler</c>, be <c>partial</c>, and carry a sealed
/// <c>DefaultOptions</c> override or <c>[D2GeneratedHandlerScope]</c>. Only a
/// test assembly is exempt, identified by an exact suffix on its assembly
/// name.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HandlerScopeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Diagnostic id: wrong implementer type name.</summary>
    public const string D2HND001 = "D2HND001";

    /// <summary>Diagnostic id: product handler must be partial.</summary>
    public const string D2HND002 = "D2HND002";

    /// <summary>Diagnostic id: missing sealed DefaultOptions / gen marker.</summary>
    public const string D2HND003 = "D2HND003";

    /// <summary>The platform handler contract every product I{Op}Handler derives from.</summary>
    private const string _HANDLER_CONTRACT_METADATA_NAME =
        "DcsvIo.D2.Handler.Abstractions.IHandler`2";

    /// <summary>The generated marker that binds a handler's scope.</summary>
    private const string _SCOPE_MARKER_METADATA_NAME =
        "DcsvIo.D2.Handler.Abstractions.D2GeneratedHandlerScopeAttribute";

    private static readonly string[] sr_testAssemblySuffixes =
        [".Tests", ".UnitTests", ".IntegrationTests"];

    private static readonly DiagnosticDescriptor sr_wrongName = new(
        id: D2HND001,
        title: "Handler implementer must be named {Op}Handler",
        messageFormat: "Type '{0}' implements '{1}' but must be named '{2}'",
        category: "D2.Handler.Scope",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
            "Only the product {Op}Handler type may implement I{Op}Handler (bypass prevention).");

    private static readonly DiagnosticDescriptor sr_mustBePartial = new(
        id: D2HND002,
        title: "Product handler must be partial",
        messageFormat:
            "Type '{0}' implements a product I{{Op}}Handler and must be declared partial",
        category: "D2.Handler.Scope",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
            "Scope DefaultOptions is sealed on a generated or hand-written Scope partial; "
            + "the handler type must be partial.");

    private static readonly DiagnosticDescriptor sr_missingScope = new(
        id: D2HND003,
        title: "Product handler must declare sealed DefaultOptions or D2GeneratedHandlerScope",
        messageFormat:
            "Type '{0}' implements a product I{{Op}}Handler but has neither a sealed "
            + "DefaultOptions override nor [D2GeneratedHandlerScope]",
        category: "D2.Handler.Scope",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
            "Every product I{Op}Handler implementer must bind ScopeRequirement / InfraExempt "
            + "via sealed DefaultOptions or the generated marker attribute.");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(sr_wrongName, sr_mustBePartial, sr_missingScope);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol type || type.TypeKind != TypeKind.Class)
            return;

        if (IsCarvedOut(context.Compilation))
            return;

        var handlerContract = context.Compilation
            .GetTypeByMetadataName(_HANDLER_CONTRACT_METADATA_NAME);

        if (handlerContract is null)
            return;

        foreach (var iface in type.AllInterfaces)
        {
            if (!TryGetOpHandlerContract(iface, handlerContract, out var expectedHandlerName))
                continue;

            var location = type.Locations.FirstOrDefault() ?? Location.None;

            if (!string.Equals(type.Name, expectedHandlerName, StringComparison.Ordinal))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        sr_wrongName,
                        location,
                        type.Name,
                        iface.Name,
                        expectedHandlerName));
            }

            if (!TypeIsPartial(type))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(sr_mustBePartial, location, type.Name));
            }

            if (!HasScopeBinding(type, context.Compilation))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(sr_missingScope, location, type.Name));
            }

            // One I{Op}Handler contract is enough; a second one would repeat
            // the same diagnostics.
            break;
        }
    }

    private static bool TryGetOpHandlerContract(
        INamedTypeSymbol iface,
        INamedTypeSymbol handlerContract,
        out string expectedHandlerName)
    {
        expectedHandlerName = string.Empty;
        var name = iface.Name;

        if (name.Length < 9
            || name[0] != 'I'
            || !name.EndsWith("Handler", StringComparison.Ordinal))
        {
            return false;
        }

        if (!DerivesFromHandlerContract(iface, handlerContract))
            return false;

        // I{Op}Handler -> {Op}Handler
        expectedHandlerName = name.Substring(1);
        return true;
    }

    /// <summary>
    /// Whether the candidate interface derives from the platform handler
    /// contract, so an unrelated <c>I{Something}Handler</c> that does not
    /// derive from it is not treated as a product operation contract.
    /// </summary>
    private static bool DerivesFromHandlerContract(
        INamedTypeSymbol iface,
        INamedTypeSymbol handlerContract)
    {
        foreach (var baseInterface in iface.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    baseInterface.OriginalDefinition,
                    handlerContract))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Only a test assembly is exempt, matched by an exact suffix on the
    /// assembly name. A product type cannot become exempt through its own
    /// name.
    /// </summary>
    private static bool IsCarvedOut(Compilation compilation)
    {
        var assemblyName = compilation.AssemblyName ?? string.Empty;

        foreach (var suffix in sr_testAssemblySuffixes)
        {
            if (assemblyName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool TypeIsPartial(INamedTypeSymbol type)
    {
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            var node = reference.GetSyntax();

            if (node is Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax tds
                && tds.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasScopeBinding(INamedTypeSymbol type, Compilation compilation)
    {
        // The marker is matched by metadata name rather than by simple name:
        // any assembly can declare a type called D2GeneratedHandlerScopeAttribute,
        // and one that did would satisfy D2HND003 while binding no
        // ScopeRequirement and taking no generated Scope partial.
        var marker = compilation.GetTypeByMetadataName(_SCOPE_MARKER_METADATA_NAME);

        if (marker is not null)
        {
            foreach (var attr in type.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(
                        attr.AttributeClass?.OriginalDefinition,
                        marker))
                {
                    return true;
                }
            }
        }

        // Walk the type and its partials for a sealed override of the
        // DefaultOptions property. On a sealed handler class, a method-level
        // 'sealed' is redundant in the language (ReSharper
        // RedundantSealedModifier), so an override alone is accepted when the
        // type is sealed.
        foreach (var member in type.GetMembers("DefaultOptions"))
        {
            if (member is not IPropertySymbol prop)
                continue;

            if (prop.IsOverride && (prop.IsSealed || type.IsSealed))
                return true;
        }

        return false;
    }
}
