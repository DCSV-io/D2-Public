// -----------------------------------------------------------------------
// <copyright file="TrigramMemberEntityFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Pagination.Fixtures;

/// <summary>
/// The stored side of the trigram-search live-database tests: two searchable
/// source columns, one of them optional, and the generated blob column the
/// database computes from them.
/// </summary>
internal sealed class TrigramMemberEntityFixture
{
    /// <summary>Gets or sets the identity column and keyset tie-break.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the value the reach predicate fences on.</summary>
    public Guid ReachId { get; set; }

    /// <summary>Gets or sets the higher-weighted searchable source.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the lower-weighted searchable source, absent on some rows
    /// so the blob's handling of an absent source is observable.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets the generated blob column. Never written by this side: the database
    /// computes it, which is the property under test.
    /// </summary>
    public string SearchBlob { get; private set; } = string.Empty;
}
