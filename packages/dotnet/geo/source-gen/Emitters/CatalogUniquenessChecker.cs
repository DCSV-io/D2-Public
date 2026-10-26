// -----------------------------------------------------------------------
// <copyright file="CatalogUniquenessChecker.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Emitters;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using DcsvIo.D2.Geo.SourceGen.Spec;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Enforces catalog uniqueness as a build-time check on the countries
/// catalog and, per country, the subdivisions catalog: the matchable name
/// fields of an entity must not normalize to the same form as those of another
/// entity in the same catalog. The check normalizes each name field (NFD,
/// strip Mn, invariant casefold, collapse whitespace). It reports
/// <see cref="DiagnosticIds.DuplicateNormalizedName"/> on each collision, and
/// the build fails on that diagnostic when it is enabled (it is disabled by
/// default). The runtime name resolver is what fails closed (NotFound) on a
/// collision instead of letting the first match win in
/// <c>IGeoNameResolver</c>.
/// </summary>
internal static class CatalogUniquenessChecker
{
    /// <summary>
    /// Runs the uniqueness check across every loaded catalog. Returns one
    /// diagnostic per (catalog, normalized-name) collision.
    /// </summary>
    /// <param name="context">The aggregate spec context.</param>
    /// <returns>Collision diagnostics; empty when every catalog is clean.</returns>
    public static ImmutableArray<EmitDiagnostic> Check(GeoSpecContext context)
    {
        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();

        if (context.Countries is { } countries)
        {
            CheckCatalog(
                specName: "countries.spec.json",
                entries: ExtractCountryNames(countries.Entries),
                diagnostics: diagnostics);
        }

        if (context.Subdivisions is { } subdivisions)
        {
            // Per-country bucketed check - "Georgia" is both US-GA and the
            // country GE, "Northern Province" is a legitimate name in several
            // countries. Resolver scope is mandatory per-country (see
            // IGeoNameResolver.TryResolveSubdivisionByName), so collisions are
            // only meaningful within the same parent-country bucket.
            CheckSubdivisionsPerCountry(subdivisions.Entries, diagnostics);
        }

        // The name resolver covers countries and subdivisions only. The
        // other catalogs are addressable solely by their typed code, so
        // duplicate display names within them cannot make the resolver
        // pick between two matches. Skipping them also avoids reporting
        // legitimate cross-language locale name overlaps (for example
        // "Afar" as the name of several regional locale variants).
        _ = context.Currencies;
        _ = context.Languages;
        _ = context.Locales;
        _ = context.Timezones;
        _ = context.GeopoliticalEntities;

        return diagnostics.ToImmutable();
    }

    private static void CheckCatalog(
        string specName,
        IEnumerable<(string EntityId, IEnumerable<string> Names)> entries,
        ImmutableArray<EmitDiagnostic>.Builder diagnostics)
    {
        var normalizedToEntities =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var (entityId, names) in entries)
        {
            if (entityId.Falsey())
                continue;

            // Deduplicate per-entity (some entities have the same name in
            // multiple fields - DisplayName == OfficialName is common).
            var perEntitySeen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in names)
            {
                if (name.Falsey())
                    continue;

                var normalized = Normalize(name);
                if (normalized.Falsey())
                    continue;

                if (!perEntitySeen.Add(normalized))
                    continue;

                if (!normalizedToEntities.TryGetValue(normalized, out var owners))
                {
                    owners = new List<string>();
                    normalizedToEntities[normalized] = owners;
                }

                owners.Add(entityId);
            }
        }

        foreach (var kvp in normalizedToEntities)
        {
            if (kvp.Value.Count < 2)
                continue;

            // Sort the colliding entity ids so the diagnostic text is the same
            // on every run.
            kvp.Value.Sort(StringComparer.Ordinal);
            diagnostics.Add(EmitDiagnostics.DuplicateNormalizedName(
                specName, kvp.Key, string.Join(", ", kvp.Value)));
        }
    }

    private static IEnumerable<(string EntityId, IEnumerable<string> Names)> ExtractCountryNames(
        IReadOnlyList<CountrySpec> entries)
    {
        foreach (var entry in entries)
        {
            yield return (entry.Iso31661Alpha2Code, NamesOf(entry));
        }
    }

    private static IEnumerable<string> NamesOf(CountrySpec entry)
    {
        // Display-form names only. Codes (alpha-2 / alpha-3) are addressable
        // by their typed identifiers and are never typed free-form into a
        // resolver query.
        yield return entry.DisplayName;
        yield return entry.OfficialName;
        if (entry.EndonymDisplayName.Truthy())
            yield return entry.EndonymDisplayName!;
    }

    private static void CheckSubdivisionsPerCountry(
        IReadOnlyList<SubdivisionSpec> entries,
        ImmutableArray<EmitDiagnostic>.Builder diagnostics)
    {
        // Bucket per parent country, then run the same collision detection
        // against each country's subdivision set.
        var byCountry = new Dictionary<string, List<SubdivisionSpec>>(
            StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (entry.CountryIso31661Alpha2Code.Falsey())
                continue;

            if (!byCountry.TryGetValue(entry.CountryIso31661Alpha2Code, out var bucket))
            {
                bucket = new List<SubdivisionSpec>();
                byCountry[entry.CountryIso31661Alpha2Code] = bucket;
            }

            bucket.Add(entry);
        }

        foreach (var kvp in byCountry)
        {
            CheckCatalog(
                specName: $"subdivisions.spec.json [country {kvp.Key}]",
                entries: ProjectSubdivisionNames(kvp.Value),
                diagnostics: diagnostics);
        }
    }

    private static IEnumerable<(string EntityId, IEnumerable<string> Names)>
        ProjectSubdivisionNames(IReadOnlyList<SubdivisionSpec> entries)
    {
        foreach (var entry in entries)
        {
            yield return (entry.Iso31662Code, NamesOf(entry));
        }
    }

    private static IEnumerable<string> NamesOf(SubdivisionSpec entry)
    {
        // Display-form names only. ShortCode and Iso31662Code are addressable
        // by their typed identifiers and are never typed free-form into a
        // resolver query.
        yield return entry.DisplayName;
        yield return entry.OfficialName;
        if (entry.EndonymDisplayName.Truthy())
            yield return entry.EndonymDisplayName!;
    }

    private static string Normalize(string input)
    {
        if (input.Falsey())
            return string.Empty;

        var decomposed = input.Normalize(NormalizationForm.FormD);

        var stripped = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                stripped.Append(ch);
        }

        var lowered = CultureInfo.InvariantCulture.TextInfo.ToLower(stripped.ToString());

        // Collapse whitespace + trim.
        var trimmed = lowered.AsSpan().Trim();
        if (trimmed.IsEmpty)
            return string.Empty;

        var collapsed = new StringBuilder(trimmed.Length);
        var previousWasSpace = false;
        foreach (var ch in trimmed)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasSpace)
                {
                    collapsed.Append(' ');
                    previousWasSpace = true;
                }
            }
            else
            {
                collapsed.Append(ch);
                previousWasSpace = false;
            }
        }

        return collapsed.ToString();
    }
}
