// -----------------------------------------------------------------------
// <copyright file="NormalizedPageRequest.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination;

/// <summary>
/// Normalized page after policy apply (limit clamped/defaulted; cursor decoded; sort resolved).
/// </summary>
/// <param name="Limit">Effective page size in 1..MaxLimit.</param>
/// <param name="Cursor">Decoded keyset cursor when present; null for first page.</param>
/// <param name="AppliedSort">Resolved multi-mode sort for this page.</param>
/// <param name="Search">The validated query this page is bound to; null when there is none.</param>
public sealed record NormalizedPageRequest(
    int Limit,
    KeysetCursor? Cursor,
    SortSpec AppliedSort,
    SearchQuery? Search = null);
