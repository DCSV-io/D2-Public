// -----------------------------------------------------------------------
// <copyright file="TimezoneDataEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Emitters.Default;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using DcsvIo.D2.Geo.SourceGen.Spec;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Emits the per-timezone data: one shape per entity, with reference cycles
/// resolved through friend-assembly <c>internal set</c> and a two-pass
/// populate. The output is:
/// <list type="bullet">
///   <item><description>
///     <c>TimezoneLookup.g.cs</c>: the first pass creates every
///     <c>Timezone</c> record with scalars and code-rep fields populated and
///     nav refs <c>null</c> or empty, and the wire-nav step sets each
///     record's <c>PrimaryCountry</c> and <c>CoApplicableCountries</c>
///     through the friend-assembly <c>internal set</c> accessors.
///   </description></item>
///   <item><description>
///     <c>TimezonesNested.g.cs</c>: the <c>Timezones.America.New_York</c>
///     nested static-class hierarchy. Each leaf is a <c>TimezoneCode</c>
///     constant.
///   </description></item>
/// </list>
/// </summary>
internal static class TimezoneDataEmitter
{
    private const string _NAMESPACE = DefaultEmitterHelpers.DefaultNamespace;

    /// <summary>
    /// Emits both timezone data files. Empty when the spec context lacks
    /// a timezones catalog.
    /// </summary>
    /// <param name="context">The aggregate spec context.</param>
    /// <returns>The lookup + nested emit results.</returns>
    public static ImmutableArray<EmitResult> EmitAll(GeoSpecContext context)
    {
        if (context.Timezones is not { } timezonesEnv)
            return ImmutableArray<EmitResult>.Empty;

        var entries = SortByIana(timezonesEnv.Entries);

        // Build the valid CountryCode member set so the emitter can skip
        // timezones that reference countries not in the catalog.
        var validCountryMembers = new HashSet<string>(System.StringComparer.Ordinal);
        if (context.Countries is { } countriesEnv)
        {
            foreach (var c in countriesEnv.Entries)
            {
                if (c.Iso31661Alpha2Code.Truthy()
                    && DefaultEmitterHelpers.IsValidIdentifier(c.Iso31661Alpha2Code))
                    validCountryMembers.Add(c.Iso31661Alpha2Code);
            }
        }

        return ImmutableArray.Create(
            EmitLookup(entries, validCountryMembers),
            EmitNested(entries));
    }

    private static EmitResult EmitLookup(
        IReadOnlyList<TimezoneSpec> entries,
        HashSet<string> validCountryMembers)
    {
        var sb = new StringBuilder();
        DefaultEmitterHelpers.AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Frozen;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine($"using {DefaultEmitterHelpers.AbstractionsNamespace};");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// O(1) lookup tables over the timezone catalog. First pass (static");
        sb.AppendLine(
            "/// ctor) constructs every <see cref=\"Timezone\"/> record with");
        sb.AppendLine(
            "/// scalars and code-rep fields populated and nav refs <c>null</c> /");
        sb.AppendLine(
            "/// empty; wire-nav step (<see cref=\"WireNav\"/>) mutates each");
        sb.AppendLine(
            "/// record's <c>PrimaryCountry</c> + <c>CoApplicableCountries</c>");
        sb.AppendLine(
            "/// navs via friend-assembly <c>internal set</c> visibility.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class TimezoneLookup");
        sb.AppendLine("{");
        sb.AppendLine(
            "    /// <summary>Timezone records indexed by typed "
            + "<see cref=\"TimezoneCode\"/>.</summary>");
        sb.AppendLine(
            "    public static readonly FrozenDictionary<TimezoneCode, Timezone> ByCode;");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>All Timezone records in spec order.</summary>");
        sb.AppendLine(
            "    public static readonly IReadOnlyList<Timezone> All;");
        sb.AppendLine();

        sb.AppendLine("    static TimezoneLookup()");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        // First pass: construct every Timezone record "
            + "(nav refs null/empty until wire-nav).");
        sb.AppendLine("        var byCode = new Dictionary<TimezoneCode, Timezone>();");
        foreach (var entry in entries)
        {
            var iana = DefaultEmitterHelpers.EscapeStringLiteral(entry.IanaIdentifier);
            sb.AppendLine(
                $"        byCode[TimezoneCode.FromString(\"{iana}\")] = "
                + TimezoneRecordLiteral(entry, validCountryMembers) + ";");
        }

        sb.AppendLine();
        sb.AppendLine("        ByCode = byCode.ToFrozenDictionary();");
        sb.AppendLine();
        sb.AppendLine("        var all = new Timezone[]");
        sb.AppendLine("        {");
        foreach (var entry in entries)
        {
            var iana = DefaultEmitterHelpers.EscapeStringLiteral(entry.IanaIdentifier);
            sb.AppendLine(
                $"            byCode[TimezoneCode.FromString(\"{iana}\")],");
        }

        sb.AppendLine("        };");
        sb.AppendLine("        All = all;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // -------- WireNav --------
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Wire-nav step of the two-pass populate pattern. Invoked exactly");
        sb.AppendLine(
            "    /// once by <c>GeoDataInitializer</c> after every catalog's first-pass");
        sb.AppendLine(
            "    /// static ctor has run. Mutates each timezone's <c>PrimaryCountry</c>");
        sb.AppendLine(
            "    /// + <c>CoApplicableCountries</c> navs via the friend-assembly");
        sb.AppendLine("    /// <c>internal set</c> accessors.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    internal static void WireNav()");
        sb.AppendLine("    {");
        sb.AppendLine("        foreach (var tz in All)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (tz.PrimaryCountryIso31661Alpha2Code is { } pcc");
        sb.AppendLine("                && CountryLookup.ByCode.TryGetValue(pcc, out var primary))");
        sb.AppendLine("            {");
        sb.AppendLine("                tz.PrimaryCountry = primary;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (tz.CoApplicableCountryIso31661Alpha2Codes.Count > 0)");
        sb.AppendLine("            {");
        sb.AppendLine(
            "                var list = new List<Country>("
            + "tz.CoApplicableCountryIso31661Alpha2Codes.Count);");
        sb.AppendLine(
            "                foreach (var cc in "
            + "tz.CoApplicableCountryIso31661Alpha2Codes)");
        sb.AppendLine("                {");
        sb.AppendLine(
            "                    if (CountryLookup.ByCode.TryGetValue("
            + "cc, out var country))");
        sb.AppendLine("                        list.Add(country);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                tz.CoApplicableCountries = list.ToArray();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return new EmitResult(
            HintName: "TimezoneLookup.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty);
    }

    private static string TimezoneRecordLiteral(
        TimezoneSpec entry,
        HashSet<string> validCountryMembers)
    {
        var iana = DefaultEmitterHelpers.EscapeStringLiteral(entry.IanaIdentifier);
        var displayName = DefaultEmitterHelpers.EscapeStringLiteral(entry.DisplayName);
        var dstOffset = DefaultEmitterHelpers.IntLiteralOrNull(entry.CurrentDstOffsetMinutes);
        var stdAbbrev = DefaultEmitterHelpers.EscapeStringLiteral(entry.CurrentStdAbbrev);
        var dstAbbrev = DefaultEmitterHelpers.StringLiteralOrNull(entry.CurrentDstAbbrev);

        var sb = new StringBuilder();
        sb.Append("new Timezone { ");
        sb.Append($"IanaName = TimezoneCode.FromString(\"{iana}\"), ");
        sb.Append($"DisplayName = \"{displayName}\", ");
        sb.Append(
            "LocalizedDisplayNames = "
            + "new System.Collections.Generic.Dictionary<string, string>(), ");
        sb.Append($"CurrentStdOffsetMinutes = {entry.CurrentStdOffsetMinutes}, ");
        sb.Append($"CurrentDstOffsetMinutes = {dstOffset}, ");
        sb.Append($"CurrentStdAbbrev = \"{stdAbbrev}\", ");
        sb.Append($"CurrentDstAbbrev = {dstAbbrev}, ");

        if (entry.CountryIso31661Alpha2Code.Truthy()
            && validCountryMembers.Contains(entry.CountryIso31661Alpha2Code!))
        {
            sb.Append(
                $"PrimaryCountryIso31661Alpha2Code = "
                + $"CountryCode.{entry.CountryIso31661Alpha2Code}, ");
        }
        else
        {
            sb.Append("PrimaryCountryIso31661Alpha2Code = null, ");
        }

        // CoApplicableCountryIso31661Alpha2Codes - required set (init).
        var coCodes = new List<string>();
        foreach (var c in entry.CoApplicableCountryIso31661Alpha2Codes)
        {
            if (c.Falsey() || !validCountryMembers.Contains(c))
                continue;

            coCodes.Add(c);
        }

        if (coCodes.Count == 0)
        {
            sb.Append("CoApplicableCountryIso31661Alpha2Codes = FrozenSet<CountryCode>.Empty, ");
        }
        else
        {
            sb.Append("CoApplicableCountryIso31661Alpha2Codes = new HashSet<CountryCode> { ");
            var first = true;
            foreach (var c in coCodes)
            {
                if (!first)
                    sb.Append(", ");

                sb.Append($"CountryCode.{c}");
                first = false;
            }

            sb.Append(" }.ToFrozenSet(), ");
        }

        sb.Append("Selectable = false, ");

        sb.Append("Aliases = new string[]");
        sb.Append(" { ");
        var firstAlias = true;
        foreach (var a in entry.Aliases)
        {
            if (a.Falsey())
                continue;

            if (!firstAlias)
                sb.Append(", ");

            sb.Append($"\"{DefaultEmitterHelpers.EscapeStringLiteral(a)}\"");
            firstAlias = false;
        }

        sb.Append(" }");
        sb.Append(" }");
        return sb.ToString();
    }

    private static EmitResult EmitNested(IReadOnlyList<TimezoneSpec> entries)
    {
        // Build a trie over slash-separated IANA segments. Underscores inside
        // segments (e.g. New_York) are kept unchanged, because the segment is
        // already a valid C# identifier. EmitTrieNode maps a hyphen to an
        // underscore (Port-au-Prince -> Port_au_Prince).
        var root = new TrieNode();
        foreach (var entry in entries)
        {
            var segs = entry.IanaIdentifier.Split('/');
            if (segs.Length < 2)
                continue;

            var node = root;
            for (var i = 0; i < segs.Length; i++)
            {
                var seg = segs[i];
                if (!node.Children.TryGetValue(seg, out var child))
                {
                    child = new TrieNode { Segment = seg };
                    node.Children[seg] = child;
                }

                node = child;

                if (i == segs.Length - 1)
                    node.LeafIana = entry.IanaIdentifier;
            }
        }

        var sb = new StringBuilder();
        DefaultEmitterHelpers.AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine($"using {DefaultEmitterHelpers.AbstractionsNamespace};");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Strongly-typed nested-class hierarchy over the timezone catalog -");
        sb.AppendLine(
            "/// <c>Timezones.America.New_York</c> returns a <see cref=\"TimezoneCode\"/>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class Timezones");
        sb.AppendLine("{");

        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var rootMembers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kvp in OrderedChildren(root))
            EmitTrieNode(sb, kvp.Value, indent: 1, parentPath: null, rootMembers, diagnostics);

        sb.AppendLine("}");

        return new EmitResult(
            HintName: "TimezonesNested.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: diagnostics.ToImmutable());
    }

    private static void EmitTrieNode(
        StringBuilder sb,
        TrieNode node,
        int indent,
        string? parentPath,
        HashSet<string> siblingMembers,
        ImmutableArray<EmitDiagnostic>.Builder diagnostics)
    {
        var pad = new string(' ', indent * 4);
        var segment = node.Segment ?? string.Empty;
        var path = parentPath is null ? segment : parentPath + "/" + segment;
        var memberName = ToMemberName(segment);

        // A member name a sibling already has (Port-au-Prince beside
        // Port_au_Prince) is reported and skipped like a segment that does
        // not convert, because a second member of that name does not compile.
        if (memberName is null || !siblingMembers.Add(memberName))
        {
            // The node and every time zone under it are left out of the
            // Timezones class; TimezoneLookup still contains them.
            diagnostics.Add(EmitDiagnostics.InvalidIdentifier(path, "Timezones"));
            return;
        }

        if (node.Children.Count == 0 && node.LeafIana is not null)
        {
            sb.AppendLine(
                $"{pad}/// <summary>Timezone <c>{node.LeafIana}</c>.</summary>");
            var leafIana = DefaultEmitterHelpers.EscapeStringLiteral(node.LeafIana);
            sb.AppendLine(
                $"{pad}public static readonly TimezoneCode {memberName} = "
                + $"TimezoneCode.FromString(\"{leafIana}\");");
        }
        else
        {
            sb.AppendLine(
                $"{pad}/// <summary>Timezone subtree under <c>{node.Segment}</c>.</summary>");
            sb.AppendLine($"{pad}public static class {memberName}");
            sb.AppendLine($"{pad}{{");
            var childMembers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kvp in OrderedChildren(node))
                EmitTrieNode(sb, kvp.Value, indent + 1, path, childMembers, diagnostics);

            sb.AppendLine($"{pad}}}");
        }
    }

    /// <summary>
    /// Converts one IANA segment to a member name. A hyphen becomes an
    /// underscore, so <c>Port-au-Prince</c> emits as <c>Port_au_Prince</c>
    /// and <c>Ust-Nera</c> as <c>Ust_Nera</c>; the rest follows
    /// <see cref="DefaultEmitterHelpers.CoerceToIdentifier"/>.
    /// </summary>
    /// <param name="segment">The raw IANA segment.</param>
    /// <returns>The member name, or <c>null</c> when the segment does not convert.</returns>
    private static string? ToMemberName(string segment)
        => DefaultEmitterHelpers.CoerceToIdentifier(segment.Replace('-', '_'));

    private static IEnumerable<KeyValuePair<string, TrieNode>> OrderedChildren(TrieNode node)
    {
        var list = new List<KeyValuePair<string, TrieNode>>(node.Children);
        list.Sort(static (a, b) => string.CompareOrdinal(a.Key, b.Key));
        return list;
    }

    private static IReadOnlyList<TimezoneSpec> SortByIana(IEnumerable<TimezoneSpec> entries)
    {
        var list = new List<TimezoneSpec>(entries);
        list.Sort(static (a, b) => string.CompareOrdinal(a.IanaIdentifier, b.IanaIdentifier));
        return list;
    }

    private sealed class TrieNode
    {
        public string? Segment { get; set; }

        public string? LeafIana { get; set; }

        public Dictionary<string, TrieNode> Children { get; } =
            new Dictionary<string, TrieNode>(System.StringComparer.Ordinal);
    }
}
