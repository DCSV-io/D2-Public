// -----------------------------------------------------------------------
// <copyright file="LocaleReferenceChecker.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Emitters;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using DcsvIo.D2.Geo.SourceGen.Spec;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Build-time check that every country's <c>primaryLocaleIETFBCP47Tag</c> and
/// every entry in <c>localeIETFBCP47Tags[]</c> resolves to an entry in
/// <c>locales.spec.json</c>. It reports
/// <see cref="DiagnosticIds.MissingLocaleReference"/> for each reference that
/// does not resolve, and the build fails on that diagnostic.
/// </summary>
internal static class LocaleReferenceChecker
{
    /// <summary>
    /// Validates every country's locale references against the locales
    /// catalog. Returns one diagnostic per (country, missing-locale) pair.
    /// </summary>
    /// <param name="context">The aggregate spec context.</param>
    /// <returns>Missing-reference diagnostics; empty when every ref resolves.</returns>
    public static ImmutableArray<EmitDiagnostic> Check(GeoSpecContext context)
    {
        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();

        if (context.Countries is not { } countries)
            return diagnostics.ToImmutable();

        // Build the validation set from the loaded locales catalog. The
        // loader leaves Locales null when locales.spec.json is absent or
        // fails to parse (each such failure is reported as a diagnostic).
        // In that case skip the check, so every country reference is not
        // reported as missing against an empty set.
        if (context.Locales is not { } locales)
            return diagnostics.ToImmutable();

        var validTags = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in locales.Entries)
        {
            if (entry.IetfBcp47Tag.Truthy())
                validTags.Add(entry.IetfBcp47Tag);
        }

        foreach (var country in countries.Entries)
        {
            if (country.Iso31661Alpha2Code.Falsey())
                continue;

            if (country.PrimaryLocaleIetfBcp47Tag.Truthy()
                && !validTags.Contains(country.PrimaryLocaleIetfBcp47Tag!))
            {
                diagnostics.Add(EmitDiagnostics.MissingLocaleReference(
                    country.Iso31661Alpha2Code,
                    country.PrimaryLocaleIetfBcp47Tag!));
            }

            foreach (var tag in country.LocaleIetfBcp47Tags)
            {
                if (tag.Falsey() || validTags.Contains(tag))
                    continue;

                diagnostics.Add(EmitDiagnostics.MissingLocaleReference(
                    country.Iso31661Alpha2Code, tag));
            }
        }

        return diagnostics.ToImmutable();
    }
}
