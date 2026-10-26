// -----------------------------------------------------------------------
// <copyright file="FriendlyUsernameGeneratorOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Usernames;

/// <summary>
/// Optional overrides for <see cref="FriendlyUsernameGenerator"/>.
/// A null list field selects the built-in default list.
/// </summary>
public sealed class FriendlyUsernameGeneratorOptions
{
    /// <summary>Minimum inclusive numeric suffix (default 1).</summary>
    public const int DEFAULT_MIN_SUFFIX = 1;

    /// <summary>Maximum inclusive numeric suffix (default 999).</summary>
    public const int DEFAULT_MAX_SUFFIX = 999;

    /// <summary>Maximum allowed length of each word in override lists (default 12).</summary>
    public const int DEFAULT_MAX_WORD_LENGTH = 12;

    /// <summary>
    /// Gets the override adjective list; null uses
    /// <see cref="Data.DefaultUsernameAdjectives.All"/>.
    /// </summary>
    public IReadOnlyList<string>? Adjectives { get; init; }

    /// <summary>
    /// Gets the override noun list; null uses
    /// <see cref="Data.DefaultUsernameNouns.All"/>.
    /// </summary>
    public IReadOnlyList<string>? Nouns { get; init; }

    /// <summary>Gets the minimum inclusive numeric suffix.</summary>
    public int MinSuffix { get; init; } = DEFAULT_MIN_SUFFIX;

    /// <summary>Gets the maximum inclusive numeric suffix.</summary>
    public int MaxSuffix { get; init; } = DEFAULT_MAX_SUFFIX;

    /// <summary>
    /// Gets the maximum allowed length of each word when validating override lists.
    /// Built-in defaults already satisfy this bound.
    /// </summary>
    public int MaxWordLength { get; init; } = DEFAULT_MAX_WORD_LENGTH;
}
