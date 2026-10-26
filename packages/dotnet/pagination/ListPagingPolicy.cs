// -----------------------------------------------------------------------
// <copyright file="ListPagingPolicy.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination;

/// <summary>
/// Per-list paging policy. Each list sets its own DefaultSort and limits.
/// </summary>
/// <param name="DefaultSort">Default applied sort when client omits sort/cursor.</param>
/// <param name="DefaultLimit">Limit when the client omits <c>limit</c>.</param>
/// <param name="MaxLimit">Inclusive ceiling; over-max requests clamp to this.</param>
public sealed record ListPagingPolicy(
    SortSpec DefaultSort,
    int DefaultLimit = ListPagingPolicy.DEFAULT_LIMIT,
    int MaxLimit = ListPagingPolicy.MAX_LIMIT)
{
    /// <summary>Platform-recommended default page size.</summary>
    public const int DEFAULT_LIMIT = 50;

    /// <summary>Platform-recommended max page size.</summary>
    public const int MAX_LIMIT = 100;
}
