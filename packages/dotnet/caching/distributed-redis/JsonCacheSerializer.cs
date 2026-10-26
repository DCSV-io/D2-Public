// -----------------------------------------------------------------------
// <copyright file="JsonCacheSerializer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching.Distributed.Redis;

using System.Text.Json;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;

/// <summary>
/// Default <see cref="ICacheSerializer"/> using <c>System.Text.Json</c>.
/// Dev-friendly (Redis CLI can inspect values directly), no extra dependencies.
/// Replace it with a binary serializer (MessagePack, Protobuf) if size becomes
/// the dominant cost.
/// </summary>
public sealed class JsonCacheSerializer : ICacheSerializer
{
    private static readonly JsonSerializerOptions sr_options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
    };

    /// <inheritdoc />
    public string ContentType => "application/json";

    /// <inheritdoc />
    public D2Result<byte[]> Serialize<T>(T value)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, sr_options);
            return D2Result<byte[]>.Ok(bytes);
        }
        catch (JsonException)
        {
            return new D2Result<byte[]>(
                success: false,
                data: default,
                messages: [TK.Common.Errors.COULD_NOT_BE_SERIALIZED],
                statusCode: System.Net.HttpStatusCode.InternalServerError,
                errorCode: ErrorCodes.GEN_COULD_NOT_BE_SERIALIZED);
        }
    }

    /// <inheritdoc />
    public D2Result<T> Deserialize<T>(byte[] bytes)
    {
        try
        {
            // A null result is a legitimate round trip for nullable T
            // (Set<string?>(k, null) writes the bytes "null", and Deserialize
            // returns null). Failure is reserved for actual parse errors.
            var value = JsonSerializer.Deserialize<T>(bytes, sr_options);
            return D2Result<T>.Ok(value);
        }
        catch (JsonException)
        {
            return new D2Result<T>(
                success: false,
                data: default,
                messages: [TK.Common.Errors.COULD_NOT_BE_DESERIALIZED],
                statusCode: System.Net.HttpStatusCode.InternalServerError,
                errorCode: ErrorCodes.GEN_COULD_NOT_BE_DESERIALIZED);
        }
        catch (NotSupportedException)
        {
            // System.Text.Json throws NotSupportedException for unsupported
            // types (e.g. some interface or polymorphic shape it can't construct).
            return new D2Result<T>(
                success: false,
                data: default,
                messages: [TK.Common.Errors.COULD_NOT_BE_DESERIALIZED],
                statusCode: System.Net.HttpStatusCode.InternalServerError,
                errorCode: ErrorCodes.GEN_COULD_NOT_BE_DESERIALIZED);
        }
    }
}
