// -----------------------------------------------------------------------
// <copyright file="Argon2idPhcPasswordHasher.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.PasswordHashing;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Argon2id PHC hasher emitting standard <c>$argon2id$v=19$...</c> strings.
/// </summary>
public sealed partial class Argon2idPhcPasswordHasher : IPhcPasswordHasher
{
    /// <summary>
    /// Fixed salt for the derivation the no-match paths spend. Its content never
    /// reaches a caller; only the work it costs does. Written once at type
    /// initialization and read-only afterwards, so concurrent verifies share it
    /// safely.
    /// </summary>
    private static readonly byte[] sr_dummySalt =
        new byte[PhcPasswordHasherOptions.DEFAULT_SALT_LENGTH];

    private readonly int r_memoryKib;
    private readonly int r_iterations;
    private readonly int r_parallelism;
    private readonly int r_saltLength;
    private readonly int r_hashLength;

    /// <summary>
    /// How many derivations this instance has run, so a path that spent a
    /// derivation can be told from one that returned early. Nothing in the
    /// hasher branches on it.
    /// </summary>
    private long _derivations;

    /// <summary>
    /// Initializes a new instance with default parameters.
    /// </summary>
    public Argon2idPhcPasswordHasher()
        : this(new PhcPasswordHasherOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance with the given parameters.
    /// </summary>
    /// <param name="options">Argon2id parameters.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A parameter falls outside the accepted range on
    /// <see cref="PhcPasswordHasherOptions"/>, or the memory cost is below
    /// <see cref="PhcPasswordHasherOptions.MIN_ACCEPTED_MEMORY_KIB_PER_LANE"/>
    /// per lane. Hash and Verify share one accepted range, so a hasher that
    /// accepted these parameters would produce stored strings for which its own
    /// Verify returns <c>Ok(false)</c>, or hand Argon2id a value it throws on.
    /// </exception>
    public Argon2idPhcPasswordHasher(PhcPasswordHasherOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var snapshot = new PhcPasswordHasherOptions
        {
            MemoryKib = options.MemoryKib,
            Iterations = options.Iterations,
            Parallelism = options.Parallelism,
            SaltLength = options.SaltLength,
            HashLength = options.HashLength,
        };

        ThrowIfOutsideAcceptedRange(snapshot);
        r_memoryKib = snapshot.MemoryKib;
        r_iterations = snapshot.Iterations;
        r_parallelism = snapshot.Parallelism;
        r_saltLength = snapshot.SaltLength;
        r_hashLength = snapshot.HashLength;
    }

    /// <summary>
    /// Gets the count of derivations this instance has run.
    /// </summary>
    internal long DerivationCount => Interlocked.Read(ref _derivations);

    /// <inheritdoc/>
    public D2Result<string> Hash(string? password)
    {
        if (!IsPasswordAcceptable(password))
            return D2Result<string>.ValidationFailed();

        var salt = RandomNumberGenerator.GetBytes(r_saltLength);
        var hash = DeriveCounted(
            password!,
            salt,
            r_memoryKib,
            r_iterations,
            r_parallelism,
            r_hashLength);

        var phc =
            "$argon2id$v=19$m="
            + r_memoryKib.ToString(CultureInfo.InvariantCulture)
            + ",t="
            + r_iterations.ToString(CultureInfo.InvariantCulture)
            + ",p="
            + r_parallelism.ToString(CultureInfo.InvariantCulture)
            + "$"
            + ToB64(salt)
            + "$"
            + ToB64(hash);

        return D2Result<string>.Ok(phc);
    }

    /// <inheritdoc/>
    public D2Result<bool> Verify(string? password, string? phcHash)
    {
        if (!IsPasswordAcceptable(password))
            return D2Result<bool>.ValidationFailed();

        if (phcHash.Falsey())
            return NoMatch(password!);

        var match = PhcRegex().Match(phcHash!);

        if (!match.Success)
            return NoMatch(password!);

        if (!int.TryParse(
                match.Groups["m"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var memory)
            || !int.TryParse(
                match.Groups["t"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var iterations)
            || !int.TryParse(
                match.Groups["p"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parallelism))
        {
            return NoMatch(password!);
        }

        byte[] salt;
        byte[] expected;

        try
        {
            salt = FromB64(match.Groups["salt"].Value);
            expected = FromB64(match.Groups["hash"].Value);
        }
        catch (FormatException)
        {
            return NoMatch(password!);
        }

        if (!AreStoredParametersAccepted(
                memory,
                iterations,
                parallelism,
                salt.Length,
                expected.Length))
        {
            return NoMatch(password!);
        }

        var actual = DeriveCounted(
            password!,
            salt,
            memory,
            iterations,
            parallelism,
            expected.Length);

        var ok = CryptographicOperations.FixedTimeEquals(actual, expected);
        return D2Result<bool>.Ok(ok);
    }

    /// <inheritdoc/>
    public bool NeedsRehash(string? phcHash)
    {
        if (phcHash.Falsey())
            return true;

        var match = PhcRegex().Match(phcHash!);

        if (!match.Success)
            return true;

        if (!int.TryParse(
                match.Groups["m"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var memory)
            || !int.TryParse(
                match.Groups["t"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var iterations)
            || !int.TryParse(
                match.Groups["p"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parallelism))
        {
            return true;
        }

        byte[] salt;
        byte[] hash;

        try
        {
            salt = FromB64(match.Groups["salt"].Value);
            hash = FromB64(match.Groups["hash"].Value);
        }
        catch (FormatException)
        {
            return true;
        }

        if (!AreStoredParametersAccepted(memory, iterations, parallelism, salt.Length, hash.Length))
            return true;

        return memory < r_memoryKib
            || iterations < r_iterations
            || parallelism < r_parallelism
            || salt.Length < r_saltLength
            || hash.Length < r_hashLength;
    }

    /// <summary>
    /// Whether every value read out of a stored PHC string sits inside the
    /// accepted range on <see cref="PhcPasswordHasherOptions"/>. A row with any
    /// other value is malformed, and Argon2id would either throw on it or
    /// claim whatever memory the row stated.
    /// </summary>
    private static bool AreStoredParametersAccepted(
        int memoryKib,
        int iterations,
        int parallelism,
        int saltLength,
        int hashLength) =>
        AreStoredCostsAccepted(memoryKib, iterations, parallelism)
        && saltLength >= PhcPasswordHasherOptions.MIN_ACCEPTED_SALT_LENGTH
        && saltLength <= PhcPasswordHasherOptions.MAX_ACCEPTED_SALT_LENGTH
        && hashLength >= PhcPasswordHasherOptions.MIN_ACCEPTED_HASH_LENGTH
        && hashLength <= PhcPasswordHasherOptions.MAX_ACCEPTED_HASH_LENGTH;

    /// <summary>
    /// Whether the memory, iteration and parallelism values read out of a
    /// stored PHC string sit inside the accepted range.
    /// </summary>
    private static bool AreStoredCostsAccepted(int memoryKib, int iterations, int parallelism) =>
        memoryKib >= PhcPasswordHasherOptions.MIN_ACCEPTED_MEMORY_KIB
        && memoryKib <= PhcPasswordHasherOptions.MAX_ACCEPTED_MEMORY_KIB
        && iterations >= PhcPasswordHasherOptions.MIN_ACCEPTED_ITERATIONS
        && iterations <= PhcPasswordHasherOptions.MAX_ACCEPTED_ITERATIONS
        && parallelism >= PhcPasswordHasherOptions.MIN_ACCEPTED_PARALLELISM
        && parallelism <= PhcPasswordHasherOptions.MAX_ACCEPTED_PARALLELISM
        && memoryKib
            >= PhcPasswordHasherOptions.MIN_ACCEPTED_MEMORY_KIB_PER_LANE * parallelism;

    /// <summary>
    /// Throws on parameters this hasher's own Verify would reject. The emitted
    /// parameters and the parameters read out of a stored PHC string are held to
    /// one accepted range, so no configuration produces a hash this hasher cannot
    /// verify and none reaches Argon2id with a value it throws on.
    /// </summary>
    private static void ThrowIfOutsideAcceptedRange(PhcPasswordHasherOptions options)
    {
        ThrowIfOutside(
            options.MemoryKib,
            PhcPasswordHasherOptions.MIN_ACCEPTED_MEMORY_KIB,
            PhcPasswordHasherOptions.MAX_ACCEPTED_MEMORY_KIB,
            nameof(options.MemoryKib));
        ThrowIfOutside(
            options.Iterations,
            PhcPasswordHasherOptions.MIN_ACCEPTED_ITERATIONS,
            PhcPasswordHasherOptions.MAX_ACCEPTED_ITERATIONS,
            nameof(options.Iterations));
        ThrowIfOutside(
            options.Parallelism,
            PhcPasswordHasherOptions.MIN_ACCEPTED_PARALLELISM,
            PhcPasswordHasherOptions.MAX_ACCEPTED_PARALLELISM,
            nameof(options.Parallelism));
        ThrowIfOutside(
            options.SaltLength,
            PhcPasswordHasherOptions.MIN_ACCEPTED_SALT_LENGTH,
            PhcPasswordHasherOptions.MAX_ACCEPTED_SALT_LENGTH,
            nameof(options.SaltLength));
        ThrowIfOutside(
            options.HashLength,
            PhcPasswordHasherOptions.MIN_ACCEPTED_HASH_LENGTH,
            PhcPasswordHasherOptions.MAX_ACCEPTED_HASH_LENGTH,
            nameof(options.HashLength));

        var floor =
            PhcPasswordHasherOptions.MIN_ACCEPTED_MEMORY_KIB_PER_LANE * options.Parallelism;

        if (options.MemoryKib < floor)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.MemoryKib,
                "MemoryKib must be at least "
                + floor.ToString(CultureInfo.InvariantCulture)
                + " for "
                + options.Parallelism.ToString(CultureInfo.InvariantCulture)
                + " lanes.");
        }
    }

    /// <summary>
    /// Throws when <paramref name="value"/> falls outside the accepted range,
    /// and reports the option's name in the exception.
    /// </summary>
    private static void ThrowIfOutside(int value, int min, int max, string name)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(
                name,
                value,
                "Value must be between "
                + min.ToString(CultureInfo.InvariantCulture)
                + " and "
                + max.ToString(CultureInfo.InvariantCulture)
                + ".");
        }
    }

    private static bool IsPasswordAcceptable(string? password)
    {
        if (password.Falsey())
            return false;

        return password!.Length <= PhcPasswordHasherOptions.MAX_PASSWORD_LENGTH;
    }

    private static byte[] Derive(
        string password,
        byte[] salt,
        int memoryKib,
        int iterations,
        int parallelism,
        int hashLength)
    {
        using var argon2 = new Konscious.Security.Cryptography.Argon2id(
            Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = parallelism,
            MemorySize = memoryKib,
            Iterations = iterations,
        };

        return argon2.GetBytes(hashLength);
    }

    private static string ToB64(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=');

    private static byte[] FromB64(string b64)
    {
        var padded = b64;
        var mod = b64.Length % 4;

        if (mod > 0)
            padded += new string('=', 4 - mod);

        return Convert.FromBase64String(padded);
    }

    /// <summary>
    /// The standard Argon2id PHC shape. No backtracking: anchored at
    /// both ends, and every quantified run is separated from the next by a
    /// literal outside its own character class, so each run has exactly one
    /// place to stop. No <c>matchTimeout</c> is required.
    /// </summary>
    [GeneratedRegex(
        @"^\$argon2id\$v=19\$m=(?<m>\d{1,10}),t=(?<t>\d{1,10}),p=(?<p>\d{1,10})"
        + @"\$(?<salt>[A-Za-z0-9+/]+)\$(?<hash>[A-Za-z0-9+/]+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex PhcRegex();

    /// <summary>
    /// Runs one derivation and counts it against this instance.
    /// </summary>
    private byte[] DeriveCounted(
        string password,
        byte[] salt,
        int memoryKib,
        int iterations,
        int parallelism,
        int hashLength)
    {
        Interlocked.Increment(ref _derivations);

        return Derive(password, salt, memoryKib, iterations, parallelism, hashLength);
    }

    /// <summary>
    /// Returns no match, after spending one derivation under the configured cost
    /// parameters. The result is the same for an absent account, a corrupt row
    /// and a wrong password. The cost is equal to a real verify only where the
    /// stored parameters equal the configured ones: a real verify derives under
    /// the memory, iteration, parallelism and hash-length values read out of the
    /// stored PHC string, and a row written before a cost increase holds lower
    /// ones until the rehash that <see cref="NeedsRehash"/> reports has been
    /// done for it.
    /// </summary>
    private D2Result<bool> NoMatch(string password)
    {
        var discarded = DeriveCounted(
            password,
            sr_dummySalt,
            r_memoryKib,
            r_iterations,
            r_parallelism,
            r_hashLength);

        CryptographicOperations.ZeroMemory(discarded);

        // Ok with no data is Ok(false) for a bool result.
        return D2Result<bool>.Ok();
    }
}
