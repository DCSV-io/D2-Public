// -----------------------------------------------------------------------
// <copyright file="GeoDataInitializerEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Emitters.Default;

using System.Collections.Immutable;
using System.Text;
using DcsvIo.D2.Geo.SourceGen.Spec;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Emits the central <c>GeoDataInitializer</c> coordinator that drives the
/// two-pass populate pattern (friend-assembly <c>internal set</c> via
/// <c>InternalsVisibleTo</c>).
/// </summary>
/// <remarks>
/// <para>
/// The coordinator has a <c>[ModuleInitializer]</c>-annotated method that
/// runs once at assembly load. Both the CLR module-initializer contract and
/// an explicit <c>s_initialized</c> flag guard this, and the flag mirrors the
/// TypeScript coordinator's <c>_initialized</c> guard. The sequence is:
/// </para>
/// <list type="number">
///   <item><description>
///     Run every per-catalog static constructor (first pass) through
///     <c>RuntimeHelpers.RunClassConstructor</c>. After this step every
///     catalog's <c>ByCode</c> FrozenDictionary is populated with records
///     whose nav-rep properties have the default <c>null</c>, <c>[]</c> or
///     empty-frozen-set values.
///   </description></item>
///   <item><description>
///     Call each catalog's <c>WireNav()</c> method, which sets the nav
///     properties through the friend-assembly <c>internal set</c> accessors.
///     Order: Subdivision first (Country.WireNav depends on
///     SubdivisionLookup.ByCountry), then Country, then Locale (Locale
///     needs the Country and Language first passes complete), then Currency
///     (depends on Country.Currencies), then Language (depends on
///     Country.PrimaryLanguage and Locale.Language), then Timezone (depends
///     on Country), then GeopoliticalEntity (depends on Country).
///   </description></item>
/// </list>
/// <para>
/// The module initializer runs before any code reads the catalogs, so every
/// reader sees a fully populated nav graph.
/// </para>
/// </remarks>
internal static class GeoDataInitializerEmitter
{
    private const string _NAMESPACE = DefaultEmitterHelpers.DefaultNamespace;

    /// <summary>
    /// Emits the <c>GeoDataInitializer.g.cs</c> file. Always returns a
    /// single result regardless of which catalogs are present. The emitted
    /// WireNav calls are safe when a catalog is empty, because each catalog
    /// guards itself.
    /// </summary>
    /// <param name="context">The aggregate spec context (currently unused:
    /// the coordinator's content does not depend on per-entry data, because
    /// it only references the per-catalog <c>WireNav()</c> entry
    /// points).</param>
    /// <returns>The single emit result.</returns>
    public static ImmutableArray<EmitResult> EmitAll(GeoSpecContext context)
    {
        _ = context;

        var sb = new StringBuilder();
        DefaultEmitterHelpers.AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Coordinator for the two-pass populate pattern. Runs exactly once at");
        sb.AppendLine(
            "/// assembly load via <see cref=\"ModuleInitializerAttribute\"/>: forces");
        sb.AppendLine(
            "/// every per-catalog static constructor (first-pass record construction");
        sb.AppendLine(
            "/// with default/empty nav values), then invokes each catalog's");
        sb.AppendLine(
            "/// <c>WireNav()</c> method (mutates nav properties via friend-assembly");
        sb.AppendLine("/// <c>internal set</c> visibility).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("internal static class GeoDataInitializer");
        sb.AppendLine("{");
        sb.AppendLine("    private static bool s_initialized;");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>");
        sb.AppendLine(
            "    /// Module initializer - runs once before any consumer touches");
        sb.AppendLine(
            "    /// the catalogs. Idempotent: the CLR runs module initializers at most");
        sb.AppendLine(
            "    /// once per assembly load, and an explicit <c>s_initialized</c> flag");
        sb.AppendLine(
            "    /// short-circuits any defensive re-invocation (mirrors the TS");
        sb.AppendLine("    /// coordinator's <c>_initialized</c> guard).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    internal static void Initialize()");
        sb.AppendLine("    {");
        sb.AppendLine("        if (s_initialized)");
        sb.AppendLine("            return;");
        sb.AppendLine();
        sb.AppendLine("        s_initialized = true;");
        sb.AppendLine();
        sb.AppendLine("        // ---- First pass: force every catalog's static ctor to run. ----");
        sb.AppendLine(
            "        // Order here doesn't matter - each ctor "
            + "only touches its own catalog");
        sb.AppendLine(
            "        // and its own ByCode dict "
            + "(no cross-catalog references in the first pass).");
        sb.AppendLine(
            "        RuntimeHelpers.RunClassConstructor("
            + "typeof(CountryLookup).TypeHandle);");
        sb.AppendLine(
            "        RuntimeHelpers.RunClassConstructor("
            + "typeof(SubdivisionLookup).TypeHandle);");
        sb.AppendLine(
            "        RuntimeHelpers.RunClassConstructor("
            + "typeof(CurrencyLookup).TypeHandle);");
        sb.AppendLine(
            "        RuntimeHelpers.RunClassConstructor("
            + "typeof(LanguageLookup).TypeHandle);");
        sb.AppendLine(
            "        RuntimeHelpers.RunClassConstructor("
            + "typeof(LocaleLookup).TypeHandle);");
        sb.AppendLine(
            "        RuntimeHelpers.RunClassConstructor("
            + "typeof(TimezoneLookup).TypeHandle);");
        sb.AppendLine(
            "        RuntimeHelpers.RunClassConstructor("
            + "typeof(GeopoliticalEntityLookup).TypeHandle);");
        sb.AppendLine();
        sb.AppendLine("        // ---- Wire-nav step: wire nav refs in dependency order. ----");
        sb.AppendLine(
            "        // Subdivision.Country needs Country "
            + "first-pass; Country.Subdivisions");
        sb.AppendLine(
            "        // needs Subdivision first-pass "
            + "(the ByCountry index) - so Subdivision");
        sb.AppendLine("        // WireNav runs FIRST (mutates Subdivision.Country), then Country");
        sb.AppendLine("        // WireNav (consumes SubdivisionLookup.ByCountry for");
        sb.AppendLine("        // Country.Subdivisions; also needs Locale/Language/Currency/Gpe");
        sb.AppendLine(
            "        // first-pass records present in their "
            + "ByCode dicts - they are because");
        sb.AppendLine(
            "        // first-pass ran for all above). "
            + "Locale/Currency/Language/Timezone/Gpe");
        sb.AppendLine("        // WireNav can run in any order after that point.");
        sb.AppendLine("        SubdivisionLookup.WireNav();");
        sb.AppendLine("        CountryLookup.WireNav();");
        sb.AppendLine("        LocaleLookup.WireNav();");
        sb.AppendLine("        CurrencyLookup.WireNav();");
        sb.AppendLine("        LanguageLookup.WireNav();");
        sb.AppendLine("        TimezoneLookup.WireNav();");
        sb.AppendLine("        GeopoliticalEntityLookup.WireNav();");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return ImmutableArray.Create(new EmitResult(
            HintName: "GeoDataInitializer.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty));
    }
}
