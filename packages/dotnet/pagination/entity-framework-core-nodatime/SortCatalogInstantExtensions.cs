// -----------------------------------------------------------------------
// <copyright file="SortCatalogInstantExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination.EntityFrameworkCore.NodaTime;

using System.Linq.Expressions;
using DcsvIo.D2.Pagination.EntityFrameworkCore;
using DcsvIo.D2.Pagination.Errors;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using Instant = global::NodaTime.Instant;
using InstantPattern = global::NodaTime.Text.InstantPattern;

/// <summary>
/// Instant primary + Guid tie-break registration for <see cref="SortCatalog{TRow}"/>.
/// Cursor primary keys use <see cref="InstantPattern.ExtendedIso"/>.
/// </summary>
public static class SortCatalogInstantExtensions
{
    /// <summary>
    /// Instant registration extension block on <see cref="SortCatalog{TRow}"/>.
    /// </summary>
    /// <typeparam name="TRow">Page-row type.</typeparam>
    /// <param name="catalog">Target catalog.</param>
    extension<TRow>(SortCatalog<TRow> catalog)
    {
        /// <summary>
        /// Registers an Instant primary + Guid tie-break. Wire name = camelCase of primary.
        /// </summary>
        /// <param name="primary">Instant column expression.</param>
        /// <param name="tieBreak">Guid tie-break expression.</param>
        /// <returns>The catalog for fluent chaining.</returns>
        public SortCatalog<TRow> Instant(
            Expression<Func<TRow, Instant>> primary,
            Expression<Func<TRow, Guid>> tieBreak)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            var field = SortCatalog<TRow>.DeriveCamelCase(primary);
            return catalog.Instant(field, primary, tieBreak);
        }

        /// <summary>
        /// Alias overload for the rare case where the wire name differs from
        /// the property camelCase. Pass a generated sort const rather than a literal.
        /// </summary>
        /// <param name="fieldWireConst">Generated wire field const.</param>
        /// <param name="primary">Instant column expression.</param>
        /// <param name="tieBreak">Guid tie-break expression.</param>
        /// <returns>The catalog for fluent chaining.</returns>
        public SortCatalog<TRow> Instant(
            string fieldWireConst,
            Expression<Func<TRow, Instant>> primary,
            Expression<Func<TRow, Guid>> tieBreak)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            fieldWireConst.ThrowIfFalsey();

            return catalog.RegisterComparable(
                fieldWireConst,
                primary,
                tieBreak,
                static i => InstantPattern.ExtendedIso.Format(i),
                static wire =>
                {
                    if (wire.Falsey())
                        return PaginationFailures<Instant>.CursorKeyUndecodable();

                    var parse = InstantPattern.ExtendedIso.Parse(wire);

                    if (!parse.Success)
                        return PaginationFailures<Instant>.CursorKeyUndecodable();

                    return D2Result<Instant>.Ok(parse.Value);
                });
        }
    }

    /// <summary>
    /// Round-trips an Instant through ExtendedIso: formats it, then parses the
    /// formatted text.
    /// </summary>
    /// <param name="instant">Instant value.</param>
    /// <returns>The decoded Instant, or the undecodable-cursor-key failure.</returns>
    public static D2Result<Instant> RoundTripExtendedIso(Instant instant)
    {
        var wire = InstantPattern.ExtendedIso.Format(instant);
        var parse = InstantPattern.ExtendedIso.Parse(wire);

        return parse.Success
            ? D2Result<Instant>.Ok(parse.Value)
            : PaginationFailures<Instant>.CursorKeyUndecodable();
    }
}
