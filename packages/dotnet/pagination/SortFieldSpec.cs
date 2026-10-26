// -----------------------------------------------------------------------
// <copyright file="SortFieldSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination;

/// <summary>
/// One allow-listed sort field and the directions it permits.
/// </summary>
/// <remarks>
/// The permitted-direction check lives in the <see cref="Allowed"/> accessor
/// rather than in the primary constructor. A positional record generates a
/// copy constructor that copies backing fields directly, so a guard written
/// only in the primary constructor is bypassed by
/// <c>spec with { Allowed = AllowedDirections.None }</c>. An <c>init</c>
/// accessor runs on the primary constructor and on every <c>with</c>
/// expression alike.
/// </remarks>
/// <param name="Field">Wire sort field name.</param>
/// <param name="Allowed">Directions this field permits.</param>
public sealed record SortFieldSpec(string Field, AllowedDirections Allowed)
{
    private readonly AllowedDirections r_allowed = Validate(Allowed);

    /// <summary>
    /// Gets the directions this field permits. Never
    /// <see cref="AllowedDirections.None"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is <see cref="AllowedDirections.None"/> or has a bit
    /// outside <see cref="AllowedDirections.Both"/>. A field permitting no
    /// direction cannot be sorted on, so it is a wiring defect in the host
    /// rather than a caller failure, and it fails where the mistake was made.
    /// </exception>
    public AllowedDirections Allowed
    {
        get => r_allowed;
        init => r_allowed = Validate(value);
    }

    /// <summary>
    /// Gets the only permitted direction, or <see langword="null"/> when both
    /// directions are permitted.
    /// </summary>
    public SortDirection? SoleDirection =>
        Allowed switch
        {
            AllowedDirections.Ascending => SortDirection.Asc,
            AllowedDirections.Descending => SortDirection.Desc,
            _ => null,
        };

    private static AllowedDirections Validate(AllowedDirections allowed)
    {
        if (allowed is AllowedDirections.Ascending
            or AllowedDirections.Descending
            or AllowedDirections.Both)
        {
            return allowed;
        }

        throw new ArgumentOutOfRangeException(
            nameof(allowed),
            allowed,
            "A sort field must permit at least one direction.");
    }
}
