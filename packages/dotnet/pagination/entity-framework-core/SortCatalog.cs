// -----------------------------------------------------------------------
// <copyright file="SortCatalog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination.EntityFrameworkCore;

using System.Linq.Expressions;
using System.Reflection;
using DcsvIo.D2.Pagination.Errors;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Expression catalog for multi-mode list-page sort on a page-row type.
/// Wire field names default to camelCase of the primary property (must equal
/// generated <c>List{Op}Sort.*</c>). Emitters generate all of
/// <c>List{Op}Catalog.PageRow</c>; catalogs written by hand, of any length, are
/// banned.
/// </summary>
/// <typeparam name="TRow">EF-clean page-row type (typically generated).</typeparam>
public sealed class SortCatalog<TRow>
{
    // Plain rather than concurrent because every write happens before any read
    // can reach it: the registration members are called only from the static
    // initializer of the generated catalog that owns the instance, and the
    // instance is not reachable from any other thread until that initializer
    // has run to completion. A registration after start-up would break that
    // invariant, so nothing in the emitted code performs one.
    private readonly Dictionary<string, SortFieldEntry<TRow>> r_fields =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the registered wire field names (camelCase of primary, or alias).
    /// </summary>
    public IReadOnlyCollection<string> WireFields => r_fields.Keys;

    /// <summary>
    /// Derives the camelCase wire field name from a simple member expression.
    /// </summary>
    /// <typeparam name="TProp">Property type.</typeparam>
    /// <param name="expression">Primary member expression.</param>
    /// <returns>camelCase property name.</returns>
    public static string DeriveCamelCase<TProp>(Expression<Func<TRow, TProp>> expression)
    {
        var member = ExtractMember(expression);
        var name = member.Name;

        if (name.Falsey())
        {
            throw new ArgumentException(
                "Primary expression must reference a property.",
                nameof(expression));
        }

        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>
    /// Registers a string primary + Guid tie-break. Wire name = camelCase of primary.
    /// </summary>
    /// <param name="primary">Primary string column expression.</param>
    /// <param name="tieBreak">Stable Guid tie-break expression.</param>
    /// <returns>This catalog for fluent chaining.</returns>
    public SortCatalog<TRow> Text(
        Expression<Func<TRow, string>> primary,
        Expression<Func<TRow, Guid>> tieBreak)
        => RegisterString(DeriveCamelCase(primary), primary, tieBreak);

    /// <summary>
    /// Alias overload for the rare case where the wire name differs from the
    /// property camelCase. Pass a generated sort const rather than a literal.
    /// </summary>
    /// <param name="fieldWireConst">Generated wire field const.</param>
    /// <param name="primary">Primary string column expression.</param>
    /// <param name="tieBreak">Stable Guid tie-break expression.</param>
    /// <returns>This catalog for fluent chaining.</returns>
    public SortCatalog<TRow> Text(
        string fieldWireConst,
        Expression<Func<TRow, string>> primary,
        Expression<Func<TRow, Guid>> tieBreak)
    {
        fieldWireConst.ThrowIfFalsey();
        return RegisterString(fieldWireConst, primary, tieBreak);
    }

    /// <summary>
    /// Registers a comparable primary (e.g. Instant via NodaTime companion helpers)
    /// with Guid tie-break. Used by companion packages.
    /// </summary>
    /// <typeparam name="TPrimary">Comparable primary key type.</typeparam>
    /// <param name="fieldWire">Wire field name.</param>
    /// <param name="primary">Primary column expression.</param>
    /// <param name="tieBreak">Guid tie-break expression.</param>
    /// <param name="encodePrimary">Encode primary to cursor wire string.</param>
    /// <param name="decodePrimary">Decode cursor wire string to primary.</param>
    /// <returns>This catalog for fluent chaining.</returns>
    public SortCatalog<TRow> RegisterComparable<TPrimary>(
        string fieldWire,
        Expression<Func<TRow, TPrimary>> primary,
        Expression<Func<TRow, Guid>> tieBreak,
        Func<TPrimary, string> encodePrimary,
        Func<string, D2Result<TPrimary>> decodePrimary)
        where TPrimary : IComparable<TPrimary>
    {
        fieldWire.ThrowIfFalsey();
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(tieBreak);
        ArgumentNullException.ThrowIfNull(encodePrimary);
        ArgumentNullException.ThrowIfNull(decodePrimary);

        r_fields[fieldWire] = SortFieldEntry<TRow>.CreateComparable(
            fieldWire,
            primary,
            tieBreak,
            encodePrimary,
            decodePrimary);
        return this;
    }

    /// <summary>
    /// Tries to resolve a registered field entry.
    /// </summary>
    /// <param name="fieldWire">Wire field name.</param>
    /// <param name="entry">Entry when found.</param>
    /// <returns><see langword="true"/> when registered.</returns>
    public bool TryGet(string fieldWire, out SortFieldEntry<TRow> entry)
        => r_fields.TryGetValue(fieldWire, out entry!);

    /// <summary>
    /// Returns whether the wire field is registered.
    /// </summary>
    /// <param name="fieldWire">Wire field name.</param>
    /// <returns><see langword="true"/> when registered.</returns>
    public bool Contains(string fieldWire)
        => fieldWire.Truthy() && r_fields.ContainsKey(fieldWire);

    private static MemberInfo ExtractMember<TProp>(Expression<Func<TRow, TProp>> expression)
    {
        Expression body = expression.Body;

        if (body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
            body = unary.Operand;

        if (body is MemberExpression member)
            return member.Member;

        throw new ArgumentException(
            "Primary/tie-break expressions must be simple member access.",
            nameof(expression));
    }

    /// <summary>
    /// Decodes a string primary key from its cursor wire form.
    /// </summary>
    /// <remarks>
    /// A string sort column can hold the empty string, and a boundary row
    /// holding it is ordinary data the server read out of its own table - so
    /// the empty key decodes and pages. The absent key is refused instead: a
    /// cursor whose primary key field never arrived.
    /// </remarks>
    /// <param name="wire">The cursor's primary key wire form.</param>
    /// <returns>The decoded key, or the undecodable-key failure.</returns>
    private static D2Result<string> DecodeStringPrimary(string wire)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        // The value comes off a deserialized cursor, so it can be null whatever
        // the annotation says.
        return wire is null
            ? PaginationFailures<string>.CursorKeyUndecodable()
            : D2Result<string>.Ok(wire);
    }

    private SortCatalog<TRow> RegisterString(
        string fieldWire,
        Expression<Func<TRow, string>> primary,
        Expression<Func<TRow, Guid>> tieBreak)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(tieBreak);

        r_fields[fieldWire] = SortFieldEntry<TRow>.CreateComparable(
            fieldWire,
            primary,
            tieBreak,
            static s => s,
            DecodeStringPrimary);
        return this;
    }
}
