// -----------------------------------------------------------------------
// <copyright file="PageRequest.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination;

/// <summary>
/// Wire/handler page request after transport bind (multi-mode).
/// </summary>
/// <param name="Cursor">
/// Keyset cursor from a prior page; null or empty means the first page. Readable, not sealed.
/// </param>
/// <param name="Limit">Requested page size; null uses policy default.</param>
/// <param name="Sort">Optional client sort field (wire name).</param>
/// <param name="Order">Optional client order wire token (<see cref="SortDirectionWire"/>).</param>
/// <param name="Query">Raw caller search text; null or empty means no query.</param>
public sealed record PageRequest(
    string? Cursor,
    int? Limit,
    string? Sort = null,
    string? Order = null,
    string? Query = null);
