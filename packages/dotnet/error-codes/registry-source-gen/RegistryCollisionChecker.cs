// -----------------------------------------------------------------------
// <copyright file="RegistryCollisionChecker.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.Registry.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Build-time safety gate that enforces these cross-catalog invariants across
/// all loaded error-code specs:
/// <list type="bullet">
///   <item>
///     No two catalogs declare the same <c>code</c> string; a duplicate is
///     reported as <see cref="RegistryDiagnosticIds.CrossCatalogDuplicateCode"/>
///     (<c>D2ERC004</c>).
///   </item>
///   <item>
///     Owner-aware reserved-namespace rule (<c>D2ERC005</c>): every code must
///     start with its catalog's full <see cref="RegistrySpecEntry.DomainPrefix"/>
///     (for example <c>GEN_</c>, <c>LIB_AUTH_</c>, <c>DOM_AUTH_</c>). A generic
///     (GEN) catalog must not carry another catalog's full prefix. Multi-segment
///     owner prefixes are not reduced to the first underscore segment.
///   </item>
/// </list>
/// When any diagnostic is reported, the registry must not be emitted.
/// </summary>
internal static class RegistryCollisionChecker
{
    /// <summary>
    /// Runs both cross-catalog checks against the full set of aggregated
    /// entries. Returns one diagnostic per violation found; empty when the
    /// catalog set is clean.
    /// </summary>
    /// <param name="entries">
    /// All registry entries aggregated from every spec, in the order they
    /// were loaded.
    /// </param>
    /// <returns>
    /// Diagnostics for cross-catalog collisions and reserved-namespace
    /// violations; empty when the catalog set is clean.
    /// </returns>
    public static ImmutableArray<EmitDiagnostic> Check(
        IReadOnlyList<RegistrySpecEntry> entries)
    {
        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();

        CheckCrossCollisions(entries, diagnostics);
        CheckReservedNamespace(entries, diagnostics);

        return diagnostics.ToImmutable();
    }

    private static void CheckCrossCollisions(
        IReadOnlyList<RegistrySpecEntry> entries,
        ImmutableArray<EmitDiagnostic>.Builder diagnostics)
    {
        // code -> first SpecFileName that declared it.
        var seenCodes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (!seenCodes.TryGetValue(entry.Code, out var firstSpec))
            {
                seenCodes[entry.Code] = entry.SpecFileName;
                continue;
            }

            diagnostics.Add(RegistryDiagnostics.CrossCatalogDuplicateCode(
                entry.Code, firstSpec, entry.SpecFileName));
        }
    }

    private static void CheckReservedNamespace(
        IReadOnlyList<RegistrySpecEntry> entries,
        ImmutableArray<EmitDiagnostic>.Builder diagnostics)
    {
        // Collect every non-GEN full DomainPrefix present, so a GEN catalog
        // cannot contain LIB_ or DOM_ codes.
        var foreignPrefixes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (!string.Equals(entry.Owner, "gen", StringComparison.Ordinal))
                foreignPrefixes.Add(entry.DomainPrefix);
        }

        foreach (var entry in entries)
        {
            // Every entry must start with its catalog's full DomainPrefix.
            if (!entry.Code.StartsWith(entry.DomainPrefix, StringComparison.Ordinal))
            {
                diagnostics.Add(RegistryDiagnostics.ReservedNamespaceViolation(
                    entry.Code,
                    entry.SpecFileName,
                    $"codes in owner '{entry.Owner}' domain '{entry.Domain}' must start "
                    + $"with the full DomainPrefix '{entry.DomainPrefix}'; "
                    + $"code '{entry.Code}' lacks the prefix"));
                continue;
            }

            // A GEN catalog must not start with any foreign LIB_ or DOM_ prefix. A
            // prefix equal to this entry's own is not foreign: a catalog
            // registered with the GEN_ prefix under another owner would
            // otherwise report every legitimate GEN code.
            if (string.Equals(entry.Owner, "gen", StringComparison.Ordinal))
            {
                foreach (var foreign in foreignPrefixes)
                {
                    if (string.Equals(foreign, entry.DomainPrefix, StringComparison.Ordinal))
                        continue;

                    if (entry.Code.StartsWith(foreign, StringComparison.Ordinal))
                    {
                        diagnostics.Add(RegistryDiagnostics.ReservedNamespaceViolation(
                            entry.Code,
                            entry.SpecFileName,
                            $"the generic (GEN) catalog must not contain domain-prefixed "
                            + $"codes; code '{entry.Code}' starts with foreign prefix "
                            + $"'{foreign}'"));
                        break;
                    }
                }
            }
        }
    }
}
