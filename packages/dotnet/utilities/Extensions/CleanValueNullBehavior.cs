// -----------------------------------------------------------------------
// <copyright file="CleanValueNullBehavior.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Selects what <see cref="EnumerableExtensions"/> does when the cleaner
/// function returns null for an element.
/// </summary>
public enum CleanValueNullBehavior
{
    /// <summary>
    /// Skips elements for which the cleaner returns null. This is the default.
    /// </summary>
    RemoveNulls,

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when the cleaner
    /// returns null for any element.
    /// </summary>
    ThrowOnNull,
}
