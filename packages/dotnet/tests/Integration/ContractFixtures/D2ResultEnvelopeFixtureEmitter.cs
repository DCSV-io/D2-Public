// -----------------------------------------------------------------------
// <copyright file="D2ResultEnvelopeFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Emits one fixture per D2Result envelope scenario reflected off the
/// .NET codegen-emitted static class <see cref="D2ResultEnvelopeFieldNames"/>.
/// The fixture families are:
/// <list type="bullet">
///   <item>
///     <c>field-names</c> - the property-name catalog (SUCCESS / DATA / MESSAGES /
///     INPUT_ERRORS / ERROR_CODE / TRACE_ID / STATUS_CODE / CATEGORY -> wire values).
///   </item>
///   <item>
///     <c>round-trip-*</c> - round-trip JSON fixtures produced by
///     serializing real <see cref="D2Result"/> / <see cref="D2Result{TData}"/>
///     instances via <see cref="System.Text.Json.JsonSerializer"/>. The
///     scenarios are success, success with data, not found, validation
///     failed, failure with a trace id and failure with a category.
///   </item>
/// </list>
/// </summary>
public sealed class D2ResultEnvelopeFixtureEmitter
{
    private const string CATALOG = "d2result-envelope";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_FieldNames()
    {
        var data = EnumerateConstants(typeof(D2ResultEnvelopeFieldNames));
        FixturePathHelpers.WriteFixture(CATALOG, "field-names", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_RoundTripOk()
    {
        // Canonical success - Ok() with no payload. Pins {success: true,
        // messages: [], inputErrors: [], statusCode: 200, errorCode: null,
        // traceId: null}. D2Result has no data property, and category is
        // omitted because it is null and has JsonIgnoreCondition.WhenWritingNull
        // (D2Result.cs). errorCode and traceId serialize as null.
        var result = D2Result.Ok();
        WriteRoundTripFixture("round-trip-ok", result);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_RoundTripOkWithData()
    {
        // D2Result<T>.Ok carrying a typed payload - the {data: {...}}
        // field is present in the wire shape.
        var result = D2Result<TestEntity>.Ok(new TestEntity { Id = "x", Name = "fixture" });
        WriteRoundTripFixture("round-trip-ok-with-data", result);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_RoundTripNotFound()
    {
        // Canonical NotFound failure - {success: false, statusCode: 404,
        // errorCode: "GEN_NOT_FOUND", messages: [TKMessage]}.
        var result = D2Result.NotFound(messages: [new TKMessage("common_errors_NOT_FOUND")]);
        WriteRoundTripFixture("round-trip-not-found", result);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_RoundTripValidationFailed()
    {
        // Canonical ValidationFailed - {success: false, statusCode: 400,
        // errorCode: "GEN_VALIDATION_FAILED", inputErrors: [{field, errors}]}.
        var result = D2Result.ValidationFailed(
            inputErrors:
            [
                new InputError(
                    "email",
                    [new TKMessage("common_validation_EMAIL_INVALID")]),
            ]);
        WriteRoundTripFixture("round-trip-validation-failed", result);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_RoundTripWithTraceId()
    {
        // Failure result carrying a trace id - pins the {traceId: "..."}
        // wire field for log-correlation.
        var result = D2Result.UnhandledException()
            .WithTraceId("0123456789abcdef0123456789abcdef");
        WriteRoundTripFixture("round-trip-with-trace-id", result);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_RoundTripWithCategory()
    {
        // A spec-derived failure factory stamps its code's category - pins the
        // {category: "not_found"} snake-wire field. NotFound() carries
        // ErrorCategory.NotFound, serialized via ErrorCategoryJsonConverter.
        var result = D2Result.NotFound(messages: [new TKMessage("common_errors_NOT_FOUND")]);
        WriteRoundTripFixture("round-trip-with-category", result);
    }

    private static void WriteRoundTripFixture(string scenario, D2Result result)
    {
        // Serialize via the production System.Text.Json pipeline. The
        // [JsonPropertyName] attributes on D2Result properties pin
        // camelCase wire names regardless of JsonSerializerOptions.
        var json = JsonSerializer.Serialize<object>(result);
        using var doc = JsonDocument.Parse(json);
        var data = JsonElementToObject(doc.RootElement);
        FixturePathHelpers.WriteFixture(CATALOG, scenario, data!);
    }

    private static object? JsonElementToObject(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.Object => JsonObjectToDict(el),
            JsonValueKind.Array => JsonArrayToList(el),
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var i) ? (object)i : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static Dictionary<string, object?> JsonObjectToDict(JsonElement obj)
    {
        var d = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var p in obj.EnumerateObject())
            d[p.Name] = JsonElementToObject(p.Value);

        return d;
    }

    private static List<object?> JsonArrayToList(JsonElement arr)
    {
        var l = new List<object?>();
        foreach (var item in arr.EnumerateArray()) l.Add(JsonElementToObject(item));

        return l;
    }

    /// <summary>
    /// Reflect every <c>public const string</c> on the catalog type;
    /// produce a sorted map keyed by the constant name (e.g.
    /// <c>SUCCESS</c>) so the fixture has the same keys and values as the
    /// TypeScript const map.
    /// </summary>
    private static SortedDictionary<string, object?> EnumerateConstants(Type type)
    {
        var fields = type
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .OrderBy(f => f.Name, StringComparer.Ordinal);
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var f in fields)
            data[f.Name] = (string)f.GetValue(null)!;

        return data;
    }

    /// <summary>
    /// Synthetic payload type for round-trip-ok-with-data - a small
    /// POCO with an <c>Id</c> and a <c>Name</c>, so the fixture's data
    /// sub-tree stays compact and the fixture pins the envelope, not
    /// the payload. <see cref="System.Text.Json.JsonSerializer"/> reads the properties
    /// via reflection.
    /// </summary>
    private sealed class TestEntity
    {
        [JetBrains.Annotations.UsedImplicitly]
        public string Id { get; init; } = string.Empty;

        [JetBrains.Annotations.UsedImplicitly]
        public string Name { get; init; } = string.Empty;
    }
}
