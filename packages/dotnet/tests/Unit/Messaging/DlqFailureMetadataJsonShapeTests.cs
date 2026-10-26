// -----------------------------------------------------------------------
// <copyright file="DlqFailureMetadataJsonShapeTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using Xunit;

/// <summary>
/// Pins the wire shape of <see cref="DlqFailureMetadata"/> against the
/// spec-driven <see cref="DlqFailureMetadataFields"/> catalog, with the same
/// structural check as <c>D2ResultJsonShapeTests</c>. An auto-property added
/// without a <c>[JsonIgnore]</c> attribute, or a wire key that differs from the
/// spec catalog, fails a test and does not change the wire shape silently.
/// </summary>
public sealed class DlqFailureMetadataJsonShapeTests
{
    private static readonly JsonSerializerOptions sr_omitNullOptions = new()
    {
        DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public void Serialize_EmitsCatalogWireKeysOnly()
    {
        var metadata = new DlqFailureMetadata
        {
            Cause = "HANDLER_EXCEPTION",
            ErrorCode = "System.InvalidOperationException",
            Detail = "diagnostic detail",
            AttemptCount = 3,
            TraceId = "0123456789abcdef0123456789abcdef",
            NackedBy = "edge",
        };

        var json = JsonSerializer.Serialize(metadata);
        var obj = JsonNode.Parse(json)!.AsObject();

        obj.Should().ContainKey(DlqFailureMetadataFields.CAUSE);
        obj.Should().ContainKey(DlqFailureMetadataFields.ERROR_CODE);
        obj.Should().ContainKey(DlqFailureMetadataFields.DETAIL);
        obj.Should().ContainKey(DlqFailureMetadataFields.ATTEMPT_COUNT);
        obj.Should().ContainKey(DlqFailureMetadataFields.TRACE_ID);
        obj.Should().ContainKey(DlqFailureMetadataFields.NACKED_BY);
    }

    [Fact]
    public void Serialize_WireKeysSubsetOfCatalog()
    {
        // Every wire key that a fully populated DlqFailureMetadata emits must
        // be in the spec catalog. The test fails when an auto-property without
        // a [JsonIgnore] attribute adds a key that the catalog lacks.
        var metadata = new DlqFailureMetadata
        {
            Cause = "HANDLER_EXCEPTION",
            ErrorCode = "System.InvalidOperationException",
            Detail = "diagnostic detail",
            AttemptCount = 3,
            TraceId = "0123456789abcdef0123456789abcdef",
            NackedBy = "edge",
        };

        var json = JsonSerializer.Serialize(metadata);
        var obj = JsonNode.Parse(json)!.AsObject();

        foreach (var prop in obj)
        {
            DlqFailureMetadataFields.AllFields
                .Should()
                .Contain(prop.Key, $"wire field '{prop.Key}' is not in the catalog");
        }
    }

    [Fact]
    public void Serialize_IsImmuneToDefaultOptions()
    {
        // The default JsonSerializerOptions sets no PropertyNamingPolicy, so a
        // typical record would round-trip as PascalCase. The
        // [JsonPropertyName] attributes pin camelCase under the default
        // options. The test confirms that each property sets its wire name and
        // does not depend on the caller setting JsonNamingPolicy.CamelCase.
        var metadata = new DlqFailureMetadata
        {
            Cause = "HANDLER_RESULT_FAILURE",
            ErrorCode = "GEN_NOT_FOUND",
        };

        var json = JsonSerializer.Serialize(metadata);
        var obj = JsonNode.Parse(json)!.AsObject();

        obj.Should().ContainKey(DlqFailureMetadataFields.CAUSE);
        obj.Should().NotContainKey("Cause", "PascalCase regression");
        obj.Should().ContainKey(DlqFailureMetadataFields.ERROR_CODE);
        obj.Should().NotContainKey("ErrorCode", "PascalCase regression");
    }

    [Fact]
    public void Serialize_OmitsNullOptionalFields()
    {
        // The test serializes with options that set WhenWritingNull, so the
        // optional nullable fields (Detail, TraceId and NackedBy) are omitted
        // when null, which keeps the AMQP header payload small.
        var metadata = new DlqFailureMetadata
        {
            Cause = "HANDLER_RESULT_FAILURE",
            ErrorCode = "GEN_NOT_FOUND",
        };

        var json = JsonSerializer.Serialize(metadata, sr_omitNullOptions);
        var obj = JsonNode.Parse(json)!.AsObject();

        obj.Should().ContainKey(DlqFailureMetadataFields.CAUSE);
        obj.Should().ContainKey(DlqFailureMetadataFields.ERROR_CODE);
        obj.Should().ContainKey(DlqFailureMetadataFields.ATTEMPT_COUNT);
        obj.Should().NotContainKey(DlqFailureMetadataFields.DETAIL);
        obj.Should().NotContainKey(DlqFailureMetadataFields.TRACE_ID);
        obj.Should().NotContainKey(DlqFailureMetadataFields.NACKED_BY);
    }
}
