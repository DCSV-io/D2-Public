// -----------------------------------------------------------------------
// <copyright file="CountryCacheEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.Default.NameResolution;

using DcsvIo.D2.Geo.Abstractions;

/// <summary>
/// Single dictionary value for the country name cache. Stores either a
/// record reference (when the normalized key resolves to exactly one
/// record) or an ambiguity sentinel (when two or more records share the
/// normalized key). The record and the flag are one struct value, and the cache
/// publishes the finished dictionary once, so a reader sees both fields of
/// an entry together.
/// </summary>
/// <param name="Record">
/// The matched country record, or <c>null</c> on ambiguity.
/// </param>
/// <param name="IsAmbiguous">
/// Gets a value indicating whether this entry marks an ambiguous normalized name.
/// </param>
internal readonly record struct CountryCacheEntry(
    Country? Record,
    bool IsAmbiguous) : ICacheEntry;
