// -----------------------------------------------------------------------
// <copyright file="IPhcPasswordHasher.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.PasswordHashing;

using DcsvIo.D2.Result;

/// <summary>
/// Portable PHC password hash and verify interface (Argon2id).
/// </summary>
public interface IPhcPasswordHasher
{
    /// <summary>
    /// Hashes a password into a standard Argon2id PHC string.
    /// </summary>
    /// <param name="password">Plaintext password (never logged).</param>
    /// <returns>
    /// <c>Ok</c> with PHC string; <c>ValidationFailed</c> on empty/oversized input.
    /// </returns>
    D2Result<string> Hash(string? password);

    /// <summary>
    /// Verifies a plaintext password against a PHC string (constant-time path).
    /// </summary>
    /// <remarks>
    /// An absent, unparseable or out-of-bounds stored PHC string costs the same
    /// derivation a real verify does, so the response time does not reveal to a
    /// caller whether the account exists or its stored hash is usable.
    /// </remarks>
    /// <param name="password">Plaintext password.</param>
    /// <param name="phcHash">Stored Argon2id PHC string.</param>
    /// <returns>
    /// <c>Ok(true)</c> on match; <c>Ok(false)</c> on mismatch or malformed PHC;
    /// <c>ValidationFailed</c> when password is empty/oversized.
    /// </returns>
    D2Result<bool> Verify(string? password, string? phcHash);

    /// <summary>
    /// Returns whether the stored PHC parameters are below the current policy
    /// and should be rehashed after a successful verify.
    /// </summary>
    /// <param name="phcHash">Stored PHC string.</param>
    /// <returns><c>true</c> when rehash is recommended; <c>false</c> otherwise.</returns>
    bool NeedsRehash(string? phcHash);
}
