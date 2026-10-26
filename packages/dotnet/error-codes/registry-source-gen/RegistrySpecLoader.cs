// -----------------------------------------------------------------------
// <copyright file="RegistrySpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.Registry.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using DcsvIo.D2.ErrorCodes.SourceGen;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Aggregates all error-code spec files provided through <c>AdditionalFiles</c>
/// into a flat list of <see cref="RegistrySpecEntry"/> records. Each spec
/// is parsed using the shared <see cref="ErrorCodeSpecLoader"/> and enriched
/// with owner-aware catalog identity (Owner, DomainToken and DomainPrefix). It
/// never derives the domain from the filename alone for multi-segment catalogs.
/// </summary>
internal static class RegistrySpecLoader
{
    private const string _GENERIC_SPEC_NAME = "error-codes.spec.json";
    private const string _DOMAIN_SPEC_SUFFIX = "-error-codes.spec.json";

    /// <summary>
    /// Built-in catalog identities keyed by filename (GEN and public LIB).
    /// DOM catalogs are added through the
    /// <c>extraIdentities</c> argument to <see cref="LoadAll"/> /
    /// <see cref="ResolveIdentity"/>; they are never hard-coded here.
    /// A catalog in neither map is rejected with <c>D2ERC008</c>.
    /// </summary>
    private static readonly Dictionary<string, CatalogIdentity> sr_identities =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["error-codes.spec.json"] = new("gen", "common", "GEN_"),
            ["auth-error-codes.spec.json"] = new("lib", "auth", "LIB_AUTH_"),
            ["pagination-error-codes.spec.json"] = new("lib", "pagination", "LIB_PAGINATION_"),
            ["messaging-error-codes.spec.json"] = new("lib", "messaging", "LIB_MESSAGING_"),
        };

    /// <summary>
    /// Parses all provided spec files and returns the aggregated registry
    /// entries plus any load-time diagnostics.
    /// </summary>
    /// <param name="specFiles">
    /// All spec files provided through <c>AdditionalFiles</c>. The loader
    /// distinguishes the generic spec from domain specs by filename.
    /// </param>
    /// <param name="diagnostics">
    /// Builder to append load-time diagnostics to. An error-severity
    /// diagnostic means emission must not proceed.
    /// </param>
    /// <param name="extraIdentities">
    /// Optional host-supplied catalog identity map for catalogs the built-in
    /// map lacks. Pass <see langword="null"/> when the built-in map is enough.
    /// </param>
    /// <returns>Aggregated entries in the order they were loaded.</returns>
    public static IReadOnlyList<RegistrySpecEntry> LoadAll(
        ImmutableArray<SpecFile> specFiles,
        ImmutableArray<EmitDiagnostic>.Builder diagnostics,
        IReadOnlyDictionary<string, CatalogIdentity>? extraIdentities = null)
    {
        var entries = new List<RegistrySpecEntry>();

        // Sort by spec filename for deterministic output order.
        var sorted = new List<SpecFile>(specFiles);
        sorted.Sort(static (a, b) =>
            string.Compare(
                Path.GetFileName(a.Path),
                Path.GetFileName(b.Path),
                StringComparison.Ordinal));

        foreach (var spec in sorted)
        {
            var fileName = Path.GetFileName(spec.Path);

            if (!IsErrorCodeSpec(fileName))
                continue;

            var identity = ResolveIdentity(fileName, extraIdentities);

            if (identity is null)
            {
                diagnostics.Add(RegistryDiagnostics.UnregisteredCatalog(fileName));
                continue;
            }

            var loadResult = ErrorCodeSpecLoader.Load(
                spec.Path,
                spec.Content,
                malformedSpecId: RegistryDiagnosticIds.MalformedRegistrySpec);

            if (loadResult.Diagnostic is { } diag)
            {
                diagnostics.Add(diag);
                continue;
            }

            foreach (var entry in loadResult.Spec!.ErrorCodes)
            {
                // All factory fields are required in the registry. Every valid
                // spec entry will have them; guard defensively.
                if (entry.Category is null || entry.UserMessageKey is null
                    || entry.FactoryName is null || entry.FactoryShape is null)
                {
                    diagnostics.Add(RegistryDiagnostics.MalformedRegistrySpec(
                        fileName,
                        $"entry '{entry.Code}' is missing required registry fields "
                        + "(category / userMessageKey / factoryName / factoryShape)"));
                    continue;
                }

                entries.Add(new RegistrySpecEntry(
                    Code: entry.Code,
                    HttpStatus: entry.HttpStatus,
                    Category: entry.Category,
                    UserMessageKey: entry.UserMessageKey,
                    FactoryName: entry.FactoryName,
                    FactoryShape: entry.FactoryShape,
                    Doc: entry.Doc,
                    Domain: identity.DomainToken,
                    Owner: identity.Owner,
                    DomainPrefix: identity.DomainPrefix,
                    SpecFileName: fileName,
                    IsDeprecated: entry.Deprecated));
            }
        }

        return entries;
    }

    /// <summary>
    /// Returns <see langword="true"/> for spec files the registry generator
    /// owns: either the generic <c>error-codes.spec.json</c> or any
    /// <c>*-error-codes.spec.json</c> domain spec.
    /// </summary>
    /// <param name="fileName">The filename (without path) to test.</param>
    /// <returns><see langword="true"/> when the file is an error-code spec.</returns>
    public static bool IsErrorCodeSpec(string? fileName)
    {
        if (fileName is null)
            return false;

        return string.Equals(fileName, _GENERIC_SPEC_NAME, StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(_DOMAIN_SPEC_SUFFIX, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves owner-aware catalog identity for a registered filename.
    /// </summary>
    /// <param name="fileName">Spec filename.</param>
    /// <param name="extraIdentities">
    /// Optional host-supplied map. Checked after the built-in map.
    /// </param>
    /// <returns>
    /// Owner, domain token, and full DomainPrefix, or <see langword="null"/>
    /// when the filename is in neither map.
    /// </returns>
    public static CatalogIdentity? ResolveIdentity(
        string fileName,
        IReadOnlyDictionary<string, CatalogIdentity>? extraIdentities = null)
    {
        if (sr_identities.TryGetValue(fileName, out var known))
            return known;

        if (extraIdentities is not null
            && (extraIdentities.TryGetValue(fileName, out known)
                || TryGetExtraIgnoreCase(extraIdentities, fileName, out known)))
        {
            return known;
        }

        return null;
    }

    /// <summary>
    /// Derives the domain token from a spec filename.
    /// Prefer <see cref="ResolveIdentity"/> for Owner and DomainPrefix.
    /// </summary>
    /// <param name="fileName">The spec filename.</param>
    /// <param name="extraIdentities">Optional host-supplied map.</param>
    /// <returns>
    /// The domain token (lowercase), or <see langword="null"/> when the
    /// filename is in neither map.
    /// </returns>
    public static string? DeriveDomain(
        string fileName,
        IReadOnlyDictionary<string, CatalogIdentity>? extraIdentities = null) =>
        ResolveIdentity(fileName, extraIdentities)?.DomainToken;

    private static bool TryGetExtraIgnoreCase(
        IReadOnlyDictionary<string, CatalogIdentity> extras,
        string fileName,
        out CatalogIdentity identity)
    {
        foreach (var pair in extras)
        {
            if (string.Equals(pair.Key, fileName, StringComparison.OrdinalIgnoreCase))
            {
                identity = pair.Value;
                return true;
            }
        }

        identity = null!;
        return false;
    }

    /// <summary>Owner, DomainToken and full DomainPrefix for one catalog file.</summary>
    /// <param name="Owner">gen | lib | dom.</param>
    /// <param name="DomainToken">Business domain token.</param>
    /// <param name="DomainPrefix">Full wire prefix.</param>
    public sealed record CatalogIdentity(
        string Owner,
        string DomainToken,
        string DomainPrefix);
}
