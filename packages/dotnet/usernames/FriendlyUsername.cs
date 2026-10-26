// -----------------------------------------------------------------------
// <copyright file="FriendlyUsername.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Usernames;

/// <summary>
/// Result of pure friendly-username generation (no uniqueness / DB check).
/// </summary>
/// <param name="Username">
/// Lowercase canonical form suitable for unique storage (adjectivenoun###).
/// </param>
/// <param name="DisplayUsername">PascalCase display form (AdjectiveNoun###).</param>
public sealed record FriendlyUsername(
    string Username,
    string DisplayUsername);
