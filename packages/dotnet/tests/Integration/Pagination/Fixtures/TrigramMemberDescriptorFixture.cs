// -----------------------------------------------------------------------
// <copyright file="TrigramMemberDescriptorFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Pagination.Fixtures;

using DcsvIo.D2.Pagination;
using DcsvIo.D2.Pagination.EntityFrameworkCore;

/// <summary>
/// A searchable list-page descriptor in the shape the list-page emitter
/// writes: the policy, the allow-set with the emitted descending-only
/// relevance lock, the sort catalog and the search catalog.
/// </summary>
internal sealed class TrigramMemberDescriptorFixture
    : ISearchableListPageDescriptor<TrigramMemberPageRowFixture>
{
    private static readonly SortFieldAllowSet sr_allow = SortFieldAllowSet.From(
        new SortFieldSpec("displayName", AllowedDirections.Both),
        new SortFieldSpec(SortFieldNames.RELEVANCE, AllowedDirections.Descending));

    private static readonly SortCatalog<TrigramMemberPageRowFixture> sr_sortCatalog =
        new SortCatalog<TrigramMemberPageRowFixture>()
            .Text(r => r.DisplayName, r => r.Id);

    private static readonly SearchCatalog<TrigramMemberPageRowFixture> sr_searchCatalog =
        new SearchCatalogBuilder<TrigramMemberPageRowFixture>()
            .Blob(r => r.SearchBlob)
            .RankField(r => r.DisplayName, 2d)
            .RankField(r => r.Email, 1d)
            .TieBreak(r => r.Id)
            .Build();

    /// <inheritdoc/>
    public ListPagingPolicy Policy { get; } =
        new(new SortSpec("displayName", SortDirection.Asc));

    /// <inheritdoc/>
    public SortFieldAllowSet AllowedSortFields => sr_allow;

    /// <inheritdoc/>
    public SortCatalog<TrigramMemberPageRowFixture> SortCatalog => sr_sortCatalog;

    /// <inheritdoc/>
    public SearchPolicy SearchPolicy { get; } = new();

    /// <inheritdoc/>
    public SearchCatalog<TrigramMemberPageRowFixture> SearchCatalog => sr_searchCatalog;

    /// <inheritdoc/>
    public SearchIndexDescriptor SearchIndex => TrigramMemberDbContextFixture.SearchIndex;
}
