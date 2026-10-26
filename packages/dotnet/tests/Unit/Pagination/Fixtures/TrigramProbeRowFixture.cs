// -----------------------------------------------------------------------
// <copyright file="TrigramProbeRowFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Pagination.Fixtures;

/// <summary>
/// Test page row with the shape a searching operation emits: the sortable and
/// rankable columns, the identity column a keyset tie-break needs, and the
/// generated blob column the trigram index is declared on.
/// </summary>
public sealed class TrigramProbeRowFixture
{
    /// <summary>Gets or sets the identity column.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the higher-weighted rank field.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Gets or sets the lower-weighted rank field.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets the generated concatenated search column.</summary>
    public string SearchBlob { get; set; } = string.Empty;
}
