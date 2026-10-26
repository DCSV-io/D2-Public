// -----------------------------------------------------------------------
// <copyright file="AllowedDirections.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination;

/// <summary>
/// Directions a sort field permits.
/// </summary>
/// <remarks>
/// <see cref="None"/> exists because a flags enumeration needs a zero, not
/// because a field can have it: constructing a <see cref="SortFieldSpec"/>
/// with <see cref="None"/> throws.
/// </remarks>
[Flags]
public enum AllowedDirections
{
    /// <summary>No direction. Not a value a sort field may have.</summary>
    None = 0,

    /// <summary>Ascending order is permitted.</summary>
    Ascending = 1,

    /// <summary>Descending order is permitted.</summary>
    Descending = 2,

    /// <summary>Both directions are permitted.</summary>
    Both = Ascending | Descending,
}
