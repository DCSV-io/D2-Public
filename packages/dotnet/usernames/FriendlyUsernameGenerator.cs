// -----------------------------------------------------------------------
// <copyright file="FriendlyUsernameGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Usernames;

using System.Globalization;
using System.Security.Cryptography;
using DcsvIo.D2.Result;
using DcsvIo.D2.Usernames.Data;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Pure friendly-username generator (crypto RNG only; never checks uniqueness).
/// Defaults draw one word from the built-in adjective and noun lists and a suffix in 1-999.
/// </summary>
public static class FriendlyUsernameGenerator
{
    /// <summary>
    /// Generates a random friendly username.
    /// Pure: uses <see cref="RandomNumberGenerator"/> only, and does not check the returned
    /// handle for uniqueness.
    /// </summary>
    /// <param name="config">Optional overrides; null uses built-in defaults.</param>
    /// <returns>
    /// <see cref="FriendlyUsername"/> on success, or <c>ValidationFailed</c> when override lists /
    /// suffix / word-length bounds are invalid.
    /// </returns>
    public static D2Result<FriendlyUsername> GenerateFriendlyUsername(
        FriendlyUsernameGeneratorOptions? config = null)
    {
        var options = config ?? new FriendlyUsernameGeneratorOptions();

        // The top of the range is rejected as well as the bottom: the exclusive
        // bound handed to GetInt32 below is MaxSuffix + 1, which overflows at
        // int.MaxValue and would hand the generator a bound under its floor.
        if (options.MinSuffix < 0
            || options.MaxSuffix < options.MinSuffix
            || options.MaxSuffix == int.MaxValue
            || options.MaxWordLength < 1)
        {
            return D2Result<FriendlyUsername>.ValidationFailed();
        }

        var adjectives = options.Adjectives ?? DefaultUsernameAdjectives.All;
        var nouns = options.Nouns ?? DefaultUsernameNouns.All;

        if (adjectives.Falsey() || nouns.Falsey())
        {
            return D2Result<FriendlyUsername>.ValidationFailed();
        }

        if (!AreWordsValid(adjectives, options.MaxWordLength)
            || !AreWordsValid(nouns, options.MaxWordLength))
        {
            return D2Result<FriendlyUsername>.ValidationFailed();
        }

        var adjective = adjectives[RandomNumberGenerator.GetInt32(adjectives.Count)];
        var noun = nouns[RandomNumberGenerator.GetInt32(nouns.Count)];

        // GetInt32 upper bound is exclusive.
        var suffix = RandomNumberGenerator.GetInt32(options.MinSuffix, options.MaxSuffix + 1);

        var displayUsername = string.Concat(
            adjective,
            noun,
            suffix.ToString(CultureInfo.InvariantCulture));

        var username = displayUsername.ToLowerInvariant();

        return D2Result<FriendlyUsername>.Ok(new FriendlyUsername(username, displayUsername));
    }

    private static bool AreWordsValid(IReadOnlyList<string> words, int maxWordLength)
    {
        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i];

            if (word.Falsey() || word.Length > maxWordLength)
            {
                return false;
            }
        }

        return true;
    }
}
