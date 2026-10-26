// -----------------------------------------------------------------------
// <copyright file="MutableForwardedJwtAccessor.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// Default <see cref="IForwardedJwtAccessor"/> implementation - a plain
/// per-request mutable cell. Registered request-scoped, every request gets a
/// fresh instance with no captured token and no cross-request state.
/// </summary>
/// <remarks>
/// <para>
/// Holds no static state - request isolation is provided entirely by the
/// scoped DI lifetime. The capture path routes through
/// <see cref="ForwardedJwt.Create"/>, so a blank input is validated away and
/// never stored.
/// </para>
/// <para>
/// Public because the DI registration sites live in separate transport
/// assemblies - the same cross-assembly-registration reason the analogous
/// request-context concrete implementation is public. Depend on
/// <see cref="IForwardedJwtAccessor"/>, not on this type.
/// </para>
/// </remarks>
public sealed class MutableForwardedJwtAccessor : IForwardedJwtAccessor
{
    private ForwardedJwt? _current;

    /// <inheritdoc/>
    public ForwardedJwt? Current => _current;

    /// <inheritdoc/>
    public void Capture(string rawBearer)
    {
        // Create() validates (null/empty/whitespace -> fail); a failed wrapper is
        // never stored. A second capture in the same scope overwrites the first
        // (last-write-wins); the real pipeline validates once, so this is a
        // defensive, documented semantic rather than an expected occurrence.
        var result = ForwardedJwt.Create(rawBearer);

        if (result.Success)
            _current = result.Data;
    }

    /// <inheritdoc/>
    public void Clear() => _current = null;
}
