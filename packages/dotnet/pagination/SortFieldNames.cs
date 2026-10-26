// -----------------------------------------------------------------------
// <copyright file="SortFieldNames.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination;

/// <summary>
/// Wire sort field names the library itself owns.
/// </summary>
/// <remarks>
/// Each name is declared once, so the code that writes a relevance entry and
/// the code that resolves one cannot hold literals that disagree.
/// </remarks>
public static class SortFieldNames
{
    /// <summary>The relevance ordering available only on a searching request.</summary>
    public const string RELEVANCE = "relevance";
}
