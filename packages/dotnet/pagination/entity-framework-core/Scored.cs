// -----------------------------------------------------------------------
// <copyright file="Scored.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination.EntityFrameworkCore;

/// <summary>
/// A row and the relevance score one query gave it.
/// </summary>
/// <remarks>
/// <para>
/// The score is a fact about a row and a query rather than about a row, so it
/// lives here rather than on the page row itself.
/// </para>
/// <para>
/// Its members are settable, and the projection that builds it is a member
/// initialization rather than a constructor call. That is a provider
/// constraint rather than a style: this type is projected and then ordered and
/// filtered by, and Entity Framework rewrites the later ordering into the
/// projection it follows - it can bind a member read back to a settable member
/// and it cannot bind one back to a constructor argument. Built through a
/// constructor, every relevance page reaches the provider as an expression the
/// provider refuses. <see cref="Row"/> is <c>required</c>, so a
/// member initialization that omits it does not compile.
/// </para>
/// </remarks>
/// <typeparam name="TRow">EF-clean page-row type.</typeparam>
public sealed class Scored<TRow>
{
    /// <summary>Gets or sets the page row.</summary>
    public required TRow Row { get; set; }

    /// <summary>
    /// Gets or sets the relevance score, higher being a better match.
    /// </summary>
    public double Score { get; set; }
}
