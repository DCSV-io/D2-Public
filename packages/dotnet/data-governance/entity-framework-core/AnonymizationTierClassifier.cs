// -----------------------------------------------------------------------
// <copyright file="AnonymizationTierClassifier.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.EntityFrameworkCore;

using System.Collections.Concurrent;
using System.Collections.Generic;
using DcsvIo.D2.DataGovernance.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// Classifies an EF Core entity type for anonymization by walking its <c>D2:Anonymize</c>-
/// annotated properties, determining the entity's <see cref="AnonymizationTier"/>, and
/// assembling the ordered per-column metadata.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Caching:</strong> classification results are cached per <see cref="IEntityType"/>
/// reference in a process-global <see cref="ConcurrentDictionary{TKey,TValue}"/>. The EF Core
/// model is immutable after build and a singleton per <c>DbContext</c> type, so the
/// <see cref="IEntityType"/> reference is a stable, correct cache key. First-use
/// classification is lazy and thread-safe (<c>GetOrAdd</c>); subsequent calls return the
/// same <see cref="AnonymizationClassification"/> instance.
/// </para>
/// <para>
/// <strong>Walk scope:</strong> the classifier enumerates annotated properties from these
/// sources on the target entity:
/// <list type="number">
///   <item>Root scalar properties (<c>IEntityType.GetProperties()</c>).</item>
///   <item>
///     Complex sub-properties (recursively via
///     <c>IEntityType.GetComplexProperties()</c>).
///   </item>
///   <item>Owned entity sub-properties (transitively via the full owned graph).</item>
/// </list>
/// </para>
/// <para>
/// <strong>Tier roll-up:</strong> <see cref="AnonymizationTier.TierC"/> takes precedence over
/// <see cref="AnonymizationTier.TierB"/>, which takes precedence over
/// <see cref="AnonymizationTier.TierA"/>.
/// A single Tier-C shape (owned-JSON or <c>OwnsMany</c> child) anywhere in the owned
/// subtree demotes the whole entity to Tier C. A single
/// <see cref="AnonymizeKind.Template"/> rule with no C-shapes demotes the entity to Tier B.
/// </para>
/// <para>
/// <strong>Rule origin:</strong> rules are read exclusively from the <c>D2:Anonymize</c>
/// EF Core model annotation (<see cref="AnonymizationAnnotations.ANONYMIZE"/>). The
/// classifier never reflects on <see cref="AnonymizableAttribute"/> directly - it is
/// origin-agnostic.
/// </para>
/// </remarks>
public static class AnonymizationTierClassifier
{
    private static readonly ConcurrentDictionary<IEntityType, AnonymizationClassification>
        sr_cache = new();

    /// <summary>
    /// Gets the current number of cached classifications. Exposed for testing only.
    /// </summary>
    internal static int CacheCount => sr_cache.Count;

    /// <summary>
    /// Classifies <paramref name="entityType"/> and returns its
    /// <see cref="AnonymizationClassification"/>. Results are cached per entity type reference;
    /// repeated calls with the same reference return the same instance.
    /// </summary>
    /// <param name="entityType">
    /// The EF Core entity type to classify. Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// The <see cref="AnonymizationClassification"/> for <paramref name="entityType"/>,
    /// either freshly built or retrieved from the process-global cache.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="entityType"/> is <see langword="null"/>.
    /// </exception>
    public static AnonymizationClassification Classify(IEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return sr_cache.GetOrAdd(entityType, static key => Build(key));
    }

    /// <summary>
    /// Clears the process-global classification cache. Exposed for testing only; do not call
    /// in production code.
    /// </summary>
    internal static void ClearCache() => sr_cache.Clear();

    // =========================================================================
    // Build - the GetOrAdd factory; performs the full walk + roll-up
    // =========================================================================
    private static AnonymizationClassification Build(IEntityType entityType)
    {
        var columns = new List<AnonymizationColumn>();

        // (1) Root scalars.
        CollectRootScalars(entityType, columns);

        // (2) Complex sub-properties (recursive).
        CollectComplex(entityType.GetComplexProperties(), columns);

        // (3) Owned entity sub-properties (recursive transitive owned graph).
        CollectOwned(entityType, entityType, columns);

        // Tier roll-up: C > B > A.
        var tier = AnonymizationTier.TierA;
        AnonymizationColumn? blocker = null;

        foreach (var col in columns)
        {
            if (col.Shape == AnonymizationColumnShape.OwnedJson ||
                col.Shape == AnonymizationColumnShape.OwnsManyChild)
            {
                tier = AnonymizationTier.TierC;
                blocker ??= col;

                // No break - keep collecting so Columns is complete.
            }
            else if (tier != AnonymizationTier.TierC &&
                     col.Rule.Kind == AnonymizeKind.Template)
            {
                tier = AnonymizationTier.TierB;
            }
        }

        return new AnonymizationClassification
        {
            Tier = tier,
            Columns = columns.AsReadOnly(),
            TierCBlocker = blocker,
        };
    }

    // =========================================================================
    // Root scalars - shape (a)
    // =========================================================================
    private static void CollectRootScalars(
        IEntityType entityType,
        List<AnonymizationColumn> columns)
    {
        foreach (var property in entityType.GetProperties())
        {
            var rule =
                property.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value
                as AnonymizationRule;

            if (rule is null)
                continue;

            columns.Add(new AnonymizationColumn
            {
                ColumnName = property.GetColumnName(),
                PropertyName = property.Name,
                Rule = rule,
                Shape = AnonymizationColumnShape.Scalar,
                Property = property,
            });
        }
    }

    // =========================================================================
    // Complex sub-properties - shape (c); recursive
    // =========================================================================
    private static void CollectComplex(
        IEnumerable<IComplexProperty> complexProperties,
        List<AnonymizationColumn> columns)
    {
        foreach (var complexProp in complexProperties)
        {
            var complexType = complexProp.ComplexType;

            foreach (var property in complexType.GetProperties())
            {
                var rule =
                    property.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value
                    as AnonymizationRule;

                if (rule is null)
                    continue;

                columns.Add(new AnonymizationColumn
                {
                    ColumnName = property.GetColumnName(),
                    PropertyName = property.Name,
                    Rule = rule,
                    Shape = AnonymizationColumnShape.Complex,
                    Property = property,
                });
            }

            // Recurse into nested complex properties.
            CollectComplex(complexType.GetComplexProperties(), columns);
        }
    }

    // =========================================================================
    // Owned entity sub-properties - shapes (b), (d), (e); recursive
    // =========================================================================
    private static void CollectOwned(
        IEntityType ownerRoot,
        IEntityType currentOwner,
        List<AnonymizationColumn> columns)
    {
        foreach (var ownedType in currentOwner.Model.GetEntityTypes())
        {
            var ownership = ownedType.FindOwnership();
            if (ownership is null || !ReferenceEquals(ownership.PrincipalEntityType, currentOwner))
                continue;

            var shape = ClassifyOwnedShape(ownedType, ownerRoot);

            foreach (var property in ownedType.GetProperties())
            {
                var rule =
                    property.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value
                    as AnonymizationRule;

                if (rule is null)
                    continue;

                columns.Add(new AnonymizationColumn
                {
                    ColumnName = property.GetColumnName(),
                    PropertyName = property.Name,
                    Rule = rule,
                    Shape = shape,
                    Property = property,
                });
            }

            // Recurse into nested owned types.
            CollectOwned(ownerRoot, ownedType, columns);
        }
    }

    // =========================================================================
    // Owned shape discriminator
    // =========================================================================
    private static AnonymizationColumnShape ClassifyOwnedShape(IEntityType owned, IEntityType root)
    {
        // Owned-JSON: OwnsOne/OwnsMany + .ToJson() -> Tier C.
        if (owned.IsMappedToJson())
            return AnonymizationColumnShape.OwnedJson;

        // Table-split OwnsOne: owned type maps to the same table as the root -> Tier A.
        // Both table names must be non-null and equal. Any other case (different
        // tables for an OwnsMany child table, or a null or unknown table mapping) is
        // treated as fail-safe Tier-C OwnsManyChild; the startup guard surfaces these at boot.
        var ownedTable = owned.GetTableName();
        var rootTable = root.GetTableName();
        if (ownedTable is not null && rootTable is not null && ownedTable == rootTable)
            return AnonymizationColumnShape.TableSplitOwned;

        // Different tables, or null/unknown table mapping -> treat as OwnsManyChild (Tier C).
        // The null-table branch fires when either ownedTable or rootTable is null, which
        // happens with non-relational providers (e.g. InMemory) or unmapped entity types.
        // The fail-safe classifies these as OwnsManyChild so the startup guard surfaces them
        // at boot rather than silently routing them to a tier that cannot handle them.
        // The table-split equality check above covers this branch by inversion. A dedicated
        // InMemory-based unit test would require a non-relational provider in a library that
        // targets only relational semantics, and would be fragile.
        return AnonymizationColumnShape.OwnsManyChild;
    }
}
