// -----------------------------------------------------------------------
// <copyright file="ConvertedValueStubConverter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Outbox.Fixtures;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Writes <see cref="ConvertedValueStub"/> as a fixed string, so the value
/// serializes and the refusal under test is the personal-data check's own.
/// </summary>
internal sealed class ConvertedValueStubConverter : JsonConverter<ConvertedValueStub>
{
    /// <inheritdoc/>
    public override ConvertedValueStub Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Skip();
        return new ConvertedValueStub();
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer, ConvertedValueStub value, JsonSerializerOptions options)
        => writer.WriteStringValue("converted");
}
