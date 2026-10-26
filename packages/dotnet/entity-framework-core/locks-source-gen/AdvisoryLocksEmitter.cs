// -----------------------------------------------------------------------
// <copyright file="AdvisoryLocksEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AdvisoryLocks.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Pure logic for emitting the <c>AdvisoryLocks</c> static class from a
/// parsed <see cref="AdvisoryLocksSpec"/>.
/// </summary>
internal static class AdvisoryLocksEmitter
{
    /// <summary>The emitted top-level class name.</summary>
    public const string CLASS_NAME = "AdvisoryLocks";

    /// <summary>The nested class holding one database's two-integer lock classes.</summary>
    public const string CLASSES_NEST_NAME = "Classes";

    private static readonly Regex sr_constNameRegex = new(
        "^[A-Z][A-Z0-9_]*$",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(50));

    /// <summary>Emits the <c>AdvisoryLocks</c> class source and its diagnostics.</summary>
    /// <param name="spec">Parsed advisory-locks spec.</param>
    /// <param name="rootNamespace">
    /// Namespace for the emitted <c>AdvisoryLocks</c> class (the owning module's namespace).
    /// </param>
    /// <param name="databaseFilter">
    /// When non-null, only locks whose <c>database</c> is in this set are emitted
    /// (after full-catalog uniqueness validation). Null emits all valid entries.
    /// An empty collection emits the class shell with no database nests.
    /// </param>
    /// <returns>The generated source and the diagnostics.</returns>
    public static EmitResult Emit(
        AdvisoryLocksSpec spec,
        string rootNamespace,
        IReadOnlyCollection<string>? databaseFilter)
    {
        if (spec is null)
            throw new ArgumentNullException(nameof(spec));

        if (rootNamespace is null)
            throw new ArgumentNullException(nameof(rootNamespace));

        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();

        // Per-database: seen keys and seen constNames.
        // The same key in two different databases is legal because each database has
        // its own keyspace. Uniqueness is always validated over the full catalog.
        var seenKeysByDb = new Dictionary<string, Dictionary<long, string>>(
            StringComparer.Ordinal);
        var seenConstNamesByDb = new Dictionary<string, HashSet<string>>(
            StringComparer.Ordinal);

        var validEntries = new List<AdvisoryLockEntry>();

        foreach (var entry in spec.Locks)
        {
            if (!sr_constNameRegex.IsMatch(entry.ConstName))
            {
                diagnostics.Add(EmitDiagnostics.InvalidConstName(entry.ConstName));
                continue;
            }

            if (!seenKeysByDb.TryGetValue(entry.Database, out var seenKeys))
            {
                seenKeys = new Dictionary<long, string>();
                seenKeysByDb[entry.Database] = seenKeys;
            }

            if (!seenConstNamesByDb.TryGetValue(entry.Database, out var seenConstNames))
            {
                seenConstNames = new HashSet<string>(StringComparer.Ordinal);
                seenConstNamesByDb[entry.Database] = seenConstNames;
            }

            // Duplicate constName within the same database.
            if (!seenConstNames.Add(entry.ConstName))
            {
                diagnostics.Add(EmitDiagnostics.DuplicateConstNameInDatabase(
                    entry.ConstName, entry.Database));
                continue;
            }

            // Duplicate key within the same database: two constants would identify one lock.
            if (seenKeys.TryGetValue(entry.Key, out var existingConst))
            {
                diagnostics.Add(EmitDiagnostics.DuplicateKeyInDatabase(
                    entry.Key, existingConst, entry.Database));
                continue;
            }

            seenKeys[entry.Key] = entry.ConstName;
            validEntries.Add(entry);
        }

        var validClasses = ValidateClasses(spec, diagnostics);

        IEnumerable<AdvisoryLockEntry> emitEntries = validEntries;
        IEnumerable<AdvisoryLockClassEntry> emitClasses = validClasses;

        if (databaseFilter is not null)
        {
            var allowed = new HashSet<string>(databaseFilter, StringComparer.Ordinal);
            emitEntries = validEntries.Where(e => allowed.Contains(e.Database));
            emitClasses = validClasses.Where(c => allowed.Contains(c.Database));
        }

        var source = EmitSource(emitEntries.ToList(), emitClasses.ToList(), rootNamespace);
        return new EmitResult(source, diagnostics.ToImmutable());
    }

    /// <summary>
    /// Converts a database name to PascalCase for the nested lock class.
    /// Segments are split on <c>_</c> and <c>-</c> (canonical PostgreSQL names use the
    /// <c>d2-&#123;domain&#125;</c> form, for example fixture id <c>d2-fixture-db</c> ->
    /// <c>D2FixtureDb</c>).
    /// </summary>
    /// <param name="databaseName">The database name to convert.</param>
    internal static string SnakeToPascal(string databaseName)
    {
        if (databaseName.Length == 0)
            return databaseName;

        var parts = databaseName.Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            sb.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1)
                sb.Append(part, 1, part.Length - 1);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Validates the two-integer class entries over the full catalog, per
    /// database, and returns the ones that survive.
    /// </summary>
    /// <param name="spec">Parsed advisory-locks spec.</param>
    /// <param name="diagnostics">Builder the diagnostics are added to.</param>
    /// <returns>The class entries that passed every check.</returns>
    private static List<AdvisoryLockClassEntry> ValidateClasses(
        AdvisoryLocksSpec spec,
        ImmutableArray<EmitDiagnostic>.Builder diagnostics)
    {
        var seenClassesByDb = new Dictionary<string, Dictionary<int, string>>(
            StringComparer.Ordinal);
        var seenNamesByDb = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var valid = new List<AdvisoryLockClassEntry>();

        foreach (var entry in spec.Classes)
        {
            if (!sr_constNameRegex.IsMatch(entry.ConstName))
            {
                diagnostics.Add(EmitDiagnostics.InvalidConstName(entry.ConstName));
                continue;
            }

            if (!seenClassesByDb.TryGetValue(entry.Database, out var seenClasses))
            {
                seenClasses = new Dictionary<int, string>();
                seenClassesByDb[entry.Database] = seenClasses;
            }

            if (!seenNamesByDb.TryGetValue(entry.Database, out var seenNames))
            {
                seenNames = new HashSet<string>(StringComparer.Ordinal);
                seenNamesByDb[entry.Database] = seenNames;
            }

            if (!seenNames.Add(entry.ConstName))
            {
                diagnostics.Add(EmitDiagnostics.DuplicateConstNameInDatabase(
                    entry.ConstName, entry.Database));
                continue;
            }

            if (seenClasses.TryGetValue(entry.Class, out var existingConst))
            {
                diagnostics.Add(EmitDiagnostics.DuplicateClassInDatabase(
                    entry.Class, existingConst, entry.Database));
                continue;
            }

            seenClasses[entry.Class] = entry.ConstName;
            valid.Add(entry);
        }

        return valid;
    }

    private static string EmitSource(
        List<AdvisoryLockEntry> entries,
        List<AdvisoryLockClassEntry> classes,
        string rootNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine(
            "//   Generated by DcsvIo.D2.AdvisoryLocks.SourceGen.AdvisoryLocksGenerator");
        sb.AppendLine(
            "//   from host-supplied advisory-locks.spec.json AdditionalFiles");
        sb.AppendLine("//   (the source of truth). Manual edits will be lost on rebuild.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Spec-derived registry of PostgreSQL session advisory lock keys,");
        sb.AppendLine(
            "/// partitioned by owning database. Keys are unique within each");
        sb.AppendLine(
            "/// database; the same key value in two different databases is legal");
        sb.AppendLine("/// (different keyspaces).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <remarks>");
        sb.AppendLine(
            "/// Use these constants instead of raw literals so a collision");
        sb.AppendLine(
            "/// between two locks in the same database is caught at build time.");
        sb.AppendLine("/// </remarks>");
        sb.AppendLine($"public static class {CLASS_NAME}");
        sb.AppendLine("{");

        // Group entries by database, preserving spec order within each group. A
        // database contributing only classes still gets its nest.
        var entriesByDatabase = entries
            .GroupBy(e => e.Database, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var classesByDatabase = classes
            .GroupBy(c => c.Database, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var databases = entriesByDatabase.Keys
            .Concat(classesByDatabase.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();

        var firstDb = true;
        foreach (var database in databases)
        {
            if (!firstDb)
                sb.AppendLine();
            firstDb = false;

            var dbClass = SnakeToPascal(database);
            sb.AppendLine($"    /// <summary>Advisory locks owned by {database}.</summary>");
            sb.AppendLine($"    public static class {dbClass}");
            sb.AppendLine("    {");

            var firstMember = true;

            if (entriesByDatabase.TryGetValue(database, out var dbEntries))
            {
                foreach (var entry in dbEntries)
                {
                    if (!firstMember)
                        sb.AppendLine();

                    firstMember = false;

                    sb.AppendLine(
                        $"        /// <summary>{EscapeXmlDoc(entry.Doc)}</summary>");
                    sb.AppendLine(
                        $"        public const long {entry.ConstName} = {entry.Key}L;");
                }
            }

            if (classesByDatabase.TryGetValue(database, out var dbClasses))
            {
                if (!firstMember)
                    sb.AppendLine();

                sb.AppendLine(
                    "        /// <summary>Two-integer advisory-lock classes owned by "
                    + $"{database}.</summary>");
                sb.AppendLine("        /// <remarks>");
                sb.AppendLine(
                    "        /// PostgreSQL tags a two-integer advisory lock differently");
                sb.AppendLine(
                    "        /// from a single-bigint one, so a class here can never name");
                sb.AppendLine(
                    "        /// the same lock as a key above it. The second integer is");
                sb.AppendLine(
                    "        /// supplied at runtime, usually folded from a row identifier.");
                sb.AppendLine("        /// </remarks>");
                sb.AppendLine($"        public static class {CLASSES_NEST_NAME}");
                sb.AppendLine("        {");

                var firstClass = true;

                foreach (var entry in dbClasses)
                {
                    if (!firstClass)
                        sb.AppendLine();

                    firstClass = false;

                    sb.AppendLine(
                        $"            /// <summary>{EscapeXmlDoc(entry.Doc)}</summary>");
                    sb.AppendLine(
                        $"            public const int {entry.ConstName} = {entry.Class};");
                }

                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        return sb.ToString().LfNormalized();
    }

    private static string EscapeXmlDoc(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");
}
