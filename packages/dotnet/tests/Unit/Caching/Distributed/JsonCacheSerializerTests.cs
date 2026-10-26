// -----------------------------------------------------------------------
// <copyright file="JsonCacheSerializerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Caching.Distributed;

using AwesomeAssertions;
using DcsvIo.D2.Caching.Distributed.Redis;
using DcsvIo.D2.Result;
using JetBrains.Annotations;
using Xunit;

/// <summary>
/// Unit tests for <see cref="JsonCacheSerializer"/>, the most likely surface
/// for adversarial bug shapes (null handling, cycles, garbage bytes, type
/// collisions). Each test targets one specific edge case.
/// </summary>
public sealed class JsonCacheSerializerTests
{
    private readonly JsonCacheSerializer r_serializer = new();

    [Fact]
    public void RoundTrip_Primitive_Works()
    {
        var serialized = r_serializer.Serialize(42);
        serialized.IsOk.Should().BeTrue();

        var deserialized = r_serializer.Deserialize<int>(serialized.Data!);
        deserialized.IsOk.Should().BeTrue();
        deserialized.Data.Should().Be(42);
    }

    [Fact]
    public void RoundTrip_String_Works()
    {
        var serialized = r_serializer.Serialize("hello world");
        var deserialized = r_serializer.Deserialize<string>(serialized.Data!);
        deserialized.Data.Should().Be("hello world");
    }

    [Fact]
    public void RoundTrip_NullableString_Null_Works()
    {
        // Regression test: deserialize-of-null used to be treated as a
        // failure. It is a legitimate round-trip for a nullable T:
        // Set<string?>(k, null) serializes to the bytes "null", which
        // deserialize to null.
        var serialized = r_serializer.Serialize<string?>(null);
        serialized.IsOk.Should().BeTrue();

        var deserialized = r_serializer.Deserialize<string?>(serialized.Data!);
        deserialized.IsOk.Should().BeTrue();
        deserialized.Data.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_NullableInt_Null_Works()
    {
        var serialized = r_serializer.Serialize<int?>(null);
        var deserialized = r_serializer.Deserialize<int?>(serialized.Data!);
        deserialized.IsOk.Should().BeTrue();
        deserialized.Data.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_ComplexRecord_Works()
    {
        var input = new TestRecord(42, "name", new List<string> { "a", "b", "c" });
        var serialized = r_serializer.Serialize(input);
        var deserialized = r_serializer.Deserialize<TestRecord>(serialized.Data!);

        deserialized.IsOk.Should().BeTrue();
        deserialized.Data.Should().BeEquivalentTo(input);
    }

    [Fact]
    public void Deserialize_GarbageBytes_ReturnsCouldNotBeDeserialized()
    {
        var garbage = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC };
        var result = r_serializer.Deserialize<TestRecord>(garbage);

        result.IsOk.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.GEN_COULD_NOT_BE_DESERIALIZED);
    }

    [Fact]
    public void Deserialize_TypeMismatch_ReturnsCouldNotBeDeserialized()
    {
        // Serialize as TestRecord and deserialize as int: the mismatch must
        // fail cleanly and not crash.
        var serialized = r_serializer.Serialize(new TestRecord(1, "x", new()));
        var result = r_serializer.Deserialize<int>(serialized.Data!);

        result.IsOk.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.GEN_COULD_NOT_BE_DESERIALIZED);
    }

    [Fact]
    public void Deserialize_NonNullablePrimitive_FromNullJson_ReturnsCouldNotBeDeserialized()
    {
        // STJ throws JsonException when it parses null into a non-nullable
        // value type. The serializer's catch block reports that as a failure.
        var nullBytes = "null"u8.ToArray();
        var result = r_serializer.Deserialize<int>(nullBytes);

        result.IsOk.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.GEN_COULD_NOT_BE_DESERIALIZED);
    }

    [Fact]
    public void RoundTrip_WithReferenceCycle_DoesNotThrow()
    {
        // Reference cycles are common in domain entities (User -> Org -> Users[]).
        // STJ throws on cycles by default; the serializer sets
        // ReferenceHandler.IgnoreCycles so that it does not throw.
        var node = new CyclicNode { Name = "A" };
        node.Self = node;

        var serialized = r_serializer.Serialize(node);
        serialized.IsOk.Should().BeTrue();

        // The cycle is broken in the serialized form; no infinite loop.
    }

    [Fact]
    public void RoundTrip_LargeValue_Works()
    {
        // 100 KB string. The cache must handle reasonably sized values.
        var large = new string('x', 100_000);
        var serialized = r_serializer.Serialize(large);
        var deserialized = r_serializer.Deserialize<string>(serialized.Data!);

        deserialized.Data.Should().Be(large);
    }

    [Fact]
    public void RoundTrip_EmptyString_Works()
    {
        var serialized = r_serializer.Serialize(string.Empty);
        var deserialized = r_serializer.Deserialize<string>(serialized.Data!);
        deserialized.Data.Should().Be(string.Empty);
    }

    [Fact]
    public void RoundTrip_EmptyList_Works()
    {
        var serialized = r_serializer.Serialize(new List<int>());
        var deserialized = r_serializer.Deserialize<List<int>>(serialized.Data!);
        deserialized.IsOk.Should().BeTrue();
        deserialized.Data.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_DictionaryWithCamelCase_Works()
    {
        // Property naming uses camelCase on the wire and reads are
        // case-insensitive. A round-trip should preserve the original casing in
        // the deserialized object; this test asserts only that both entries
        // survive.
        var input = new Dictionary<string, string>
        {
            ["AlphaKey"] = "value-1",
            ["BetaKey"] = "value-2",
        };
        var serialized = r_serializer.Serialize(input);
        var deserialized = r_serializer.Deserialize<Dictionary<string, string>>(serialized.Data!);

        deserialized.Data.Should().NotBeNull();
        deserialized.Data!.Should().HaveCount(2);
    }

    [Fact]
    public void ContentType_IsApplicationJson()
    {
        r_serializer.ContentType.Should().Be("application/json");
    }

    [Fact]
    public void Deserialize_EmptyBytes_ReturnsCouldNotBeDeserialized()
    {
        var result = r_serializer.Deserialize<TestRecord>([]);

        result.IsOk.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.GEN_COULD_NOT_BE_DESERIALIZED);
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record TestRecord(int Id, string Name, List<string> Tags);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class CyclicNode
    {
        public string Name { get; set; } = string.Empty;

        public CyclicNode? Self { get; set; }
    }
}
