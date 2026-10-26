// -----------------------------------------------------------------------
// <copyright file="PageResult.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination;

/// <summary>
/// Multi-mode page result for list ops (items, next cursor, applied sort and total).
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
/// <param name="Items">Page items (at most the normalized limit).</param>
/// <param name="NextCursor">Cursor for the next page; null when no more. Readable, not sealed.</param>
/// <param name="AppliedSort">Sort that produced this page.</param>
/// <param name="TotalCount">Base-filter total count (pre-cursor).</param>
public sealed record PageResult<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    SortSpec AppliedSort,
    long TotalCount);
