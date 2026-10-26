// -----------------------------------------------------------------------
// <copyright file="KeysetCursor.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination;

/// <summary>
/// Versioned keyset cursor payload, encoded to one wire string by an
/// <see cref="IKeysetCursorCodec"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The default encoding is base64url of this payload's JSON, which anyone
/// holding the cursor string can decode and read.</b>
/// <see cref="PrimaryKey"/> is then visible: it is the boundary row's value
/// in the column the page sorted by. A surface that sorts on a personal
/// column puts one such value per page into its URLs, where it appears in
/// access logs, proxies, referrer headers and browser history. Such a value
/// stays out of the URL only when the sort column holds no personal value or
/// the codec encrypts the payload.
/// </para>
/// <para>
/// <see cref="PrimaryKey"/> is typically an Extended ISO-8601 Instant string
/// (formatted with NodaTime <c>InstantPattern.ExtendedIso</c>) or a string
/// sort column value. <see cref="TieBreakerId"/> is typically a Guid <c>D</c>
/// form string.
/// </para>
/// <para>
/// <see cref="Sort"/> is the wire field name; <see cref="Order"/> is the wire
/// direction token (<see cref="SortDirectionWire"/>). Together they pin the
/// multi-mode sort that produced this page.
/// </para>
/// <para>
/// The default codec is versioned Base64Url without a MAC. When unforgeability
/// of the payload is required, the host provides it; authorization must re-run
/// on every page. The cursor is not a capability token.
/// </para>
/// </remarks>
/// <param name="Version">
/// Cursor schema version (the current version is <see cref="CURRENT_VERSION"/>).
/// </param>
/// <param name="Sort">Wire sort field the page was ordered by.</param>
/// <param name="PrimaryKey">Primary keyset value (sort column wire form).</param>
/// <param name="TieBreakerId">Stable tie-breaker id for equal primary keys.</param>
/// <param name="Order">
/// Wire order token (<see cref="SortDirectionWire.ASC"/> or
/// <see cref="SortDirectionWire.DESC"/>).
/// </param>
/// <param name="QueryToken">
/// The <see cref="SearchQuery.QueryToken"/> of the query this page was
/// produced under; <see langword="null"/> on a page that carried no query.
/// A search page's ordering depends on its query, so a cursor from one query
/// is meaningless against another and the normalizer rejects the pairing.
/// </param>
public sealed record KeysetCursor(
    int Version,
    string Sort,
    string PrimaryKey,
    string TieBreakerId,
    string Order = SortDirectionWire.DESC,
    string? QueryToken = null)
{
    /// <summary>Current cursor schema version.</summary>
    public const int CURRENT_VERSION = 1;

    /// <summary>
    /// Gets the applied sort as a <see cref="SortSpec"/> (field and direction).
    /// </summary>
    public SortSpec ToSortSpec()
    {
        if (!SortDirectionWire.TryParse(Order, out var direction))
            direction = SortDirection.Desc;

        return new SortSpec(Sort, direction);
    }
}
