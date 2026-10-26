// -----------------------------------------------------------------------
// <copyright file="SortFieldAllowSet.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination;

using System.Diagnostics.CodeAnalysis;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Ordinal, case-sensitive allow-set holding each field's permitted directions.
/// </summary>
public sealed class SortFieldAllowSet
{
    private readonly Dictionary<string, SortFieldSpec> r_specs;

    private SortFieldAllowSet(Dictionary<string, SortFieldSpec> specs) => r_specs = specs;

    /// <summary>Gets the allow-listed wire field names.</summary>
    public IReadOnlyCollection<string> Fields => r_specs.Keys;

    /// <summary>
    /// Builds an allow-set from explicit per-field specifications.
    /// </summary>
    /// <param name="specs">
    /// The specifications, one per field. A later entry for a field already
    /// present replaces the earlier one.
    /// </param>
    /// <returns>The allow-set.</returns>
    /// <exception cref="ArgumentException">
    /// A specification has a null, empty or whitespace field name. An
    /// unnamed field can never match a request, so it is a host wiring defect.
    /// </exception>
    public static SortFieldAllowSet From(params ReadOnlySpan<SortFieldSpec> specs)
    {
        var map = new Dictionary<string, SortFieldSpec>(specs.Length, StringComparer.Ordinal);

        foreach (var spec in specs)
        {
            ArgumentNullException.ThrowIfNull(spec);
            spec.Field.ThrowIfFalsey();
            map[spec.Field] = spec;
        }

        return new SortFieldAllowSet(map);
    }

    /// <summary>
    /// Builds an allow-set in which every field permits both directions - the
    /// common case.
    /// </summary>
    /// <param name="fields">The allow-listed wire field names.</param>
    /// <returns>The allow-set.</returns>
    /// <exception cref="ArgumentException">
    /// A field name is null, empty or whitespace.
    /// </exception>
    public static SortFieldAllowSet BothDirections(params ReadOnlySpan<string> fields)
    {
        var map = new Dictionary<string, SortFieldSpec>(fields.Length, StringComparer.Ordinal);

        foreach (var field in fields)
        {
            field.ThrowIfFalsey();
            map[field] = new SortFieldSpec(field, AllowedDirections.Both);
        }

        return new SortFieldAllowSet(map);
    }

    /// <summary>
    /// Tries to resolve one field's specification.
    /// </summary>
    /// <param name="field">Wire sort field name.</param>
    /// <param name="spec">The specification when the field is allow-listed.</param>
    /// <returns><see langword="true"/> when the field is allow-listed.</returns>
    public bool TryGet(string? field, [NotNullWhen(true)] out SortFieldSpec? spec)
    {
        if (field.Falsey())
        {
            spec = null;
            return false;
        }

        return r_specs.TryGetValue(field!, out spec);
    }

    /// <summary>
    /// Narrows this allow-set to the fields a sort catalog has registered,
    /// keeping each surviving field's own specification.
    /// </summary>
    /// <param name="catalogWireFields">The catalog's registered wire fields.</param>
    /// <returns>The narrowed allow-set.</returns>
    public SortFieldAllowSet IntersectWith(IReadOnlyCollection<string> catalogWireFields)
    {
        ArgumentNullException.ThrowIfNull(catalogWireFields);

        var map = new Dictionary<string, SortFieldSpec>(StringComparer.Ordinal);

        foreach (var field in catalogWireFields)
        {
            if (field.Truthy() && r_specs.TryGetValue(field, out var spec))
                map[field] = spec;
        }

        return new SortFieldAllowSet(map);
    }

    /// <summary>
    /// Returns a copy of this allow-set that also contains
    /// <paramref name="spec"/>, replacing any entry for the same field.
    /// </summary>
    /// <param name="spec">The specification to add.</param>
    /// <returns>The widened allow-set.</returns>
    /// <exception cref="ArgumentException">
    /// The specification has a null, empty or whitespace field name.
    /// </exception>
    public SortFieldAllowSet With(SortFieldSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        spec.Field.ThrowIfFalsey();

        var map = new Dictionary<string, SortFieldSpec>(r_specs, StringComparer.Ordinal)
        {
            [spec.Field] = spec,
        };

        return new SortFieldAllowSet(map);
    }
}
