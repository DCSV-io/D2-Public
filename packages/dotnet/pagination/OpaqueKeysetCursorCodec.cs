// -----------------------------------------------------------------------
// <copyright file="OpaqueKeysetCursorCodec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Pagination;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DcsvIo.D2.Pagination.Errors;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// v1 Base64Url JSON codec for <see cref="KeysetCursor"/> (no MAC).
/// </summary>
/// <remarks>
/// <b>Base64url is an encoding rather than a seal.</b> Whoever holds a cursor
/// this codec produced can decode it and read the payload, including
/// <see cref="KeysetCursor.PrimaryKey"/> - the boundary row's value in the sort
/// column. A surface whose sort column holds a personal value sorts on another
/// column, or supplies a codec that encrypts the payload.
/// </remarks>
public sealed class OpaqueKeysetCursorCodec : IKeysetCursorCodec
{
    private static readonly JsonSerializerOptions sr_json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc/>
    /// <remarks>
    /// The guard covers a cursor the host built wrong - an unknown version, no
    /// sort field, no tie-break, a primary key the caller never set - which is
    /// the same class of fault as the serialization catch below and returns the
    /// same internal-fault code. An empty primary key is not in that class: a
    /// boundary row whose sort column is the empty string is ordinary data the
    /// server produced from its own table, so it produces an ordinary cursor
    /// rather than alerting an operator.
    /// </remarks>
    public D2Result<string?> Encode(KeysetCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        // The annotation is not enforced at run time and this type is
        // deserialized from caller-supplied bytes, so the absent key is real.
        if (cursor.Version != KeysetCursor.CURRENT_VERSION
            || cursor.Sort.Falsey()
            || cursor.PrimaryKey is null
            || cursor.TieBreakerId.Falsey())
        {
            return PaginationFailures<string?>.CursorNotEncodable();
        }

        try
        {
            var json = JsonSerializer.Serialize(cursor, sr_json);
            var bytes = Encoding.UTF8.GetBytes(json);
            return D2Result<string?>.Ok(Base64UrlEncode(bytes));
        }
        catch (Exception)
        {
            return PaginationFailures<string?>.CursorNotEncodable();
        }
    }

    /// <inheritdoc/>
    public D2Result<KeysetCursor?> Decode(string? wire)
    {
        if (wire.Falsey())
            return PaginationFailures<KeysetCursor?>.CursorInvalid();

        try
        {
            var bytes = Base64UrlDecode(wire!);

            if (bytes.Falsey())
                return PaginationFailures<KeysetCursor?>.CursorInvalid();

            var cursor = JsonSerializer.Deserialize<KeysetCursor>(bytes, sr_json);

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            // A cursor whose JSON omitted the key deserializes with it null.
            if (cursor is null
                || cursor.Sort.Falsey()
                || cursor.PrimaryKey is null
                || cursor.TieBreakerId.Falsey())
            {
                return PaginationFailures<KeysetCursor?>.CursorInvalid();
            }

            return D2Result<KeysetCursor?>.Ok(cursor);
        }
        catch (Exception)
        {
            return PaginationFailures<KeysetCursor?>.CursorInvalid();
        }
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        var b64 = Convert.ToBase64String(bytes);
        return b64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[]? Base64UrlDecode(string wire)
    {
        var padded = wire.Replace('-', '+').Replace('_', '/');

        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
            case 1:
                return null;
        }

        return Convert.FromBase64String(padded);
    }
}
