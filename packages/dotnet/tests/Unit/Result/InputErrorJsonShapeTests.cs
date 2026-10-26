// -----------------------------------------------------------------------
// <copyright file="InputErrorJsonShapeTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result;

using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Pins the wire shape of <see cref="InputError"/> against the spec-driven
/// <see cref="InputErrorWireShape"/> catalog. Mirrors the
/// <c>D2ResultJsonShapeTests</c> structural checks, so an
/// auto-property added to the record without a <c>[JsonIgnore]</c> attribute
/// (or any drift between the property's wire key and the spec catalog)
/// fails a test instead of changing the wire shape unnoticed.
/// </summary>
public sealed class InputErrorJsonShapeTests
{
    [Fact]
    public void Serialize_EmitsCatalogWireKeys()
    {
        var inputError = new InputError(
            "email",
            [new TKMessage("common_validation_EMAIL_INVALID")]);

        var json = JsonSerializer.Serialize(inputError);
        var obj = JsonNode.Parse(json)!.AsObject();

        obj.Should().ContainKey(InputErrorWireShape.FIELD);
        obj.Should().ContainKey(InputErrorWireShape.ERRORS);
        obj[InputErrorWireShape.FIELD]!.GetValue<string>().Should().Be("email");
    }

    [Fact]
    public void Serialize_WireKeysSubsetOfCatalog()
    {
        // Every wire key emitted by a fully-populated InputError MUST be in
        // the spec catalog. An auto-property added later without a [JsonIgnore]
        // attribute would add a wire key and fail this test.
        var inputError = new InputError(
            "email",
            [new TKMessage("common_validation_EMAIL_INVALID")]);

        var json = JsonSerializer.Serialize(inputError);
        var obj = JsonNode.Parse(json)!.AsObject();

        var catalog = new[] { InputErrorWireShape.FIELD, InputErrorWireShape.ERRORS };
        foreach (var prop in obj)
        {
            catalog
                .Should()
                .Contain(prop.Key, $"wire field '{prop.Key}' is not in the catalog");
        }
    }

    [Fact]
    public void Serialize_IsImmuneToDefaultOptions()
    {
        // Default JsonSerializerOptions has NO PropertyNamingPolicy set, so a
        // typical record would round-trip as PascalCase. The
        // [JsonPropertyName] attributes fix camelCase even under default
        // options. Each wire name is set on its property and does not depend on
        // the serializer being configured with JsonNamingPolicy.CamelCase.
        var inputError = new InputError(
            "email",
            [new TKMessage("k")]);

        var json = JsonSerializer.Serialize(inputError);
        var obj = JsonNode.Parse(json)!.AsObject();

        obj.Should().ContainKey(InputErrorWireShape.FIELD);
        obj.Should().NotContainKey("Field", "PascalCase regression");
        obj.Should().ContainKey(InputErrorWireShape.ERRORS);
        obj.Should().NotContainKey("Errors", "PascalCase regression");
    }
}
