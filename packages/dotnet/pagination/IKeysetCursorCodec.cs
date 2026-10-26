// -----------------------------------------------------------------------
// <copyright file="IKeysetCursorCodec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination;

using DcsvIo.D2.Result;

/// <summary>
/// Encodes/decodes versioned keyset cursor payloads for list paging.
/// </summary>
public interface IKeysetCursorCodec
{
    /// <summary>
    /// Encodes a cursor payload to one wire string.
    /// </summary>
    /// <param name="cursor">Decoded cursor payload.</param>
    /// <returns>The wire string on success; ValidationFailed on invalid payload.</returns>
    D2Result<string?> Encode(KeysetCursor cursor);

    /// <summary>
    /// Decodes a wire string back to a cursor payload.
    /// </summary>
    /// <param name="wire">Wire cursor; null/empty is not a valid decode target.</param>
    /// <returns>Payload on success; ValidationFailed when malformed.</returns>
    D2Result<KeysetCursor?> Decode(string? wire);
}
