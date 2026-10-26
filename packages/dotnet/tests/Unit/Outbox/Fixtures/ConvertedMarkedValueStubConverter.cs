// -----------------------------------------------------------------------
// <copyright file="ConvertedMarkedValueStubConverter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Writes <see cref="ConvertedMarkedValueStub"/> as a fixed string, so the type
/// has a converter of its own and no member metadata.
/// </summary>
internal sealed class ConvertedMarkedValueStubConverter : JsonConverter<ConvertedMarkedValueStub>
{
    /// <inheritdoc/>
    public override ConvertedMarkedValueStub Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Skip();
        return new ConvertedMarkedValueStub();
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer, ConvertedMarkedValueStub value, JsonSerializerOptions options)
        => writer.WriteStringValue("converted");
}
