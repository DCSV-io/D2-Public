// -----------------------------------------------------------------------
// <copyright file="TrigramMemberPageRowFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Pagination.Fixtures;

/// <summary>
/// The page row of a searching operation: the identity column, the column
/// the reach predicate fences on, the rank fields and the generated blob
/// member.
/// </summary>
/// <remarks>
/// Its members are settable and the projection that builds it is an object
/// initializer, because that is the only shape a relational provider can bind a
/// later filter or ordering back through - see
/// <c>ProjectedPageRowTranslationTests</c>, which pins both halves of that.
/// </remarks>
public sealed class TrigramMemberPageRowFixture
{
    /// <summary>Gets or sets the identity column and keyset tie-break.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the value the reach predicate fences on.</summary>
    public Guid ReachId { get; set; }

    /// <summary>Gets or sets the higher-weighted rank field.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets the lower-weighted rank field.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Gets or sets the generated concatenated search column.</summary>
    public string SearchBlob { get; set; } = string.Empty;
}
