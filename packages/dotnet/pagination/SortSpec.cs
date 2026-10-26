// -----------------------------------------------------------------------
// <copyright file="SortSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination;

using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Applied or default sort: wire field name and direction.
/// </summary>
/// <param name="Field">
/// Wire sort field (camelCase; typically a generated
/// <c>List{Op}Sort</c> const).
/// </param>
/// <param name="Direction">Sort direction.</param>
public sealed record SortSpec(string Field, SortDirection Direction)
{
    /// <summary>
    /// Gets the wire order token for this direction (<see cref="SortDirectionWire"/>).
    /// </summary>
    public string DirectionWire =>
        Direction == SortDirection.Asc ? SortDirectionWire.ASC : SortDirectionWire.DESC;

    /// <summary>
    /// Gets a stable token form <c>field:asc|desc</c> for cursor embedding and diagnostics.
    /// </summary>
    public string Token => $"{Field}:{DirectionWire}";

    /// <summary>
    /// Parses a <see cref="Token"/> or field-only string into a <see cref="SortSpec"/>.
    /// Field-only defaults to <see cref="SortDirection.Desc"/>.
    /// </summary>
    /// <param name="token">Token or field wire name.</param>
    /// <param name="spec">Parsed spec when successful.</param>
    /// <returns><see langword="true"/> when a non-empty field was present.</returns>
    public static bool TryParse(string? token, out SortSpec? spec)
    {
        spec = null;

        if (token.Falsey())
            return false;

        var colon = token!.IndexOf(':');

        if (colon < 0)
        {
            spec = new SortSpec(token, SortDirection.Desc);
            return true;
        }

        var field = token[..colon];
        var order = token[(colon + 1)..];

        if (field.Falsey() || !SortDirectionWire.TryParse(order, out var direction))
            return false;

        spec = new SortSpec(field, direction);
        return true;
    }
}
