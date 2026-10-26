// -----------------------------------------------------------------------
// <copyright file="IListPageDescriptor.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination.EntityFrameworkCore;

using DcsvIo.D2.Pagination;

/// <summary>
/// Everything one list operation's paging is configured by.
/// </summary>
/// <typeparam name="TRow">EF-clean page-row type.</typeparam>
public interface IListPageDescriptor<TRow>
{
    /// <summary>Gets the operation's paging policy - default sort and limits.</summary>
    ListPagingPolicy Policy { get; }

    /// <summary>Gets the allow-set listing each field's permitted directions.</summary>
    SortFieldAllowSet AllowedSortFields { get; }

    /// <summary>Gets the operation's registered sort fields.</summary>
    SortCatalog<TRow> SortCatalog { get; }
}
