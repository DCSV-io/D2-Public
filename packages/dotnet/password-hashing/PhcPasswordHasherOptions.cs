// -----------------------------------------------------------------------
// <copyright file="PhcPasswordHasherOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.PasswordHashing;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Argon2id parameters for PHC emission. The defaults are the fixed
/// <c>DEFAULT_*</c> constants, which are the locked floors for these
/// parameters.
/// </summary>
/// <remarks>
/// Every property is bounded by the same accepted range
/// <see cref="Argon2idPhcPasswordHasher"/> applies to a stored PHC string, so a
/// hasher cannot emit a hash its own Verify then rejects. The
/// <see cref="RangeAttribute"/> on each property states the bound for a host that
/// binds this type through options validation, and the hasher constructor throws
/// on an out-of-range instance whether or not the host runs that validation.
/// </remarks>
public sealed class PhcPasswordHasherOptions
{
    /// <summary>Default memory cost in kibibytes (64 MiB).</summary>
    public const int DEFAULT_MEMORY_KIB = 65536;

    /// <summary>Default iterations.</summary>
    public const int DEFAULT_ITERATIONS = 3;

    /// <summary>Default parallelism (lanes).</summary>
    public const int DEFAULT_PARALLELISM = 1;

    /// <summary>Default salt length in bytes.</summary>
    public const int DEFAULT_SALT_LENGTH = 16;

    /// <summary>Default hash length in bytes.</summary>
    public const int DEFAULT_HASH_LENGTH = 32;

    /// <summary>Mechanical maximum password length accepted by the hasher.</summary>
    public const int MAX_PASSWORD_LENGTH = 128;

    /// <summary>Lowest memory cost in kibibytes a stored PHC string may state.</summary>
    public const int MIN_ACCEPTED_MEMORY_KIB = 8;

    /// <summary>
    /// Kibibytes of memory Argon2id requires per lane. Memory below
    /// <see cref="Parallelism"/> times this value makes the engine throw, so both
    /// the emitted parameters and a stored PHC string are held to it.
    /// </summary>
    public const int MIN_ACCEPTED_MEMORY_KIB_PER_LANE = 8;

    /// <summary>Highest memory cost in kibibytes a stored PHC string may state (1 GiB).</summary>
    public const int MAX_ACCEPTED_MEMORY_KIB = 1048576;

    /// <summary>Lowest iteration count a stored PHC string may state.</summary>
    public const int MIN_ACCEPTED_ITERATIONS = 1;

    /// <summary>Highest iteration count a stored PHC string may state.</summary>
    public const int MAX_ACCEPTED_ITERATIONS = 16;

    /// <summary>Lowest parallelism a stored PHC string may state.</summary>
    public const int MIN_ACCEPTED_PARALLELISM = 1;

    /// <summary>Highest parallelism a stored PHC string may state.</summary>
    public const int MAX_ACCEPTED_PARALLELISM = 16;

    /// <summary>Lowest salt length in bytes a stored PHC string may carry.</summary>
    public const int MIN_ACCEPTED_SALT_LENGTH = 8;

    /// <summary>Highest salt length in bytes a stored PHC string may carry.</summary>
    public const int MAX_ACCEPTED_SALT_LENGTH = 64;

    /// <summary>Lowest tag length in bytes a stored PHC string may carry.</summary>
    public const int MIN_ACCEPTED_HASH_LENGTH = 16;

    /// <summary>Highest tag length in bytes a stored PHC string may carry.</summary>
    public const int MAX_ACCEPTED_HASH_LENGTH = 64;

    /// <summary>
    /// Gets or sets memory cost in kibibytes. Also at least
    /// <see cref="Parallelism"/> times
    /// <see cref="MIN_ACCEPTED_MEMORY_KIB_PER_LANE"/>.
    /// </summary>
    [Range(MIN_ACCEPTED_MEMORY_KIB, MAX_ACCEPTED_MEMORY_KIB)]
    public int MemoryKib { get; set; } = DEFAULT_MEMORY_KIB;

    /// <summary>Gets or sets iteration count.</summary>
    [Range(MIN_ACCEPTED_ITERATIONS, MAX_ACCEPTED_ITERATIONS)]
    public int Iterations { get; set; } = DEFAULT_ITERATIONS;

    /// <summary>Gets or sets parallelism (lanes).</summary>
    [Range(MIN_ACCEPTED_PARALLELISM, MAX_ACCEPTED_PARALLELISM)]
    public int Parallelism { get; set; } = DEFAULT_PARALLELISM;

    /// <summary>Gets or sets salt length in bytes.</summary>
    [Range(MIN_ACCEPTED_SALT_LENGTH, MAX_ACCEPTED_SALT_LENGTH)]
    public int SaltLength { get; set; } = DEFAULT_SALT_LENGTH;

    /// <summary>Gets or sets derived hash length in bytes.</summary>
    [Range(MIN_ACCEPTED_HASH_LENGTH, MAX_ACCEPTED_HASH_LENGTH)]
    public int HashLength { get; set; } = DEFAULT_HASH_LENGTH;
}
