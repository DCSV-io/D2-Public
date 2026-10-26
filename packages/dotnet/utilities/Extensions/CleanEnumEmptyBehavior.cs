// -----------------------------------------------------------------------
// <copyright file="CleanEnumEmptyBehavior.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Selects what <see cref="EnumerableExtensions"/> does when the input
/// enumerable is null or empty, or when cleaning leaves it empty.
/// </summary>
public enum CleanEnumEmptyBehavior
{
    /// <summary>
    /// Returns an empty enumerable.
    /// </summary>
    ReturnEmpty,

    /// <summary>
    /// Returns null.
    /// </summary>
    ReturnNull,

    /// <summary>
    /// Throws an exception.
    /// </summary>
    Throw,
}
