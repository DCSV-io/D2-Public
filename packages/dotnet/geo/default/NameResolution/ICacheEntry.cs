// -----------------------------------------------------------------------
// <copyright file="ICacheEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.Default.NameResolution;

/// <summary>
/// Cache-entry contract - the record-presence field and the ambiguity-flag
/// field are published together, because they live in one dictionary value
/// type. The type is internal; the existing <c>InternalsVisibleTo</c>
/// declaration on this assembly makes it visible to test code.
/// </summary>
internal interface ICacheEntry
{
    /// <summary>
    /// Gets a value indicating whether this entry marks an ambiguous normalized name.
    /// </summary>
    bool IsAmbiguous { get; }
}
