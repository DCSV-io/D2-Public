// -----------------------------------------------------------------------
// <copyright file="ISearchableListPageDescriptor.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination.EntityFrameworkCore;

using DcsvIo.D2.Pagination;

/// <summary>
/// A list operation that also searches.
/// </summary>
/// <remarks>
/// The interface states the searchable case at the type level. A list that does
/// not search cannot be passed to a search extension, so a missing search shape
/// is a compile error rather than a runtime failure.
/// </remarks>
/// <typeparam name="TRow">EF-clean page-row type.</typeparam>
public interface ISearchableListPageDescriptor<TRow> : IListPageDescriptor<TRow>
{
    /// <summary>Gets the surface's query-length bounds.</summary>
    SearchPolicy SearchPolicy { get; }

    /// <summary>Gets the operation's search shape.</summary>
    SearchCatalog<TRow> SearchCatalog { get; }

    /// <summary>Gets the properties the searchable blob concatenates.</summary>
    SearchIndexDescriptor SearchIndex { get; }
}
