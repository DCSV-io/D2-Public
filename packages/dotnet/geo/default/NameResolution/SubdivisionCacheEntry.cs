// -----------------------------------------------------------------------
// <copyright file="SubdivisionCacheEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.Default.NameResolution;

using DcsvIo.D2.Geo.Abstractions;

/// <summary>
/// Single dictionary value for the per-country subdivision name cache.
/// Stores either a record reference or an ambiguity sentinel. The two
/// fields are one struct value, and the cache publishes the finished
/// dictionary once, so a reader sees both fields of an entry together.
/// </summary>
/// <param name="Record">
/// The matched subdivision record, or <c>null</c> on ambiguity.
/// </param>
/// <param name="IsAmbiguous">
/// Gets a value indicating whether this entry marks an ambiguous normalized name.
/// </param>
internal readonly record struct SubdivisionCacheEntry(
    Subdivision? Record,
    bool IsAmbiguous) : ICacheEntry;
